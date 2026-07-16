using GA.Application.Features.Chat;
using GA.Application.Features.Chat.DTOs;
using GA.Infrastructure.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace GA.Presentation.Controllers
{
    [Route("api/chat")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IHubContext<ChatHub> _hub;

        public ChatController(IChatService chatService, IHubContext<ChatHub> hub)
        {
            _chatService = chatService;
            _hub = hub;
        }

        /// <summary>Mobil: kendi Operasyon konuşması + son mesajlar.</summary>
        [HttpGet("conversation")]
        public async Task<IActionResult> GetMyConversation([FromQuery] int take = 50, CancellationToken ct = default)
        {
            try
            {
                var data = await _chatService.GetMyConversationAsync(take, ct);
                return Ok(data);
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

        /// <summary>Web: tenant / partner içi saha personeli konuşma listesi.</summary>
        [HttpGet("conversations")]
        public async Task<IActionResult> ListConversations(
            [FromQuery] string? partnerKey, CancellationToken ct = default)
        {
            try
            {
                var data = await _chatService.ListConversationsAsync(partnerKey, ct);
                return Ok(data);
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

        [HttpGet("conversations/{id:guid}/messages")]
        public async Task<IActionResult> GetMessages(
            Guid id,
            [FromQuery] DateTime? before,
            [FromQuery] int take = 50,
            CancellationToken ct = default)
        {
            try
            {
                var data = await _chatService.GetMessagesAsync(id, before, take, ct);
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

        /// <summary>Mobil: conversationId opsiyonel (kendi kanalı). Web: zorunlu.</summary>
        [HttpPost("conversations/{id:guid}/messages")]
        public async Task<IActionResult> SendToConversation(
            Guid id, [FromBody] SendMessageRequest request, CancellationToken ct = default)
        {
            return await SendInternal(id, request, ct);
        }

        /// <summary>Mobil kısayol: kendi konuşmasına gönder.</summary>
        [HttpPost("messages")]
        public async Task<IActionResult> SendMyMessage(
            [FromBody] SendMessageRequest request, CancellationToken ct = default)
        {
            return await SendInternal(null, request, ct);
        }

        [HttpPut("conversations/{id:guid}/read")]
        [HttpPost("conversations/{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
        {
            try
            {
                var (conversationId, userId, lastReadAt) = await _chatService.MarkReadAsync(id, ct);
                await _hub.Clients.Group($"conversation-{conversationId}")
                    .SendAsync("MessagesRead", new { conversationId, userId, lastReadAt }, ct);
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

        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount(CancellationToken ct = default)
        {
            try
            {
                var count = await _chatService.GetMyUnreadTotalAsync(ct);
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

        private async Task<IActionResult> SendInternal(
            Guid? conversationId, SendMessageRequest request, CancellationToken ct)
        {
            try
            {
                var (dto, tenantId, fieldWorkerUserId) =
                    await _chatService.SendMessageAsync(conversationId, request, ct);

                await _hub.Clients.Group($"conversation-{dto.ConversationId}")
                    .SendAsync("MessageCreated", dto, ct);

                // Tenant / SuperAdmin gruplarına da yayınla (JoinConversation yarışına karşı yedek)
                await _hub.Clients.Group($"tenant-chat-{tenantId}")
                    .SendAsync("MessageCreated", dto, ct);
                await _hub.Clients.Group("tenant-chat-superadmin")
                    .SendAsync("MessageCreated", dto, ct);

                var preview = dto.Body.Length > 80 ? dto.Body[..80] + "…" : dto.Body;
                var updatedPayload = new
                {
                    conversationId = dto.ConversationId,
                    lastMessageAt = dto.SentAt,
                    lastMessagePreview = preview,
                    fieldWorkerUserId,
                };
                await _hub.Clients.Group($"tenant-chat-{tenantId}")
                    .SendAsync("ConversationUpdated", updatedPayload, ct);
                await _hub.Clients.Group("tenant-chat-superadmin")
                    .SendAsync("ConversationUpdated", updatedPayload, ct);

                return Ok(dto);
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
    }
}
