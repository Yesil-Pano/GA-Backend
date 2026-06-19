using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class Permission : BaseEntity
    {
        public required string Name { get; set; } // Örn: "Job.ViewISG"
        public required string Description { get; set; }

        // Çoka çok ilişki için (Navigation Property)
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
