using System.Text.Json;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;

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
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExpoPushNotificationService> _logger;

        public ExpoPushNotificationService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ExpoPushNotificationService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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

            await SendExpoPushAsync(userId, title, body, data, cancellationToken);
            await SendWebPushAsync(userId, title, body, data, cancellationToken);
        }

        private async Task SendExpoPushAsync(
            Guid userId,
            string title,
            string body,
            IDictionary<string, object>? data,
            CancellationToken cancellationToken)
        {
            var tokens = await _context.UserPushTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == userId && t.IsActive && !t.IsDeleted)
                .Select(t => t.Token)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (tokens.Count == 0) return;

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

        private async Task SendWebPushAsync(
            Guid userId,
            string title,
            string body,
            IDictionary<string, object>? data,
            CancellationToken cancellationToken)
        {
            var vapid = WebPushVapidStore.GetOrCreate(_configuration);

            var subs = await _context.UserWebPushSubscriptions
                .IgnoreQueryFilters()
                .Where(s => s.UserId == userId && s.IsActive && !s.IsDeleted)
                .ToListAsync(cancellationToken);

            if (subs.Count == 0) return;

            var payload = JsonSerializer.Serialize(new
            {
                title,
                body,
                data = data ?? new Dictionary<string, object>(),
            });

            var client = new WebPushClient();

            foreach (var sub in subs)
            {
                try
                {
                    var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await client.SendNotificationAsync(pushSub, payload, vapid, cancellationToken);
                }
                catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                                                  ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    sub.IsActive = false;
                    sub.UpdatedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Web push gönderilemedi. User={UserId}", userId);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private sealed class ExpoPushMessage
        {
            [System.Text.Json.Serialization.JsonPropertyName("to")]
            public string To { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("body")]
            public string Body { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("sound")]
            public string Sound { get; set; } = "default";

            [System.Text.Json.Serialization.JsonPropertyName("priority")]
            public string Priority { get; set; } = "high";

            [System.Text.Json.Serialization.JsonPropertyName("channelId")]
            public string? ChannelId { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("data")]
            public IDictionary<string, object>? Data { get; set; }
        }
    }
}
