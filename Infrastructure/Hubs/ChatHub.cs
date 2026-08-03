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
            var userId = _currentUserService.UserId;
            if (userId != Guid.Empty)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

            var tenantId = _currentUserService.TenantId;
            if (tenantId != Guid.Empty)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-chat-{tenantId}");
            else
                await Groups.AddToGroupAsync(Context.ConnectionId, "tenant-chat-superadmin");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = _currentUserService.UserId;
            if (userId != Guid.Empty)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");

            var tenantId = _currentUserService.TenantId;
            if (tenantId != Guid.Empty)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant-chat-{tenantId}");
            else
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "tenant-chat-superadmin");

            await base.OnDisconnectedAsync(exception);
        }

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

        public async Task SendTyping(string conversationId)
        {
            if (!Guid.TryParse(conversationId, out _)) return;
            var userId = _currentUserService.UserId;
            if (userId == Guid.Empty) return;

            await Clients.OthersInGroup($"conversation-{conversationId}")
                .SendAsync("DirectTyping", new { conversationId, userId });
        }
    }
}
