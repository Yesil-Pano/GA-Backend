namespace GA.Application.Features.Surveys.DTOs
{
    public class UpdateTeamLocationRequest
    {
        public Guid TeamUserId { get; set; } // Konumu güncelleyen personelin ID'si
        public double Latitude { get; set; } // Gelen enlem verisi
        public double Longitude { get; set; } // Gelen boylam verisi
    }
}
