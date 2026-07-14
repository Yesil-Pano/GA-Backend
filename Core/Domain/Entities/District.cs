namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Referans ilçe tablosu. Kaynak DB (Districts) ile uyumlu şema — cross-server veri kopyası için.
    /// </summary>
    public class District
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Latitude { get; set; } = string.Empty;

        public string Longitude { get; set; } = string.Empty;

        public Guid CityId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime LastUpdate { get; set; }

        public string Createdby { get; set; } = string.Empty;

        public string Updatedby { get; set; } = string.Empty;

        public City City { get; set; } = null!;
    }
}
