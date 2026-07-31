using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Ofis kullanıcıları arası 1:1 doğrudan mesajlaşma.
    /// UserOneId &lt; UserTwoId kanonik sıralama ile tenant içinde tek kayıt.
    /// </summary>
    public class OfficeDirectConversation : BaseEntity, IMultiTenant
    {
        public Guid UserOneId { get; set; }
        public Guid UserTwoId { get; set; }
        public DateTime? LastMessageAt { get; set; }

        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }

        public virtual ICollection<OfficeDirectMessage> Messages { get; set; } = new List<OfficeDirectMessage>();
        public virtual ICollection<OfficeDirectReadState> ReadStates { get; set; } = new List<OfficeDirectReadState>();
    }

}
