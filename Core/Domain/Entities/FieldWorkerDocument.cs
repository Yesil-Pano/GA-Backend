using GA.Core.Domain.Common;
using System;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Saha personeli evrakları (Yetki Belgesi + Personel Evrak Bilgisi).
    /// Binary içerik PostgreSQL bytea olarak saklanır.
    /// </summary>
    public class FieldWorkerDocument : BaseEntity, IMultiTenant
    {
        public Guid FieldWorkerProfileId { get; set; }
        public virtual FieldWorkerProfile FieldWorkerProfile { get; set; } = null!;

        /// <summary>Authorization | Personnel</summary>
        public string DocumentType { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}
