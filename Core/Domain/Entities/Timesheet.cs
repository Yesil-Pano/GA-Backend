using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class Timesheet : BaseEntity
    {
        // Hangi personele ait olduğu (1-N İlişki)
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public DateTime ShiftDate { get; set; } // Mesai günü

        // Başlangıç ve bitiş saatleri
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public string? Notes { get; set; }
        public bool IsApproved { get; set; } = false; // Yöneticisi onayladı mı?
    }
}
