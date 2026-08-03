using GA.Application.Features.Notifications;
using GA.Application.Features.OfficeChat.DTOs;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GA.Presentation.Controllers
{
    [Route("api/devices")]
    [ApiController]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IConfiguration _configuration;

        public DevicesController(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IConfiguration configuration)
        {
            _context = context;
            _currentUserService = currentUserService;
            _configuration = configuration;
        }

        public class RegisterPushTokenDto
        {
            public string Token { get; set; } = string.Empty;
            public string? Platform { get; set; }
            public string? DeviceName { get; set; }
        }

        /// <summary>POST /api/devices/push-token — mobil Expo push token kaydı</summary>
        [HttpPost("push-token")]
        public async Task<IActionResult> RegisterPushToken([FromBody] RegisterPushTokenDto dto)
        {
            var userId = _currentUserService.UserId;
            if (userId == Guid.Empty)
                return Unauthorized(new { message = "Oturum gerekli." });

            var token = (dto.Token ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
                return BadRequest(new { message = "Geçersiz push token." });

            var existing = await _context.UserPushTokens
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Token == token && !t.IsDeleted);

            if (existing != null)
            {
                existing.UserId = userId;
                existing.Platform = dto.Platform;
                existing.DeviceName = dto.DeviceName;
                existing.IsActive = true;
                existing.LastSeenAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.UserPushTokens.Add(new UserPushToken
                {
                    UserId = userId,
                    Token = token,
                    Platform = dto.Platform,
                    DeviceName = dto.DeviceName,
                    IsActive = true,
                    LastSeenAt = DateTime.UtcNow,
                });
            }

            // Aynı kullanıcının diğer eski tokenlarını koru (çok cihaz), sadece bu token aktif
            await _context.SaveChangesAsync();
            return Ok(new { message = "Push token kaydedildi." });
        }

        /// <summary>DELETE /api/devices/push-token — çıkışta token pasifle</summary>
        [HttpDelete("push-token")]
        public async Task<IActionResult> UnregisterPushToken([FromBody] RegisterPushTokenDto dto)
        {
            var userId = _currentUserService.UserId;
            var token = (dto.Token ?? string.Empty).Trim();
            if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
                return Ok();

            var rows = await _context.UserPushTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == userId && t.Token == token && !t.IsDeleted)
                .ToListAsync();

            foreach (var row in rows)
            {
                row.IsActive = false;
                row.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Push token kaldırıldı." });
        }

        [HttpGet("web-push/vapid-public-key")]
        public IActionResult GetWebPushPublicKey()
        {
            var key = WebPushVapidStore.GetPublicKey(_configuration);
            return Ok(new WebPushVapidPublicKeyResponse { PublicKey = key });
        }

        [HttpPost("web-push-subscription")]
        public async Task<IActionResult> RegisterWebPushSubscription(
            [FromBody] RegisterWebPushSubscriptionRequest dto)
        {
            var userId = _currentUserService.UserId;
            if (userId == Guid.Empty)
                return Unauthorized(new { message = "Oturum gerekli." });

            var endpoint = (dto.Endpoint ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(endpoint))
                return BadRequest(new { message = "Geçersiz abonelik." });

            var existing = await _context.UserWebPushSubscriptions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Endpoint == endpoint && !s.IsDeleted);

            if (existing != null)
            {
                existing.UserId = userId;
                existing.P256dh = dto.P256dh;
                existing.Auth = dto.Auth;
                existing.IsActive = true;
                existing.LastSeenAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.UserWebPushSubscriptions.Add(new UserWebPushSubscription
                {
                    UserId = userId,
                    Endpoint = endpoint,
                    P256dh = dto.P256dh,
                    Auth = dto.Auth,
                    IsActive = true,
                    LastSeenAt = DateTime.UtcNow,
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Web push aboneliği kaydedildi." });
        }
    }
}
