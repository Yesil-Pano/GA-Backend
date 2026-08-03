namespace GA.Application.Features.Auth.DTOs
{
    /// <summary>E-posta veya kullanıcı adı ile giriş. JSON alan adı geriye dönük uyumluluk için "email" kalır.</summary>
    public class LoginRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}
