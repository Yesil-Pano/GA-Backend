using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Mobil cihaz Expo Push Token kaydı (ekran kapalıyken bildirim için).
    /// </summary>
    public class UserPushToken : BaseEntity
    {
        public Guid UserId { get; set; }
        /// <summary>ExponentPushToken[...]</summary>
        public string Token { get; set; } = string.Empty;
        public string? Platform { get; set; }
        public string? DeviceName { get; set; }
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
