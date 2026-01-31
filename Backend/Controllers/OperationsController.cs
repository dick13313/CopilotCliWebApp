using CopilotApi.Models;
using CopilotApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CopilotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OperationsController : ControllerBase
{
    private readonly OperationsSupervisor _operations;
    private readonly ILogger<OperationsController> _logger;

    public OperationsController(OperationsSupervisor operations, ILogger<OperationsController> logger)
    {
        _operations = operations;
        _logger = logger;
    }

    [HttpGet("status")]
    public ActionResult<OperationsStatus> GetStatus()
    {
        try
        {
            return Ok(_operations.GetStatus());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get operations status");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("frontend/start")]
    public async Task<ActionResult<OperationsActionResult>> StartFrontend(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _operations.StartFrontendAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start frontend");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("frontend/stop")]
    public async Task<ActionResult<OperationsActionResult>> StopFrontend(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _operations.StopFrontendAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop frontend");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("frontend/restart")]
    public async Task<ActionResult<OperationsActionResult>> RestartFrontend(CancellationToken cancellationToken)
    {
        try
        {
            await _operations.StopFrontendAsync(cancellationToken);
            var startResult = await _operations.StartFrontendAsync(cancellationToken);
            return Ok(new OperationsActionResult
            {
                Action = "restart_frontend",
                Status = startResult.Status,
                Message = startResult.Message,
                Snapshot = startResult.Snapshot
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart frontend");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("diagnostics")]
    public async Task<ActionResult<DiagnosticsResult>> RunDiagnostics(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _operations.RunDiagnosticsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run diagnostics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("heal")]
    public async Task<ActionResult<OperationsActionResult>> Heal([FromServices] CopilotService copilotService)
    {
        try
        {
            await copilotService.ResetClientAsync();
            return Ok(new OperationsActionResult
            {
                Action = "heal",
                Status = "completed",
                Message = "Copilot CLI client reset completed.",
                Snapshot = _operations.GetStatus()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to heal Copilot client");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("logs")]
    public ActionResult<List<OperationLogEntry>> GetLogs([FromQuery] int count = 50)
    {
        try
        {
            var safeCount = Math.Clamp(count, 1, 200);
            return Ok(_operations.GetRecentLogs(safeCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get operations logs");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
