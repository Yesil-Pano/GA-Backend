using GA.Core.Interfaces;
using System.Security.Claims;

namespace GA.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Token içindeki TenantId'yi okur, yoksa boş guid döner (Örn: Register anında)
        public Guid TenantId =>
            Guid.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirstValue("TenantId"), out var tenantId)
                ? tenantId
                : Guid.Empty;

        public Guid? CustomerId =>
            Guid.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirstValue("CustomerId"), out var customerId)
                ? customerId
                : null;

        public Guid UserId =>
            Guid.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : Guid.Empty;
    }
}
