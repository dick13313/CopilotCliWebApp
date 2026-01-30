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
            var status = _copilotService.GetSessionStatus(sessionId);
            return Ok(new SessionInfo
            {
                SessionId = sessionId,
                Model = request.Model ?? "claude-sonnet-4.5",
                CreatedAt = status?.CreatedAt ?? DateTime.UtcNow,
                Status = status?.Status,
                LastUpdatedAt = status?.LastUpdatedAt
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
            var status = _copilotService.GetSessionStatus(request.SessionId);
            return Ok(new ChatResponse
            {
                SessionId = request.SessionId,
                Content = lastMessage?.Content ?? "",
                IsComplete = true,
                Status = status?.Status,
                LastUpdatedAt = status?.LastUpdatedAt
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

    [HttpPost("model")]
    public async Task<ActionResult> SwitchModel([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.SessionId))
            {
                return BadRequest(new { error = "SessionId is required" });
            }

            if (string.IsNullOrEmpty(request.Model))
            {
                return BadRequest(new { error = "Model is required" });
            }

            await _copilotService.UpdateSessionModelAsync(request.SessionId, request.Model);
            return Ok(new { message = "Model switched successfully", model = request.Model });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch model");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("sessions")]
    public ActionResult<List<string>> GetSessions()
    {
        var sessions = _copilotService.GetActiveSessions();
        return Ok(sessions);
    }

    [HttpGet("sessions/status")]
    public ActionResult<List<SessionStatusInfo>> GetSessionStatuses()
    {
        return Ok(_copilotService.GetSessionStatuses());
    }

    [HttpGet("session/{sessionId}")]
    public ActionResult<SessionStatusInfo> GetSessionStatus(string sessionId)
    {
        var status = _copilotService.GetSessionStatus(sessionId);
        if (status == null)
        {
            return NotFound(new { error = "Session not found" });
        }

        return Ok(status);
    }

    [HttpPost("batch")]
    public async Task<ActionResult<List<SessionTaskResult>>> SendBatch([FromBody] BatchTaskRequest request)
    {
        try
        {
            if (request.SessionIds == null || request.SessionIds.Count == 0)
            {
                return BadRequest(new { error = "SessionIds is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest(new { error = "Prompt is required" });
            }

            var results = await _copilotService.SendBatchAsync(request.SessionIds, request.Prompt);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send batch message");
            return StatusCode(500, new { error = ex.Message });
        }
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
