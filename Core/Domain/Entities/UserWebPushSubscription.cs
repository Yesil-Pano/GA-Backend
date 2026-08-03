using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    /// <summary>Web tarayıcı Web Push aboneliği.</summary>
    public class UserWebPushSubscription : BaseEntity
    {
        public Guid UserId { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
