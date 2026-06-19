namespace GA.Application.Features.Auth.DTOs
{
    public class CreateWorkOrderDto
    {
        public string Title { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MobileDescription { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Priority { get; set; } = "Orta";
        public string WorkType { get; set; } = "Arıza";
        public string WorkCategory { get; set; } = "Arıza Bildirimi";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Formdan seçilen personellerin ID'leri
        public Guid? OperationUserId { get; set; }
        public Guid? OpenedByUserId { get; set; }
        public Guid? AssignedToUserId { get; set; }
    }
}
