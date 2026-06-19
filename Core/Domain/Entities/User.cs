using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class User : BaseEntity, IMultiTenant // 🔒 Multi-Tenant zırhı eklendi
    {
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
        public required string FullName { get; set; }
        public required string PhoneNumber { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual FieldWorkerProfile? FieldWorkerProfile { get; set; }
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}