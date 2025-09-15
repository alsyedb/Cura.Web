using Cura.Web.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Markdig;

namespace Cura.Web.Services
{
    public interface IAiClient
    {
        Task<string> SummarizeAndDraftAsync(Patient p, string? userQuestion);
    }

    public class TogetherAiClient : IAiClient
    {
        private readonly HttpClient _http;
        private readonly TogetherOptions _opts;
        private static readonly JsonSerializerOptions JsonOpts = new()
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public TogetherAiClient(HttpClient http, IOptions<TogetherOptions> opts)
        {
            _http = http;
            _opts = opts.Value;
        }

        public async Task<string> SummarizeAndDraftAsync(Patient p, string? userQuestion)
        {
            var apiKey = Environment.GetEnvironmentVariable("TOGETHER_API_KEY") ?? _opts.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("TOGETHER_API_KEY not configured.");

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var system =
            @"You are a clinical assistant responding to a licensed physician inside a secure EHR.
            Audience: Doctor (assume medical knowledge).
            Tone: respectful, collegial, professional; no lay explanations, no fluff, no generic disclaimers.

            When a question is present, ANSWER IT FIRST, directly.
            Constraints:
            - Be concise (≤ 120 words or ≤ 6 bullets).
            - Do NOT restate the chart; cite only facts you actually use, e.g., [FHIR:Observation/123].
            - If a critical datum is missing, ask at most ONE clarifying question at the end.
            - Do not provide medication or place final orders unless the user asked.
            - If uncertain, state uncertainty briefly.

            If NO question is provided, return:
            - A 3-bullet chart summary, then a ≤5-line SOAP draft (concise).";


            var recentObs = p.Observations
                .OrderByDescending(o => o.Date)
                .Take(3)
                .Select(o => $"- {o.Code}: {o.Value} on {o.Date:yyyy-MM-dd} [{o.RefId}]");
            var obsLines = string.Join("\n", recentObs);

            var hasQuestion = !string.IsNullOrWhiteSpace(userQuestion);
            var task = hasQuestion
                ? "Answer the question directly. If a fact from the patient is relevant, cite it once."
                : "No question provided: Return a 3-bullet chart summary and a 5-line SOAP draft (concise).";

            var user =
                        $@"
                Patient: {p.FullName} (ID={p.Id})
                Gender: {p.Gender}, DOB: {p.BirthDate:yyyy-MM-dd}
                Problems: {string.Join(", ", p.Conditions.Select(c => c.Name))}
                Medications: {string.Join(", ", p.Medications.Select(m => m.Name))}
                Recent Observations (latest up to 3):
                {obsLines}
                Last note: {p.LastNote}

                Task: {task}
                Question: {(hasQuestion ? userQuestion : "(none)")}
                ";

            var payload = new
            {
                model = _opts.Model,
                temperature = 0.1,
                max_tokens = hasQuestion ? 300 : 450,
                frequency_penalty = 0.6,   // discourage repetition
                presence_penalty = 0.2,   // keep focused
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user",   content = user }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, $"{_opts.BaseUrl}/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new ApplicationException($"AI error {resp.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var md = content ?? "(no content)";
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var html = Markdown.ToHtml(md, pipeline);
            return $"<div class=\"markdown-body\">{html}</div>";
        }

    }
}
