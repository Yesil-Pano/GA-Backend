using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class Tenant : BaseEntity
    {
        public required string Name { get; set; } // Firma Adı (Örn: Trugo)
        public string? TaxNumber { get; set; }     // Vergi Numarası
        public bool IsActive { get; set; } = true;

        // Bir ana firmanın birden fazla alt müşterisi olabilir
        public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }
}
