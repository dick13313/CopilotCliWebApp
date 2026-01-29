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
}

public class SessionInfo
{
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public DateTime CreatedAt { get; set; }
}
