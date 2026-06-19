namespace GA.Application.Features.Auth.DTOs
{
    public class RegisterRequest
    {
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string FullName { get; set; }
        public required string PhoneNumber { get; set; }
    }
}
