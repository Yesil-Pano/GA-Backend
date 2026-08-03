using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Uzun ömürlü oturum yenileme jetonu (mobil / web sessiz yenileme).
    /// </summary>
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }
        /// <summary>SHA-256 hash of the opaque client token.</summary>
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public virtual User? User { get; set; }
    }
}
