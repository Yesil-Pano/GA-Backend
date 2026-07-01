namespace GA.Application.Features.Auth.DTOs
{
    public class UpdateTeamDto
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string TeamLeader { get; set; } = string.Empty;
        public List<Guid> ProjectIds { get; set; } = new List<Guid>();
        // 🚀 DTO HARİTA EKLENTİSİ
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
