using NetTopologySuite.Geometries;
using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class FieldWorkerProfile : BaseEntity, IMultiTenant // 🔒 Multi-Tenant zırhı eklendi
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public string? VehiclePlate { get; set; }
        public string? ProjectName { get; set; }
        public string? TeamLeader { get; set; } // 💡 Videodaki Ekip Lideri alanı eklendi

        public Point? HomeLocation { get; set; }

        // 🔒 Şirket Ayrıştırma Alanları
        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}