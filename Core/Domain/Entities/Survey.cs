using GA.Core.Domain.Common;

namespace GA.Core.Domain.Entities
{
    public class Survey : BaseEntity
    {
        public required string Title { get; set; }
        public string? Description { get; set; }

        // Anketin yayında olup olmadığını kontrol etmek için
        public bool IsActive { get; set; } = true;

        // Gelecekte eklenecek "Sorular" tablosu ile 1-N ilişki için hazırlık
        // public virtual ICollection<SurveyQuestion> Questions { get; set; } = new List<SurveyQuestion>();
    }
}
