using GA.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GA.Infrastructure.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ICurrentUserService _currentUserService;

        public ChatHub(ICurrentUserService currentUserService)
        {
            _currentUserService = currentUserService;
        }

        public override async Task OnConnectedAsync()
        {
            var tenantId = _currentUserService.TenantId;
            // SuperAdmin TenantId Empty — yine de sohbet gruplarına JoinConversation ile girer
            if (tenantId != Guid.Empty)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-chat-{tenantId}");
            else
                await Groups.AddToGroupAsync(Context.ConnectionId, "tenant-chat-superadmin");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId != Guid.Empty)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant-chat-{tenantId}");
            else
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "tenant-chat-superadmin");

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>Konuşma odasına katıl (mesaj dinlemek için).</summary>
        public async Task JoinConversation(string conversationId)
        {
            if (Guid.TryParse(conversationId, out _))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
        }

        public async Task LeaveConversation(string conversationId)
        {
            if (Guid.TryParse(conversationId, out _))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
        }
    }
}
