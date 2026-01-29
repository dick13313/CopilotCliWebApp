using CopilotApi.Options;
using CopilotApi.Services;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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
        var result = JsonSerializer.Deserialize<TelegramResponse<List<TelegramUpdate>>>(json);
        return result?.Result ?? new List<TelegramUpdate>();
    }

    private async Task HandleUpdateAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (update.Message?.Text == null)
        {
            return;
        }

        var chatId = update.Message.Chat.Id;
        if (_options.AllowedChatId.HasValue && _options.AllowedChatId.Value != chatId)
        {
            _logger.LogWarning("Telegram message from unauthorized chat: {ChatId}", chatId);
            return;
        }

        var prompt = update.Message.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        var sessionId = await GetOrCreateSessionAsync(chatId);
        _logger.LogInformation("Telegram message received from {ChatId} in session {SessionId}", chatId, sessionId);

        var responseMessages = await _copilotService.SendMessageAsync(sessionId, prompt);
        var reply = responseMessages.LastOrDefault()?.Content ?? "(no response)";

        await SendMessageAsync(chatId, reply, cancellationToken);
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

    private sealed class TelegramResponse<T>
    {
        public bool Ok { get; set; }
        public T? Result { get; set; }
    }

    private sealed class TelegramUpdate
    {
        public long UpdateId { get; set; }
        public TelegramMessage? Message { get; set; }
    }

    private sealed class TelegramMessage
    {
        public string? Text { get; set; }
        public TelegramChat Chat { get; set; } = new();
    }

    private sealed class TelegramChat
    {
        public long Id { get; set; }
    }
}
