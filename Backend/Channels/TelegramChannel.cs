using CopilotApi.Options;
using CopilotApi.Services;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.IO;
using System.Text.Json.Serialization;

namespace CopilotApi.Channels;

public class TelegramChannel : IChatChannel
{
    private readonly ILogger<TelegramChannel> _logger;
    private readonly CopilotService _copilotService;
    private readonly TelegramOptions _options;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<long, string> _chatSessions = new();
    private CancellationTokenSource? _cts;
    private long _lastUpdateId;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name => "telegram";

    public TelegramChannel(
        ILogger<TelegramChannel> logger,
        CopilotService copilotService,
        IOptions<TelegramOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _copilotService = copilotService;
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _logger.LogWarning("Telegram BotToken not configured, skipping Telegram channel");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => PollUpdatesAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("Telegram channel started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        _logger.LogInformation("Telegram channel stopped");
        return Task.CompletedTask;
    }

    private async Task PollUpdatesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var updates = await GetUpdatesAsync(cancellationToken);
                foreach (var update in updates)
                {
                    _lastUpdateId = Math.Max(_lastUpdateId, update.UpdateId + 1);
                    await HandleUpdateAsync(update, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling Telegram updates");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task<List<TelegramUpdate>> GetUpdatesAsync(CancellationToken cancellationToken)
    {
        var url = $"https://api.telegram.org/bot{_options.BotToken}/getUpdates?offset={_lastUpdateId}&timeout=30";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<TelegramResponse<List<TelegramUpdate>>>(json, JsonOptions);
        return result?.Result ?? new List<TelegramUpdate>();
    }

    private async Task HandleUpdateAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (update.Message == null)
        {
            return;
        }

        var chatId = update.Message.Chat.Id;
        if (_options.AllowedChatId.HasValue && _options.AllowedChatId.Value != chatId)
        {
            _logger.LogWarning("Telegram message from unauthorized chat: {ChatId}", chatId);
            return;
        }

        var prompt = update.Message.Text?.Trim();
        var caption = update.Message.Caption?.Trim();
        var hasPhoto = update.Message.Photo != null && update.Message.Photo.Count > 0;

        if (string.IsNullOrWhiteSpace(prompt) && !hasPhoto)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/model", StringComparison.OrdinalIgnoreCase))
        {
            var reply = await HandleModelSwitchAsync(chatId, prompt);
            await SendMessageAsync(chatId, reply, cancellationToken);
            return;
        }

        var sessionId = await GetOrCreateSessionAsync(chatId);
        _logger.LogInformation("Telegram message received from {ChatId} in session {SessionId}", chatId, sessionId);

        var finalPrompt = prompt;
        if (hasPhoto && string.IsNullOrWhiteSpace(finalPrompt))
        {
            finalPrompt = string.IsNullOrWhiteSpace(caption) ? "Describe this image" : caption;
        }

        List<UserMessageDataAttachmentsItem>? attachments = null;
        if (hasPhoto)
        {
            var photo = update.Message.Photo?.OrderByDescending(p => p.FileSize ?? 0).FirstOrDefault();
            if (photo == null)
            {
                var fallbackMessages = await _copilotService.SendMessageAsync(sessionId, finalPrompt!);
                var fallbackReply = fallbackMessages.LastOrDefault()?.Content ?? "(no response)";
                await SendMessageAsync(chatId, fallbackReply, cancellationToken);
                return;
            }
            var filePath = await DownloadTelegramFileAsync(photo.FileId, cancellationToken);
            attachments = new List<UserMessageDataAttachmentsItem>
            {
                new UserMessageDataAttachmentsItemFile
                {
                    Type = "file",
                    Path = filePath,
                    DisplayName = Path.GetFileName(filePath)
                }
            };
        }

        var responseMessages = await _copilotService.SendMessageAsync(sessionId, finalPrompt!, attachments);
        var replyMessage = responseMessages.LastOrDefault()?.Content ?? "(no response)";

        await SendMessageAsync(chatId, replyMessage, cancellationToken);
    }

    private async Task<string> GetOrCreateSessionAsync(long chatId)
    {
        if (_chatSessions.TryGetValue(chatId, out var sessionId))
        {
            return sessionId;
        }

        var newSessionId = await _copilotService.CreateSessionAsync(_options.DefaultModel);
        _chatSessions[chatId] = newSessionId;
        return newSessionId;
    }

    private async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
        var payload = new
        {
            chat_id = chatId,
            text = text
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> DownloadTelegramFileAsync(string fileId, CancellationToken cancellationToken)
    {
        var fileUrl = $"https://api.telegram.org/bot{_options.BotToken}/getFile?file_id={fileId}";
        using var fileResponse = await _httpClient.GetAsync(fileUrl, cancellationToken);
        fileResponse.EnsureSuccessStatusCode();

        var fileJson = await fileResponse.Content.ReadAsStringAsync(cancellationToken);
        var fileResult = JsonSerializer.Deserialize<TelegramResponse<TelegramFileResponse>>(fileJson, JsonOptions);
        var filePath = fileResult?.Result?.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Telegram file path not found");
        }

        var downloadUrl = $"https://api.telegram.org/file/bot{_options.BotToken}/{filePath}";
        using var downloadResponse = await _httpClient.GetAsync(downloadUrl, cancellationToken);
        downloadResponse.EnsureSuccessStatusCode();

        var tempFile = Path.Combine(Path.GetTempPath(), $"telegram_{Guid.NewGuid()}_{Path.GetFileName(filePath)}");
        await using var fs = File.OpenWrite(tempFile);
        await downloadResponse.Content.CopyToAsync(fs, cancellationToken);
        return tempFile;
    }

    private async Task<string> HandleModelSwitchAsync(long chatId, string prompt)
    {
        var parts = prompt.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return "使用方式: /model <model-name>";
        }

        var model = parts[1].Trim();
        var sessionId = await GetOrCreateSessionAsync(chatId);
        await _copilotService.UpdateSessionModelAsync(sessionId, model);
        return $"模型已切換為: {model}";
    }

    private sealed class TelegramFileResponse
    {
        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; }
    }

    private sealed class TelegramResponse<T>
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public T? Result { get; set; }
    }

    private sealed class TelegramUpdate
    {
        [JsonPropertyName("update_id")]
        public long UpdateId { get; set; }

        [JsonPropertyName("message")]
        public TelegramMessage? Message { get; set; }
    }

    private sealed class TelegramMessage
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("photo")]
        public List<TelegramPhoto> Photo { get; set; } = new();

        [JsonPropertyName("chat")]
        public TelegramChat Chat { get; set; } = new();
    }

    private sealed class TelegramPhoto
    {
        [JsonPropertyName("file_id")]
        public string FileId { get; set; } = string.Empty;

        [JsonPropertyName("file_size")]
        public int? FileSize { get; set; }
    }

    private sealed class TelegramChat
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }
}
