using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GA.Application.Features.Notifications
{
    public interface IPushNotificationService
    {
        Task SendToUserAsync(
            Guid userId,
            string title,
            string body,
            IDictionary<string, object>? data = null,
            CancellationToken cancellationToken = default);
    }

    public class ExpoPushNotificationService : IPushNotificationService
    {
        private static readonly Uri ExpoPushUri = new("https://exp.host/--/api/v2/push/send");
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ExpoPushNotificationService> _logger;

        public ExpoPushNotificationService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<ExpoPushNotificationService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task SendToUserAsync(
            Guid userId,
            string title,
            string body,
            IDictionary<string, object>? data = null,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty) return;

            var tokens = await _context.UserPushTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == userId && t.IsActive && !t.IsDeleted)
                .Select(t => t.Token)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (tokens.Count == 0)
            {
                _logger.LogInformation("Push atlandı: kullanıcı {UserId} için token yok.", userId);
                return;
            }

            var messages = tokens.Select(token => new ExpoPushMessage
            {
                To = token,
                Title = title,
                Body = body,
                Sound = "default",
                Priority = "high",
                ChannelId = "default",
                Data = data,
            }).ToList();

            try
            {
                var client = _httpClientFactory.CreateClient("expo-push");
                using var response = await client.PostAsJsonAsync(ExpoPushUri, messages, cancellationToken);
                var raw = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Expo push HTTP {Status}: {Body}", (int)response.StatusCode, raw);
                    return;
                }

                _logger.LogInformation("Expo push gönderildi. User={UserId}, Count={Count}", userId, tokens.Count);

                // DeviceNotRegistered tokenlarını pasife al
                if (raw.Contains("DeviceNotRegistered", StringComparison.OrdinalIgnoreCase))
                {
                    var stale = await _context.UserPushTokens
                        .IgnoreQueryFilters()
                        .Where(t => t.UserId == userId && t.IsActive && !t.IsDeleted)
                        .ToListAsync(cancellationToken);

                    foreach (var t in stale)
                    {
                        t.IsActive = false;
                        t.UpdatedAt = DateTime.UtcNow;
                    }
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expo push gönderilemedi. User={UserId}", userId);
            }
        }

        private sealed class ExpoPushMessage
        {
            [JsonPropertyName("to")]
            public string To { get; set; } = string.Empty;

            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("body")]
            public string Body { get; set; } = string.Empty;

            [JsonPropertyName("sound")]
            public string Sound { get; set; } = "default";

            [JsonPropertyName("priority")]
            public string Priority { get; set; } = "high";

            [JsonPropertyName("channelId")]
            public string? ChannelId { get; set; }

            [JsonPropertyName("data")]
            public IDictionary<string, object>? Data { get; set; }
        }
    }
}
