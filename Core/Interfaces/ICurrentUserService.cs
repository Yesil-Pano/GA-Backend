namespace GA.Core.Interfaces
{
    public interface ICurrentUserService
    {
        Guid TenantId { get; }
        Guid? CustomerId { get; }
        Guid UserId { get; }
    }
}
