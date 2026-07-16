using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class ChatMessage : BaseEntity, IMultiTenant
    {
        public Guid ConversationId { get; set; }
        public virtual Conversation Conversation { get; set; } = null!;

        public Guid SenderUserId { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>İstemci tarafı idempotent gönderim anahtarı (opsiyonel).</summary>
        public string? ClientMessageId { get; set; }

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}
