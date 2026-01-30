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

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/cd", StringComparison.OrdinalIgnoreCase))
        {
            var reply = await HandleDirectorySwitchAsync(prompt);
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

    private async Task<string> HandleDirectorySwitchAsync(string prompt)
    {
        var parts = prompt.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        
        // 如果只輸入 /cd，顯示當前目錄和可用目錄
        if (parts.Length < 2)
        {
            try
            {
                var currentDir = _copilotService.GetCurrentDirectory();
                var baseDir = new DirectoryInfo(currentDir).Parent?.FullName ?? currentDir;
                
                var sb = new StringBuilder();
                sb.AppendLine($"📂 當前目錄: {Path.GetFileName(currentDir)}");
                sb.AppendLine($"🏠 主目錄: {baseDir}");
                sb.AppendLine();
                sb.AppendLine("📋 可用目錄列表：");
                
                if (Directory.Exists(baseDir))
                {
                    var directories = Directory.GetDirectories(baseDir)
                        .Select(d => new DirectoryInfo(d))
                        .Where(d => !d.Name.StartsWith("."))
                        .OrderBy(d => d.Name)
                        .ToList();
                    
                    for (int i = 0; i < directories.Count; i++)
                    {
                        var marker = directories[i].FullName == currentDir ? "✓ " : "  ";
                        sb.AppendLine($"{marker}{i + 1}. {directories[i].Name}");
                    }
                }
                
                sb.AppendLine();
                sb.AppendLine("使用方式：");
                sb.AppendLine("• /cd <數字> - 切換到對應目錄");
                sb.AppendLine("• /cd <目錄名稱> - 切換到指定目錄");
                sb.AppendLine("• /cd .. - 返回主目錄");
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list directories");
                return $"❌ 無法列出目錄: {ex.Message}";
            }
        }

        var input = parts[1].Trim();
        string? targetDirectory = null;

        try
        {
            var currentDir = _copilotService.GetCurrentDirectory();
            var baseDir = new DirectoryInfo(currentDir).Parent?.FullName ?? currentDir;

            // 處理 ".." 返回主目錄
            if (input == "..")
            {
                targetDirectory = baseDir;
            }
            // 嘗試解析為數字
            else if (int.TryParse(input, out var dirIndex) && dirIndex >= 1)
            {
                if (Directory.Exists(baseDir))
                {
                    var directories = Directory.GetDirectories(baseDir)
                        .Select(d => new DirectoryInfo(d))
                        .Where(d => !d.Name.StartsWith("."))
                        .OrderBy(d => d.Name)
                        .ToList();
                    
                    if (dirIndex <= directories.Count)
                    {
                        targetDirectory = directories[dirIndex - 1].FullName;
                    }
                    else
                    {
                        return $"❌ 無效的目錄編號，請選擇 1-{directories.Count}";
                    }
                }
            }
            // 嘗試作為目錄名稱
            else
            {
                // 先嘗試相對於 base directory
                var fullPath = Path.Combine(baseDir, input);
                if (Directory.Exists(fullPath))
                {
                    targetDirectory = fullPath;
                }
                // 再嘗試絕對路徑
                else if (Directory.Exists(input))
                {
                    targetDirectory = input;
                }
                else
                {
                    return $"❌ 找不到目錄: {input}";
                }
            }

            if (targetDirectory != null)
            {
                await _copilotService.SwitchDirectoryAsync(targetDirectory);
                return $"✅ 已切換到目錄: {Path.GetFileName(targetDirectory)}";
            }
            
            return "❌ 無法切換目錄";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch directory to {Directory}", input);
            return $"❌ 切換目錄失敗: {ex.Message}";
        }
    }

    private async Task<string> HandleModelSwitchAsync(long chatId, string prompt)
    {
        var availableModels = new[]
        {
            ("claude-sonnet-4.5", "Claude Sonnet 4.5 (預設, 平衡型)"),
            ("claude-haiku-4.5", "Claude Haiku 4.5 (快速/經濟)"),
            ("claude-opus-4.5", "Claude Opus 4.5 (進階)"),
            ("claude-sonnet-4", "Claude Sonnet 4 (標準)"),
            ("gemini-3-pro-preview", "Gemini 3 Pro Preview (標準)"),
            ("gpt-5.2-codex", "GPT-5.2 Codex (標準)"),
            ("gpt-5.2", "GPT-5.2 (標準)"),
            ("gpt-5.1-codex-max", "GPT-5.1 Codex Max (標準)"),
            ("gpt-5.1-codex", "GPT-5.1 Codex (標準)"),
            ("gpt-5.1", "GPT-5.1 (標準)"),
            ("gpt-5", "GPT-5 (標準)"),
            ("gpt-5.1-codex-mini", "GPT-5.1 Codex Mini (快速/經濟)"),
            ("gpt-5-mini", "GPT-5 Mini (快速/經濟)"),
            ("gpt-4.1", "GPT-4.1 (快速/經濟)")
        };

        var parts = prompt.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        
        // 如果只輸入 /model，顯示模型列表
        if (parts.Length < 2)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📋 可用模型列表：");
            sb.AppendLine();
            for (int i = 0; i < availableModels.Length; i++)
            {
                sb.AppendLine($"{i + 1}. {availableModels[i].Item2}");
            }
            sb.AppendLine();
            sb.AppendLine("使用方式：");
            sb.AppendLine("• /model <數字> - 切換模型");
            sb.AppendLine("• /model <模型名稱> - 切換模型");
            return sb.ToString();
        }

        var input = parts[1].Trim();
        string? selectedModel = null;

        // 嘗試解析為數字
        if (int.TryParse(input, out var modelIndex) && modelIndex >= 1 && modelIndex <= availableModels.Length)
        {
            selectedModel = availableModels[modelIndex - 1].Item1;
        }
        else
        {
            // 直接使用模型名稱
            selectedModel = input;
        }

        try
        {
            var sessionId = await GetOrCreateSessionAsync(chatId);
            await _copilotService.UpdateSessionModelAsync(sessionId, selectedModel);
            
            // 找到對應的描述
            var modelDesc = availableModels.FirstOrDefault(m => m.Item1.Equals(selectedModel, StringComparison.OrdinalIgnoreCase)).Item2;
            return modelDesc != null 
                ? $"✅ 模型已切換為: {modelDesc}" 
                : $"✅ 模型已切換為: {selectedModel}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch model to {Model}", selectedModel);
            return $"❌ 切換模型失敗: {ex.Message}";
        }
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
