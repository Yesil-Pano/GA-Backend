using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Ofis (operasyon) ↔ saha personeli 1:1 konuşması.
    /// Her saha kullanıcısı için tenant içinde tek kayıt.
    /// </summary>
    public class Conversation : BaseEntity, IMultiTenant
    {
        public Guid FieldWorkerUserId { get; set; }
        public DateTime? LastMessageAt { get; set; }

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }

        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public virtual ICollection<ChatReadState> ReadStates { get; set; } = new List<ChatReadState>();
    }
}
