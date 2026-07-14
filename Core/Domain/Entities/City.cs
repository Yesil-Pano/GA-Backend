using System.Collections.Generic;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Referans il tablosu. Kaynak DB (Cities) ile uyumlu şema — cross-server veri kopyası için.
    /// Multi-tenant değildir (tüm kiracılar ortak kullanır).
    /// </summary>
    public class City
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Latitude { get; set; } = string.Empty;

        public string Longitude { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime LastUpdate { get; set; }

        public string Createdby { get; set; } = string.Empty;

        public string Updatedby { get; set; } = string.Empty;

        public ICollection<District> Districts { get; set; } = new List<District>();
    }
}
