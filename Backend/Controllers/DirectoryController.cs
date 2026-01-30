using Microsoft.AspNetCore.Mvc;
using CopilotApi.Services;
using CopilotApi.Options;
using Microsoft.Extensions.Options;

namespace CopilotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DirectoryController : ControllerBase
{
    private readonly CopilotService _copilotService;
    private readonly ILogger<DirectoryController> _logger;
    private readonly CopilotCliOptions _cliOptions;

    public DirectoryController(
        CopilotService copilotService, 
        ILogger<DirectoryController> logger,
        IOptions<CopilotCliOptions> cliOptions)
    {
        _copilotService = copilotService;
        _logger = logger;
        _cliOptions = cliOptions.Value;
    }

    [HttpGet]
    public ActionResult<DirectoryListResponse> GetAvailableDirectories()
    {
        try
        {
            var baseDir = _cliOptions.WorkingDirectory;
            if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
            {
                return BadRequest(new { error = "Working directory not configured or does not exist" });
            }

            var fullBaseDir = Path.GetFullPath(baseDir);
            var directories = Directory.GetDirectories(fullBaseDir)
                .Select(d => new DirectoryInfo(d))
                .Where(d => !d.Name.StartsWith("."))
                .Select(d => new DirectoryItem
                {
                    Name = d.Name,
                    FullPath = d.FullName
                })
                .OrderBy(d => d.Name)
                .ToList();

            return Ok(new DirectoryListResponse
            {
                BaseDirectory = fullBaseDir,
                CurrentDirectory = _copilotService.GetCurrentDirectory(),
                Directories = directories
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available directories");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("current")]
    public ActionResult<CurrentDirectoryResponse> GetCurrentDirectory()
    {
        try
        {
            var currentDir = _copilotService.GetCurrentDirectory();
            return Ok(new CurrentDirectoryResponse
            {
                CurrentDirectory = currentDir,
                BaseDirectory = _cliOptions.WorkingDirectory ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current directory");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("switch")]
    public async Task<ActionResult> SwitchDirectory([FromBody] SwitchDirectoryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.DirectoryPath))
            {
                return BadRequest(new { error = "DirectoryPath is required" });
            }

            if (!Directory.Exists(request.DirectoryPath))
            {
                return BadRequest(new { error = "Directory does not exist" });
            }

            var baseDir = _cliOptions.WorkingDirectory;
            if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
            {
                return BadRequest(new { error = "Working directory not configured or does not exist" });
            }

            var fullBaseDir = Path.GetFullPath(baseDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullTarget = Path.GetFullPath(request.DirectoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullTarget.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Directory must be under working directory" });
            }

            await _copilotService.SwitchDirectoryAsync(request.DirectoryPath);
            
            return Ok(new { 
                message = "Directory switched successfully", 
                currentDirectory = request.DirectoryPath 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch directory");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class DirectoryListResponse
{
    public string BaseDirectory { get; set; } = string.Empty;
    public string CurrentDirectory { get; set; } = string.Empty;
    public List<DirectoryItem> Directories { get; set; } = new();
}

public class DirectoryItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
}

public class CurrentDirectoryResponse
{
    public string CurrentDirectory { get; set; } = string.Empty;
    public string BaseDirectory { get; set; } = string.Empty;
}

public class SwitchDirectoryRequest
{
    public string DirectoryPath { get; set; } = string.Empty;
}
