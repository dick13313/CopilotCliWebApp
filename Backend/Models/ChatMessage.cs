namespace CopilotApi.Models;

public class ChatMessage
{
    public string? Role { get; set; }
    public string? Content { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class ChatRequest
{
    public string? Prompt { get; set; }
    public string? SessionId { get; set; }
    public string? Model { get; set; } = "claude-sonnet-4.5";
}

public class ChatResponse
{
    public string? SessionId { get; set; }
    public string? Content { get; set; }
    public bool IsComplete { get; set; }
    public string? Status { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

public class SessionInfo
{
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Status { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

public class SessionStatusInfo
{
    public string SessionId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = "idle";
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public string? LastPrompt { get; set; }
    public string? LastResponse { get; set; }
    public string? LastResponsePreview { get; set; }
    public string? LastError { get; set; }
}

public class BatchTaskRequest
{
    public List<string> SessionIds { get; set; } = new();
    public string? Prompt { get; set; }
}

public class SessionTaskResult
{
    public string SessionId { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Content { get; set; }
    public string? Error { get; set; }
}
