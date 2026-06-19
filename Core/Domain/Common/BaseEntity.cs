namespace GA.Core.Domain.Common
{
    public abstract class BaseEntity
    {
        public Guid Id { get; init; } = Guid.NewGuid(); // PostgreSQL UUID performansı için
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false; // Soft-delete mekanizması için
    }
}
