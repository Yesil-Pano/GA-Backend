using GA.Core.Domain.Common;
using NetTopologySuite.Geometries;

namespace GA.Core.Domain.Entities
{
    public class WorkOrder : BaseEntity, IMultiTenant // 🔒 Multi-Tenant zırhı eklendi
    {
        public string Title { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MobileDescription { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Priority { get; set; } = "Orta";
        public string Status { get; set; } = "Bekliyor";
        public string WorkType { get; set; } = "Arıza";
        public string WorkCategory { get; set; } = "Arıza Bildirimi";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Point Location { get; set; } = null!;

        public Guid? OperationUserId { get; set; }
        public Guid? OpenedByUserId { get; set; }
        public Guid? AssignedToUserId { get; set; }

        // 🔒 Şirket Ayrıştırma Alanları
        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}