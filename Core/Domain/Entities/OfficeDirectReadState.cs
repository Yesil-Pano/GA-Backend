using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Ofis doğrudan mesajlaşmada kullanıcının en son okuduğu zaman.
    /// </summary>
    public class OfficeDirectReadState : BaseEntity, IMultiTenant
    {
        public Guid OfficeDirectConversationId { get; set; }
        public virtual OfficeDirectConversation Conversation { get; set; } = null!;

        public Guid UserId { get; set; }
        public DateTime LastReadAt { get; set; } = DateTime.UtcNow;

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
    }
}
