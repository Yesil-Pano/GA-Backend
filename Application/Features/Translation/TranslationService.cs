using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GA.Application.Features.Translation
{
    public class TranslationService : ITranslationService
    {
        private readonly TranslationOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TranslationService> _logger;

        public TranslationService(
            IOptions<TranslationOptions> options,
            IHttpClientFactory httpClientFactory,
            ILogger<TranslationService> logger)
        {
            _options = options.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<WorkOrderTranslationResult> TranslateWorkOrderAsync(
            string title,
            string description,
            string mobileDescription,
            string? fieldNote,
            CancellationToken ct = default)
        {
            var primary = NormalizeProvider(_options.PrimaryProvider);
            var secondary = primary == "gemini" ? "groq" : "gemini";

            try
            {
                return await TranslateWithProviderAsync(primary, title, description, mobileDescription, fieldNote, ct);
            }
            catch (Exception ex) when (IsQuotaOrRateLimit(ex))
            {
                _logger.LogWarning(ex, "Translation primary provider {Provider} quota/rate-limited; falling back to {Fallback}", primary, secondary);
                return await TranslateWithProviderAsync(secondary, title, description, mobileDescription, fieldNote, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Translation primary provider {Provider} failed; falling back to {Fallback}", primary, secondary);
                return await TranslateWithProviderAsync(secondary, title, description, mobileDescription, fieldNote, ct);
            }
        }

        private async Task<WorkOrderTranslationResult> TranslateWithProviderAsync(
            string provider,
            string title,
            string description,
            string mobileDescription,
            string? fieldNote,
            CancellationToken ct)
        {
            var prompt = BuildPrompt(title, description, mobileDescription, fieldNote);

            var jsonText = provider == "groq"
                ? await CallGroqAsync(prompt, ct)
                : await CallGeminiAsync(prompt, ct);

            using var doc = JsonDocument.Parse(ExtractJsonObject(jsonText));
            var root = doc.RootElement;

            return new WorkOrderTranslationResult(
                TitleEn: root.TryGetProperty("titleEn", out var t) ? (t.GetString() ?? "") : "",
                DescriptionEn: root.TryGetProperty("descriptionEn", out var d) ? (d.GetString() ?? "") : "",
                MobileDescriptionEn: root.TryGetProperty("mobileDescriptionEn", out var m) ? (m.GetString() ?? "") : "",
                FieldNoteEn: root.TryGetProperty("fieldNoteEn", out var f) ? f.GetString() : null,
                Provider: provider == "groq" ? "Groq" : "Gemini");
        }

        private static string BuildPrompt(string title, string description, string mobileDescription, string? fieldNote)
        {
            return
                """
                You are a professional translator. Translate the following Turkish work-order fields to English.
                Keep meaning faithful. Keep proper names, plates, codes, and addresses unchanged when appropriate.
                Return ONLY a valid JSON object with keys: titleEn, descriptionEn, mobileDescriptionEn, fieldNoteEn.
                If fieldNote is empty, set fieldNoteEn to null.

                Input JSON:
                """
                + JsonSerializer.Serialize(new
                {
                    title,
                    description,
                    mobileDescription,
                    fieldNote = string.IsNullOrWhiteSpace(fieldNote) ? null : fieldNote
                });
        }

        private async Task<string> CallGeminiAsync(string prompt, CancellationToken ct)
        {
            var apiKey = _options.Gemini.ApiKey?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("PASTE_", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Gemini API key is not configured.");

            var model = string.IsNullOrWhiteSpace(_options.Gemini.Model) ? "gemini-2.0-flash" : _options.Gemini.Model;
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new { temperature = 0.2, responseMimeType = "application/json" }
            };

            var client = _httpClientFactory.CreateClient("translation");
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var res = await client.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"Gemini error {(int)res.StatusCode}: {body}", null, res.StatusCode);

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Gemini returned empty translation.");

            return text;
        }

        private async Task<string> CallGroqAsync(string prompt, CancellationToken ct)
        {
            var apiKey = _options.Groq.ApiKey?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("PASTE_", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Groq API key is not configured.");

            var model = string.IsNullOrWhiteSpace(_options.Groq.Model) ? "llama-3.3-70b-versatile" : _options.Groq.Model;
            var client = _httpClientFactory.CreateClient("translation");

            var payload = new
            {
                model,
                temperature = 0.2,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = "Return only valid JSON." },
                    new { role = "user", content = prompt }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var res = await client.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"Groq error {(int)res.StatusCode}: {body}", null, res.StatusCode);

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Groq returned empty translation.");

            return text;
        }

        private static string ExtractJsonObject(string raw)
        {
            var s = raw.Trim();
            if (s.StartsWith("```"))
            {
                var firstNl = s.IndexOf('\n');
                if (firstNl > 0) s = s[(firstNl + 1)..];
                var fence = s.LastIndexOf("```", StringComparison.Ordinal);
                if (fence >= 0) s = s[..fence];
                s = s.Trim();
            }

            var start = s.IndexOf('{');
            var end = s.LastIndexOf('}');
            if (start >= 0 && end > start)
                return s[start..(end + 1)];

            return s;
        }

        private static string NormalizeProvider(string? value) =>
            string.Equals(value?.Trim(), "groq", StringComparison.OrdinalIgnoreCase) ? "groq" : "gemini";

        private static bool IsQuotaOrRateLimit(Exception ex)
        {
            if (ex is HttpRequestException httpEx && httpEx.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                return true;

            var msg = ex.Message ?? "";
            return msg.Contains("429", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("quota", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("resource_exhausted", StringComparison.OrdinalIgnoreCase);
        }
    }
}
