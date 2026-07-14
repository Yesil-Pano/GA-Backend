using GA.Core.Domain.Common;
using NetTopologySuite.Geometries;
using System;

namespace GA.Core.Domain.Entities
{
    public class Station : BaseEntity, IMultiTenant
    {
        public string Name { get; set; } = string.Empty;
        public string StatusType { get; set; } = "Alt Yapı Tamamlandı";
        public string PowerType { get; set; } = "DC";
        public string City { get; set; } = "Ankara";

        // 🚀 EXCEL'DEN GELEN YENİ VE DETAYLI DONANIM/OPERASYON ALANLARI
        public string? ChargepointId { get; set; }
        public string? DeviceVendor { get; set; }
        public string? VendorModel { get; set; }
        public int? SocketCount { get; set; }
        public string? DevicePower { get; set; }
        public string? District { get; set; }
        public string? PartnerStatus { get; set; }
        public string? OwnerCompany { get; set; }
        public DateTime? EstimatedDate { get; set; }

        // Eski Zorunlu Alanlar (Geriye Dönük Uyumluluk İçin)
        public string PersonnelName { get; set; } = string.Empty;
        public string PersonnelPhone { get; set; } = string.Empty;
        public string Edas { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PointType { get; set; } = "YG Abonelik";

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

        /// <summary>Cities/Districts referans FK (string City/District alanları geriye dönük uyumluluk için kalır)</summary>
        public Guid? CityId { get; set; }
        public Guid? DistrictId { get; set; }

        public City? CityRef { get; set; }
        public District? DistrictRef { get; set; }
    }
}