using GA.Application.Features.Auth.DTOs;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace GA.Application.Features.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IGenericRepository<User> _userRepository;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(
            IGenericRepository<User> userRepository,
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            var existingUsers = await _userRepository.FindAsync(u => u.Email == request.Email);
            if (existingUsers.Any())
                throw new Exception("Bu email adresi zaten kullanımda.");

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

            return await IssueAuthResponseAsync(newUser);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var login = request.Email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(login))
                throw new Exception("Geçersiz e-posta/kullanıcı adı veya şifre.");

            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => !u.IsDeleted &&
                    (u.Email.ToLower() == login.ToLower()
                     || u.Username.ToLower() == login.ToLower()));

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new Exception("Geçersiz e-posta/kullanıcı adı veya şifre.");

            if (!user.IsActive)
                throw new Exception("Hesabınız pasif durumda. Giriş engellendi.");

            await EnsureUserTenantAccessAsync(user);
            return await IssueAuthResponseAsync(user);
        }

        public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                throw new Exception("Yenileme jetonu gerekli.");

            var hash = HashToken(request.RefreshToken.Trim());
            var stored = await _context.RefreshTokens
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r =>
                    r.TokenHash == hash &&
                    !r.IsDeleted &&
                    r.RevokedAt == null);

            if (stored == null || stored.ExpiresAt <= DateTime.UtcNow)
                throw new Exception("Oturum süresi dolmuş. Lütfen tekrar giriş yapın.");

            var user = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == stored.UserId && !u.IsDeleted);

            if (user == null || !user.IsActive)
                throw new Exception("Kullanıcı hesabı bulunamadı veya pasif.");

            await EnsureUserTenantAccessAsync(user);

            stored.RevokedAt = DateTime.UtcNow;
            stored.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await IssueAuthResponseAsync(user);
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return;

            var hash = HashToken(refreshToken.Trim());
            var stored = await _context.RefreshTokens
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.TokenHash == hash && !r.IsDeleted && r.RevokedAt == null);

            if (stored == null) return;

            stored.RevokedAt = DateTime.UtcNow;
            stored.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async Task EnsureUserTenantAccessAsync(User user)
        {
            if (user.TenantId == Guid.Empty) return;

            var tenant = await _context.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId);

            if (tenant == null || tenant.IsDeleted || !tenant.IsActive)
                throw new Exception("Firma hesabı pasif veya bulunamadı. Giriş engellendi.");

            if (tenant.IsDemo
                && tenant.DemoExpiresAt.HasValue
                && tenant.DemoExpiresAt.Value <= DateTime.UtcNow)
                throw new Exception("Demo süreniz dolmuştur. Web ve mobil erişim kapatıldı.");
        }

        private async Task<AuthResponse> IssueAuthResponseAsync(User user)
        {
            var roleNames = await _context.UserRoles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(ur => ur.UserId == user.Id)
                .Join(
                    _context.Roles.IgnoreQueryFilters().AsNoTracking().Where(r => !r.IsDeleted),
                    ur => ur.RoleId,
                    r => r.Id,
                    (_, r) => r.Name)
                .Distinct()
                .ToListAsync();

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var accessMinutes = double.Parse(jwtSettings["ExpiryInMinutes"] ?? "480");
            var refreshDays = int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "365");
            var accessExpires = DateTime.UtcNow.AddMinutes(accessMinutes);
            var refreshExpires = DateTime.UtcNow.AddDays(refreshDays);

            var accessToken = BuildAccessToken(user, roleNames, accessExpires, jwtSettings);
            var refreshPlain = GenerateRefreshTokenValue();
            var refreshHash = HashToken(refreshPlain);

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = refreshHash,
                ExpiresAt = refreshExpires,
            });
            await _context.SaveChangesAsync();

            return new AuthResponse
            {
                Token = accessToken,
                UserId = user.Id.ToString(),
                Username = user.Username,
                FullName = user.FullName,
                Roles = roleNames.ToList(),
                RefreshToken = refreshPlain,
                AccessTokenExpiresAt = accessExpires,
                RefreshTokenExpiresAt = refreshExpires,
            };
        }

        private static string BuildAccessToken(
            User user,
            IReadOnlyList<string> roleNames,
            DateTime expiresAt,
            IConfigurationSection jwtSettings)
        {
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.FullName),
                new Claim("TenantId", user.TenantId.ToString()),
                new Claim("CustomerId", user.CustomerId?.ToString() ?? string.Empty)
            };

            foreach (var role in roleNames)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(secretKey),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }

        private static string GenerateRefreshTokenValue()
        {
            var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }
    }
}
