using GA.Core.Domain.Common;
using System;

namespace GA.Core.Domain.Entities
{
    public class Tenant : BaseEntity
    {
        public required string Name { get; set; }
        public string? TaxNumber { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>Demo firma — süre dolunca web/mobil erişim kapanır.</summary>
        public bool IsDemo { get; set; }

        /// <summary>Demo bitiş (UTC). IsDemo=false iken null.</summary>
        public DateTime? DemoExpiresAt { get; set; }

        /// <summary>Web firma seçici anahtarı (trugo, tesla, …). Benzersiz.</summary>
        public string? PartnerKey { get; set; }

        public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }
}
