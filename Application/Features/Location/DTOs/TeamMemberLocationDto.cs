namespace GA.Application.Features.Location.DTOs
{
    public class TeamMemberLocationDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
