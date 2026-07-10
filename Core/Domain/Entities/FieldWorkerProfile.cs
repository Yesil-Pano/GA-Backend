using NetTopologySuite.Geometries;
using GA.Core.Domain.Common;
using System;
using System.Collections.Generic;

namespace GA.Core.Domain.Entities
{
    public class FieldWorkerProfile : BaseEntity, IMultiTenant // 🔒 Multi-Tenant zırhı eklendi
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public string? VehiclePlate { get; set; }
        public string? ProjectName { get; set; }
        public string? TeamLeader { get; set; }

        // 🚀 CSV'DEN GELEN YENİ ADRES VE BÖLGE ALANLARI
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }

        public Point? HomeLocation { get; set; }

        // 🔒 Şirket Ayrıştırma Alanları
        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }

        public ICollection<Project> Projects { get; set; } = new List<Project>();
    }
}