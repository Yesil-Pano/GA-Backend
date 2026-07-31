using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class OfficeDirectMessage : BaseEntity, IMultiTenant
    {
        public Guid OfficeDirectConversationId { get; set; }
        public virtual OfficeDirectConversation Conversation { get; set; } = null!;

        public Guid SenderUserId { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>İstemci tarafı idempotent gönderim anahtarı (opsiyonel).</summary>
        public string? ClientMessageId { get; set; }

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}
