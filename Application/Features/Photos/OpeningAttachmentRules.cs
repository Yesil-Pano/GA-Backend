namespace GA.Application.Features.Photos
{
    public static class OpeningAttachmentRules
    {
        public const string Category = "ACILIS";
        public const int MaxPerWorkOrder = 5;
        public const long MaxImageBytes = 10L * 1024 * 1024;
        public const long MaxVideoBytes = 30L * 1024 * 1024;

        private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
        };

        private static readonly HashSet<string> VideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4",
            "video/quicktime",
            "video/webm",
        };

        public static bool IsOpeningCategory(string? description) =>
            string.Equals(description?.Trim(), Category, StringComparison.OrdinalIgnoreCase);

        public static bool IsAllowedContentType(string contentType) =>
            ImageContentTypes.Contains(contentType) || VideoContentTypes.Contains(contentType);

        public static bool IsVideoContentType(string contentType) =>
            VideoContentTypes.Contains(contentType);

        public static long MaxBytesForContentType(string contentType) =>
            IsVideoContentType(contentType) ? MaxVideoBytes : MaxImageBytes;
    }
}
