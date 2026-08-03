namespace GA.Application.Features.Auth.DTOs
{
    public class AuthResponse
    {
        public required string Token { get; set; }
        public required string UserId { get; set; }
        public required string Username { get; set; }
        public required string FullName { get; set; }
        public List<string> Roles { get; set; } = new();
        public string? RefreshToken { get; set; }
        public DateTime? AccessTokenExpiresAt { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
    }
}
