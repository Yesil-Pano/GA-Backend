using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class Role : BaseEntity
    {
        public required string Name { get; set; } // Örn: "IsgManager", "FieldWorker"
        public string? Description { get; set; }

        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
