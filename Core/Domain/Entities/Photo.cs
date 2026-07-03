using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Web veya mobil üzerinden yüklenen fotoğrafları binary olarak tutar.
    /// EntityType + EntityId ile herhangi bir tabloya (WorkOrder, Survey, vb.) bağlanır.
    /// </summary>
    public class Photo : BaseEntity, IMultiTenant
    {
        /// <summary>Orijinal dosya adı (ör: DCIM_20260703.jpg)</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>MIME tipi (image/jpeg, image/png, image/webp)</summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>Gerçek binary içerik — PostgreSQL bytea</summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>Dosya boyutu (byte). Sorgularda Data okunmadan boyut göstermek için.</summary>
        public long FileSize { get; set; }

        /// <summary>Opsiyonel başlık/açıklama</summary>
        public string? Description { get; set; }

        /// <summary>Hangi modüle ait: "WorkOrder" | "Survey" | "Station" | ...</summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>O modüldeki kaydın ID'si</summary>
        public Guid EntityId { get; set; }

        /// <summary>Yükleyen kullanıcı</summary>
        public Guid UserId { get; set; }

        // Multi-tenant
        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}
