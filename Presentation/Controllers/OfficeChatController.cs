using GA.Application.Features.OfficeChat;
using GA.Application.Features.OfficeChat.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GA.Presentation.Controllers
{
    [Route("api/office-chat")]
    [ApiController]
    [Authorize]
    public class OfficeChatController : ControllerBase
    {
        private readonly IOfficeChatService _officeChatService;

        public OfficeChatController(IOfficeChatService officeChatService)
        {
            _officeChatService = officeChatService;
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> ListConversations(CancellationToken ct = default)
        {
            try
            {
                var data = await _officeChatService.ListConversationsAsync(ct);
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
                var data = await _officeChatService.SendMessageAsync(id, request, ct);
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

        [HttpPost("conversations/{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
        {
            try
            {
                await _officeChatService.MarkReadAsync(id, ct);
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
