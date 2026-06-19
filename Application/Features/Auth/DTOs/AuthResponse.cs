namespace GA.Application.Features.Auth.DTOs
{
    public class AuthResponse
    {
        public required string Token { get; set; }
        public required string Username { get; set; }
        public required string FullName { get; set; }
        // İleride menüyü dinamik çizdirmek için rolleri de buraya ekleyeceğiz
        public List<string> Roles { get; set; } = new();
    }
}
