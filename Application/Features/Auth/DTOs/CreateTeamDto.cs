namespace GA.Application.Features.Auth.DTOs
{
    public class CreateTeamDto
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string TeamLeader { get; set; } = string.Empty;
        public List<Guid> ProjectIds { get; set; } = new List<Guid>();
        public Guid? TenantId { get; set; }
        // 🚀 DTO HARİTA EKLENTİSİ
        public double Latitude { get; set; } = 39.92077;
        public double Longitude { get; set; } = 32.85411;
    }
}
