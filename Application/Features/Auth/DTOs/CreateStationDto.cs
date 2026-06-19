namespace GA.Application.Features.Auth.DTOs
{
    public class CreateStationDto
    {
        public string Name { get; set; } = string.Empty;
        public string StatusType { get; set; } = "Alt Yapı Tamamlandı";
        public string PowerType { get; set; } = "AC";
        public string PersonnelName { get; set; } = string.Empty;
        public string PersonnelPhone { get; set; } = string.Empty;
        public string Edas { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PointType { get; set; } = "YG Abonelik";
        public string City { get; set; } = "Ankara";
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Dosya Yolları (İleride FileService ile diske/S3'e yazılıp adresi buraya setlenecek)
        public string? GeneralFilePath { get; set; }
        public string? YgTescilBelgesiPath { get; set; }
        public string? YgSozlesmesiPath { get; set; }
        public string? SabitFotograflarPath { get; set; }
        public string? YillikBakimFormuPath { get; set; }
        public string? YgIsletmeBelgesiPath { get; set; }
    }
}
