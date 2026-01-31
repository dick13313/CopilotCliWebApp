namespace CopilotApi.Models;

public class OperationsStatus
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ProcessStatus Frontend { get; set; } = new();
}

public class ProcessStatus
{
    public string Name { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int? Pid { get; set; }
    public int? Port { get; set; }
    public bool? PortOpen { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ExitedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? LastOutput { get; set; }
    public string? LastError { get; set; }
    public List<OperationLogEntry> RecentLogs { get; set; } = new();
}

public class OperationLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class OperationsActionResult
{
    public string Action { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public OperationsStatus? Snapshot { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class DiagnosticsResult
{
    public DateTime RanAt { get; set; } = DateTime.UtcNow;
    public List<DiagnosticCheckResult> Checks { get; set; } = new();
}

public class DiagnosticCheckResult
{
    public string Command { get; set; } = string.Empty;
    public int? ExitCode { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public bool TimedOut { get; set; }
}
