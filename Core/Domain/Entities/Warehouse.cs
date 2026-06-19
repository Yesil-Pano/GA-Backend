using NetTopologySuite.Geometries;
using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class Warehouse : BaseEntity
    {
        public required string Name { get; set; }
        public string? Address { get; set; }

        // Harita üzerinde deponun koordinat konumu (PostGIS)
        public Point? Location { get; set; }

        // Depodan sorumlu personelin (User) kimliği
        public Guid? ManagerId { get; set; }
        public virtual User? Manager { get; set; }

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}
