using GA.Application.Features.Notifications;
using GA.Application.Features.OfficeChat;
using GA.Application.Features.OfficeChat.DTOs;
using GA.Core.Interfaces;
using GA.Infrastructure.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace GA.Presentation.Controllers
{
    [Route("api/office-chat")]
    [ApiController]
    [Authorize]
    public class OfficeChatController : ControllerBase
    {
        private readonly IOfficeChatService _officeChatService;
        private readonly IHubContext<ChatHub> _hub;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ICurrentUserService _currentUserService;

        public OfficeChatController(
            IOfficeChatService officeChatService,
            IHubContext<ChatHub> hub,
            IPushNotificationService pushNotificationService,
            ICurrentUserService currentUserService)
        {
            _officeChatService = officeChatService;
            _hub = hub;
            _pushNotificationService = pushNotificationService;
            _currentUserService = currentUserService;
        }

        [HttpGet("contacts")]
        public async Task<IActionResult> ListContacts(
            [FromQuery] string? partnerKey,
            CancellationToken ct = default)
        {
            try
            {
                var data = await _officeChatService.ListContactsAsync(partnerKey, ct);
                return Ok(data);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> ListConversations(
            [FromQuery] string? partnerKey,
            CancellationToken ct = default)
        {
            try
            {
                var data = await _officeChatService.ListContactsAsync(partnerKey, ct);
                return Ok(data);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount(CancellationToken ct = default)
        {
            try
            {
                var count = await _officeChatService.GetUnreadTotalAsync(ct);
                return Ok(new { count });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("conversations/{id:guid}/messages")]
        public async Task<IActionResult> GetMessages(
            Guid id,
            [FromQuery] DateTime? before,
            [FromQuery] int take = 50,
            CancellationToken ct = default)
        {
            try
            {
                var data = await _officeChatService.GetMessagesAsync(id, before, take, ct);
                return Ok(data);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("conversations/start")]
        public async Task<IActionResult> StartConversation(
            [FromBody] StartOfficeConversationRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var data = await _officeChatService.StartConversationAsync(request.TargetUserId, ct);
                return Ok(data);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("conversations/{id:guid}/messages")]
        public async Task<IActionResult> SendMessage(
            Guid id,
            [FromBody] SendOfficeMessageRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var (senderDto, recipientDto, recipientUserId) = await _officeChatService.SendMessageAsync(id, request, ct);

                var preview = senderDto.Body.Length > 80 ? senderDto.Body[..80] + "…" : senderDto.Body;
                var senderUserId = _currentUserService.UserId;

                await _hub.Clients.Group($"user-{senderUserId}")
                    .SendAsync("DirectMessageCreated", senderDto, ct);
                if (recipientUserId != Guid.Empty && recipientUserId != senderUserId)
                {
                    await _hub.Clients.Group($"user-{recipientUserId}")
                        .SendAsync("DirectMessageCreated", recipientDto, ct);
                }

                await _hub.Clients.Group($"user-{recipientUserId}")
                    .SendAsync("DirectConversationUpdated", new
                    {
                        conversationId = senderDto.ConversationId,
                        lastMessageAt = senderDto.SentAt,
                        lastMessagePreview = preview,
                        otherUserId = senderUserId,
                    }, ct);
                await _hub.Clients.Group($"user-{senderUserId}")
                    .SendAsync("DirectConversationUpdated", new
                    {
                        conversationId = senderDto.ConversationId,
                        lastMessageAt = senderDto.SentAt,
                        lastMessagePreview = preview,
                        otherUserId = recipientUserId,
                    }, ct);

                if (recipientUserId != Guid.Empty &&
                    recipientUserId != senderUserId)
                {
                    var pushBody = senderDto.Body.Length > 120 ? senderDto.Body[..120] + "…" : senderDto.Body;
                    await _pushNotificationService.SendToUserAsync(
                        recipientUserId,
                        senderDto.SenderName,
                        pushBody,
                        new Dictionary<string, object>
                        {
                            ["type"] = "DirectChatMessage",
                            ["conversationId"] = senderDto.ConversationId.ToString(),
                            ["senderUserId"] = senderDto.SenderUserId.ToString(),
                        },
                        ct);
                }

                return Ok(senderDto);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("conversations/{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
        {
            try
            {
                var (conversationId, userId, lastReadAt) = await _officeChatService.MarkReadAsync(id, ct);
                await _hub.Clients.Group($"conversation-{conversationId}")
                    .SendAsync("DirectMessagesRead", new { conversationId, userId, lastReadAt }, ct);
                return Ok(new { message = "Okundu." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
