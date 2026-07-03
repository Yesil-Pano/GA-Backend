namespace GA.Application.Features.Photos.DTOs
{
    public class UploadPhotoRequest
    {
        /// <summary>Base64 kodlu resim verisi (data:image/... öneki olmadan)</summary>
        public required string Base64Data { get; set; }

        /// <summary>Orijinal dosya adı</summary>
        public required string FileName { get; set; }

        /// <summary>MIME tipi: image/jpeg, image/png, image/webp</summary>
        public required string ContentType { get; set; }

        /// <summary>Hangi modüle ait: WorkOrder | Survey | Station</summary>
        public required string EntityType { get; set; }

        /// <summary>O modüldeki kaydın ID'si</summary>
        public required Guid EntityId { get; set; }

        /// <summary>Opsiyonel açıklama</summary>
        public string? Description { get; set; }
    }
}
