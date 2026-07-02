using GA.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GA.Infrastructure.Hubs
{
    [Authorize]
    public class LocationHub : Hub
    {
        private readonly ICurrentUserService _currentUserService;

        public LocationHub(ICurrentUserService currentUserService)
        {
            _currentUserService = currentUserService;
        }

        public override async Task OnConnectedAsync()
        {
            var tenantId = _currentUserService.TenantId;
            // Her tenant kendi grubuna katılır — izolasyon sağlanır
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var tenantId = _currentUserService.TenantId;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
