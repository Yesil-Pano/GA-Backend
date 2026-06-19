using GA.Core.Domain.Common;
using NetTopologySuite.Geometries;

namespace GA.Core.Domain.Entities
{
    public class Station : BaseEntity, IMultiTenant
    {
        public string Name { get; set; } = string.Empty;
        public string StatusType { get; set; } = "Alt Yapı Tamamlandı"; // Alt Yapı Tamamlandı, Enerji Bekliyor, Yayınlandı
        public string PowerType { get; set; } = "AC"; // ACDC, AC, DC
        public string PersonnelName { get; set; } = string.Empty;
        public string PersonnelPhone { get; set; } = string.Empty;
        public string Edas { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PointType { get; set; } = "YG Abonelik"; // YG Abonelik, AG Abonelik, Süzme Sayaç
        public string City { get; set; } = "Ankara";

        // 📂 KURUMSAL DOSYA YÜKLEME ALANLARI (BELGE YOLLARI)
        public string? GeneralFilePath { get; set; }
        public string? YgTescilBelgesiPath { get; set; }
        public string? YgSozlesmesiPath { get; set; }
        public string? SabitFotograflarPath { get; set; }
        public string? YillikBakimFormuPath { get; set; }
        public string? YgIsletmeBelgesiPath { get; set; }

        // PostGIS Coğrafi Konum Koordinatı
        public Point Location { get; set; } = null!;

        // 🔒 Çoklu Kiracı (Multi-Tenant) İzolasyon Alanları
        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}