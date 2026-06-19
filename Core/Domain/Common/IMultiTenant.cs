namespace GA.Core.Domain.Common
{
    public interface IMultiTenant
    {
        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; } // Opsiyonel, direkt ana firmaya da bağlı olabilir
    }
}
