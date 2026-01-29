using Microsoft.AspNetCore.Mvc;
using CopilotApi.Models;
using CopilotApi.Services;

namespace CopilotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly CopilotService _copilotService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(CopilotService copilotService, ILogger<ChatController> logger)
    {
        _copilotService = copilotService;
        _logger = logger;
    }

    [HttpPost("session")]
    public async Task<ActionResult<SessionInfo>> CreateSession([FromBody] ChatRequest request)
    {
        try
        {
            var sessionId = await _copilotService.CreateSessionAsync(request.Model ?? "claude-sonnet-4.5");
            return Ok(new SessionInfo
            {
                SessionId = sessionId,
                Model = request.Model ?? "claude-sonnet-4.5",
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.SessionId))
            {
                return BadRequest(new { error = "SessionId is required" });
            }

            if (string.IsNullOrEmpty(request.Prompt))
            {
                return BadRequest(new { error = "Prompt is required" });
            }

            var messages = await _copilotService.SendMessageAsync(request.SessionId, request.Prompt);
            
            var lastMessage = messages.LastOrDefault();
            return Ok(new ChatResponse
            {
                SessionId = request.SessionId,
                Content = lastMessage?.Content ?? "",
                IsComplete = true
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("sessions")]
    public ActionResult<List<string>> GetSessions()
    {
        var sessions = _copilotService.GetActiveSessions();
        return Ok(sessions);
    }

    [HttpDelete("session/{sessionId}")]
    public async Task<ActionResult> DeleteSession(string sessionId)
    {
        try
        {
            await _copilotService.DeleteSessionAsync(sessionId);
            return Ok(new { message = "Session deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
