namespace GA.Application.Features.Common
{
    /// <summary>Türkçe I/İ duyarsız karşılaştırma yardımcıları.</summary>
    public static class TurkishText
    {
        public static string Fold(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace('\u0130', 'i') // İ
                .Replace('I', 'ı')
                .ToLowerInvariant();
        }

        public static bool Contains(string? haystack, string? needle)
        {
            if (string.IsNullOrWhiteSpace(needle)) return true;
            if (string.IsNullOrEmpty(haystack)) return false;
            return Fold(haystack).Contains(Fold(needle.Trim()), StringComparison.Ordinal);
        }
    }
}
