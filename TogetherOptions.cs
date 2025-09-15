namespace Cura.Web
{
    public class TogetherOptions
    {
        public string BaseUrl { get; set; } = "https://api.together.xyz/v1";
        public string Model { get; set; } = "meta-llama/Llama-3.3-70B-Instruct-Turbo-Free";
        public string? ApiKey { get; set; } 
    }
}
