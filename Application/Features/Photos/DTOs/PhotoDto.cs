namespace GA.Application.Features.Photos.DTOs
{
    /// <summary>
    /// Fotoğraf listelerken Data (binary) döndürülmez — yalnızca metadata.
    /// Resim verisi için GET /api/photos/{id}/image kullanılır.
    /// </summary>
    public class PhotoDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? Description { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
