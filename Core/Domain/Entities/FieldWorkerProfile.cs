using NetTopologySuite.Geometries;
using GA.Core.Domain.Common;
using System;
using System.Collections.Generic;

namespace GA.Core.Domain.Entities
{
    public class FieldWorkerProfile : BaseEntity, IMultiTenant
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public string? VehiclePlate { get; set; }
        public string? ProjectName { get; set; }
        public string? TeamLeader { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }

        public Point? HomeLocation { get; set; }

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }

        public ICollection<Project> Projects { get; set; } = new List<Project>();
        public ICollection<FieldWorkerDocument> Documents { get; set; } = new List<FieldWorkerDocument>();
    }
}
