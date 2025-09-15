using System.Text.Json;
using Cura.Web.Models;
using Microsoft.Extensions.Options;

namespace Cura.Web.Data
{
    public class FileDataOptions
    {
        public string Folder { get; set; } = "Data/FhirBundles"; // relative to ContentRoot
    }

    public class FileFhirPatientRepo : IPatientRepo
    {
        private readonly string _absFolder;
        private readonly object _lock = new();
        private volatile bool _loaded;
        private readonly Dictionary<string, Patient> _patients = new(StringComparer.OrdinalIgnoreCase);

        public FileFhirPatientRepo(IWebHostEnvironment env, IOptions<FileDataOptions> opts)
        {
            // Resolve to absolute path inside the deployed app
            _absFolder = Path.IsPathRooted(opts.Value.Folder)
                ? opts.Value.Folder
                : Path.Combine(env.ContentRootPath, opts.Value.Folder);
        }

        public IEnumerable<Patient> GetAll()
        {
            EnsureLoaded();
            return _patients.Values.OrderBy(p => p.FullName);
        }

        public Patient? Get(string id)
        {
            EnsureLoaded();
            return _patients.TryGetValue(id, out var p) ? p : null;
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_lock)
            {
                if (_loaded) return;
                LoadAll();
                _loaded = true;
            }
        }

        private void LoadAll()
        {
            if (!Directory.Exists(_absFolder)) return;

            foreach (var file in Directory.EnumerateFiles(_absFolder, "*.json", SearchOption.AllDirectories))
            {
                using var fs = File.OpenRead(file);
                using var doc = JsonDocument.Parse(fs);
                if (!doc.RootElement.TryGetProperty("entry", out var entries)) continue;

                Patient? pat = null;
                var conds = new List<Condition>();
                var meds = new List<Medication>();
                var obs = new List<Observation>();
                string lastNote = "";

                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("resource", out var res)) continue;
                    var type = res.GetProperty("resourceType").GetString();

                    switch (type)
                    {
                        case "Patient":
                            pat = MapPatient(res);
                            break;

                        case "Condition":
                            AddIfNotEmpty(conds, ReadCodeText(res));
                            break;

                        case "MedicationRequest":
                            AddIfNotEmpty(meds, ReadCodeText(res, "medicationCodeableConcept"));
                            break;

                        case "Observation":
                            var code = ReadCodeText(res);
                            var val = ReadObservationValue(res);
                            var dt = res.TryGetProperty("effectiveDateTime", out var dEl)
                                       ? ParseDate(dEl.GetString())
                                       : DateTime.MinValue;
                            var id = res.TryGetProperty("id", out var idEl)
                                       ? $"FHIR:Observation/{idEl.GetString()}"
                                       : "FHIR:Observation/unknown";
                            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(val))
                                obs.Add(new Observation(code!, val!, dt == DateTime.MinValue ? DateTime.UtcNow : dt, id));
                            break;

                        case "DocumentReference":
                        case "DiagnosticReport":
                            if (string.IsNullOrEmpty(lastNote) &&
                                res.TryGetProperty("text", out var t) &&
                                t.TryGetProperty("div", out var div))
                            {
                                lastNote = StripTags(div.GetString() ?? "");
                            }
                            break;
                    }
                }

                if (pat is null) continue;
                pat.Conditions = conds;
                pat.Medications = meds;
                pat.Observations = obs.OrderByDescending(o => o.Date).Take(8).ToList();
                pat.LastNote = string.IsNullOrWhiteSpace(lastNote) ? "—" : lastNote;

                _patients[pat.Id] = pat;
            }
        }

        private static void AddIfNotEmpty<T>(List<T> list, string? text) where T : class
        {
            if (typeof(T) == typeof(Condition) && !string.IsNullOrWhiteSpace(text))
                list.Add(new Condition(text!) as T);
            if (typeof(T) == typeof(Medication) && !string.IsNullOrWhiteSpace(text))
                list.Add(new Medication(text!) as T);
        }

        private static Patient MapPatient(JsonElement res)
        {
            var id = res.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            string fullName = "Unknown";
            if (res.TryGetProperty("name", out var nameArr) && nameArr.ValueKind == JsonValueKind.Array && nameArr.GetArrayLength() > 0)
            {
                var n0 = nameArr[0];
                var family = n0.TryGetProperty("family", out var fam) ? fam.GetString() : "";
                var given = (n0.TryGetProperty("given", out var giv) && giv.ValueKind == JsonValueKind.Array && giv.GetArrayLength() > 0)
                    ? string.Join(" ", giv.EnumerateArray().Select(g => g.GetString()))
                    : "";
                fullName = $"{given} {family}".Trim();
            }
            var gender = res.TryGetProperty("gender", out var gEl) ? (gEl.GetString() ?? "").ToLowerInvariant() : "";
            var dob = res.TryGetProperty("birthDate", out var bEl) ? ParseDate(bEl.GetString()) : DateTime.MinValue;

            return new Patient
            {
                Id = id,
                FullName = string.IsNullOrWhiteSpace(fullName) ? id : fullName,
                Gender = string.IsNullOrWhiteSpace(gender) ? "unknown" : gender,
                BirthDate = dob == DateTime.MinValue ? new DateTime(1980, 1, 1) : dob,
            };
        }

        private static string? ReadCodeText(JsonElement res, string codeProp = "code")
        {
            if (!res.TryGetProperty(codeProp, out var code)) return null;

            if (code.TryGetProperty("text", out var text) && !string.IsNullOrWhiteSpace(text.GetString()))
                return text.GetString();

            if (code.TryGetProperty("coding", out var coding) && coding.ValueKind == JsonValueKind.Array && coding.GetArrayLength() > 0)
            {
                var c0 = coding[0];
                if (c0.TryGetProperty("display", out var disp) && !string.IsNullOrWhiteSpace(disp.GetString()))
                    return disp.GetString();
                if (c0.TryGetProperty("code", out var cc) && !string.IsNullOrWhiteSpace(cc.GetString()))
                    return cc.GetString();
            }
            return null;
        }

        private static string? ReadObservationValue(JsonElement res)
        {
            if (res.TryGetProperty("valueQuantity", out var vq))
            {
                var val = vq.TryGetProperty("value", out var vv) ? vv.ToString() : "";
                var unit = vq.TryGetProperty("unit", out var uu) ? uu.GetString() : "";
                return string.IsNullOrWhiteSpace(unit) ? val : $"{val} {unit}";
            }
            if (res.TryGetProperty("valueString", out var vs))
                return vs.GetString();
            if (res.TryGetProperty("valueCodeableConcept", out var vc))
                return ReadCodeText(vc) ?? "";
            return null;
        }

        private static DateTime ParseDate(string? s) => DateTime.TryParse(s, out var d) ? d : DateTime.MinValue;

        private static string StripTags(string html) =>
            System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ").Trim();
    }
}
