using GA.Core.Domain.Common;
using NetTopologySuite.Geometries;

namespace GA.Core.Domain.Entities
{
    public class User : BaseEntity, IMultiTenant
    {
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
        public required string FullName { get; set; }
        public required string PhoneNumber { get; set; }
        public bool IsActive { get; set; } = true;

        // Canlı konum takibi (nullable — henüz konum paylaşmamış kullanıcılar)
        public Point? Location { get; set; }
        public DateTime? LocationUpdatedAt { get; set; }

        public virtual FieldWorkerProfile? FieldWorkerProfile { get; set; }
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}