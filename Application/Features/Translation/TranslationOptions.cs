namespace GA.Application.Features.Translation
{
    public class TranslationOptions
    {
        public const string SectionName = "Translation";

        /// <summary>Gemini | Groq</summary>
        public string PrimaryProvider { get; set; } = "Gemini";

        public ProviderOptions Gemini { get; set; } = new();
        public ProviderOptions Groq { get; set; } = new();
    }

    public class ProviderOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
    }
}
