namespace GA.Application.Features.Auth.DTOs
{
    public class UpdateStationDto
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
        public string? District { get; set; }
        public string? OwnerCompany { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
