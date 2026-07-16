using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Kullanıcının bir konuşmada en son okuduğu zaman.
    /// Okunmamış sayısı: SentAt &gt; LastReadAt ve SenderUserId != UserId.
    /// </summary>
    public class ChatReadState : BaseEntity, IMultiTenant
    {
        public Guid ConversationId { get; set; }
        public virtual Conversation Conversation { get; set; } = null!;

        public Guid UserId { get; set; }
        public DateTime LastReadAt { get; set; } = DateTime.UtcNow;

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}
