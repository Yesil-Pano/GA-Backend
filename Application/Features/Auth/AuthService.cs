using GA.Application.Features.Auth.DTOs;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GA.Application.Features.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IGenericRepository<User> _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(IGenericRepository<User> userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            // Email kontrolü
            var existingUsers = await _userRepository.FindAsync(u => u.Email == request.Email);
            if (existingUsers.Any())
                throw new Exception("Bu email adresi zaten kullanımda.");

            // Şifreyi BCrypt ile güvenli hale getiriyoruz
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var newUser = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = passwordHash,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber
            };

            await _userRepository.AddAsync(newUser);
            await _userRepository.SaveChangesAsync();

            // Kayıt sonrası direkt login olmuş gibi token dönüyoruz
            return GenerateTokenResponse(newUser);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var users = await _userRepository.FindAsync(u => u.Email == request.Email);
            var user = users.FirstOrDefault();

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new Exception("Geçersiz email veya şifre.");

            return GenerateTokenResponse(user);
        }

        private AuthResponse GenerateTokenResponse(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

            // Token içerisine gömeceğimiz kullanıcı bilgileri (Claims)
            var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim("TenantId", user.TenantId.ToString()),
            new Claim("CustomerId", user.CustomerId?.ToString() ?? string.Empty)
        };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpiryInMinutes"]!)),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return new AuthResponse
            {
                Token = tokenHandler.WriteToken(token),
                Username = user.Username,
                FullName = user.FullName
            };
        }
    }
}
