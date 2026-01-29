namespace CopilotApi.Options;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string? BotToken { get; set; }
    public long? AllowedChatId { get; set; }
    public string DefaultModel { get; set; } = "claude-sonnet-4.5";
}
