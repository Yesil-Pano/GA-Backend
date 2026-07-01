namespace GA.Application.Features.Auth.DTOs
{
    public class UpdateTeamLocationDto
    {
        public Guid TeamUserId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
