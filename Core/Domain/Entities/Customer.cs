using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class Customer : BaseEntity
    {
        public required string Name { get; set; } // Alt Müşteri Adı

        // Hangi ana firmaya (Tenant) bağlı olduğu
        public Guid TenantId { get; set; }
        public virtual Tenant Tenant { get; set; } = null!;

        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }
}
