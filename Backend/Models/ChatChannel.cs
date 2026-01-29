namespace CopilotApi.Models;

public class ChatChannelInfo
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Status { get; set; } = "unknown";
}

public class ChatChannelConfig
{
    public string Channel { get; set; } = string.Empty;
    public Dictionary<string, string> Settings { get; set; } = new();
}
