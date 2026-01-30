using CopilotApi.Options;
using CopilotApi.Services;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Text.Json.Serialization;

namespace CopilotApi.Channels;

public class TelegramChannel : IChatChannel
{
    private readonly ILogger<TelegramChannel> _logger;
    private readonly CopilotService _copilotService;
    private readonly TelegramOptions _options;
    private readonly CopilotCliOptions _cliOptions;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<long, string> _chatSessions = new();
    private readonly ConcurrentDictionary<long, string> _activeSessions = new();
    private readonly string? _baseDirectory;
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
        IOptions<CopilotCliOptions> cliOptions,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _copilotService = copilotService;
        _options = options.Value;
        _cliOptions = cliOptions.Value;
        _httpClient = httpClientFactory.CreateClient();
        _baseDirectory = ResolveBaseDirectory(_cliOptions.WorkingDirectory);
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
            var modelReply = await HandleModelSwitchAsync(chatId, prompt);
            await SendMessageAsync(chatId, modelReply, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
        {
            var helpReply = GetHelpText();
            await SendMessageAsync(chatId, helpReply, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/new", StringComparison.OrdinalIgnoreCase))
        {
            var newReply = await CreateAndUseSessionAsync(chatId);
            await SendMessageAsync(chatId, newReply, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/use", StringComparison.OrdinalIgnoreCase))
        {
            var useReply = await HandleUseCommandAsync(chatId, prompt);
            await SendMessageAsync(chatId, useReply, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/list", StringComparison.OrdinalIgnoreCase))
        {
            var listReply = BuildSessionListReply(chatId);
            await SendMessageAsync(chatId, listReply, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/close", StringComparison.OrdinalIgnoreCase))
        {
            var closeReply = await HandleCloseCommandAsync(chatId, prompt);
            await SendMessageAsync(chatId, closeReply, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/status", StringComparison.OrdinalIgnoreCase))
        {
            var statusReply = await HandleStatusCommandAsync(chatId, prompt);
            await SendMessageAsync(chatId, statusReply, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/session", StringComparison.OrdinalIgnoreCase))
        {
            var sessionReply = await HandleSessionCommandAsync(chatId, prompt);
            await SendMessageAsync(chatId, sessionReply, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/task", StringComparison.OrdinalIgnoreCase))
        {
            var taskReply = await HandleTaskCommandAsync(chatId, prompt);
            await SendMessageAsync(chatId, taskReply, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith("/cd", StringComparison.OrdinalIgnoreCase))
        {
            var directoryReply = await HandleDirectorySwitchAsync(prompt);
            await SendMessageAsync(chatId, directoryReply, cancellationToken);
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
        var status = _copilotService.GetSessionStatus(sessionId);
        var reply = BuildTelegramReply(sessionId, status?.Status ?? "idle", replyMessage, status?.LastUpdatedAt);

        await SendMessageAsync(chatId, reply, cancellationToken);
    }

    private async Task<string> GetOrCreateSessionAsync(long chatId)
    {
        if (_chatSessions.TryGetValue(chatId, out var sessionId))
        {
            if (_copilotService.GetSessionStatus(sessionId) != null)
            {
                return sessionId;
            }

            _chatSessions.Remove(chatId);
            _activeSessions.TryRemove(chatId, out _);
        }

        var newSessionId = await _copilotService.CreateSessionAsync(_options.DefaultModel);
        _chatSessions[chatId] = newSessionId;
        _activeSessions[chatId] = newSessionId;
        return newSessionId;
    }

    private async Task<string> HandleSessionCommandAsync(long chatId, string prompt)
    {
        var parts = prompt.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return GetSessionHelpText();
        }

        var command = parts[1].ToLowerInvariant();
        switch (command)
        {
            case "list":
                return BuildSessionListReply(chatId);
            case "use":
                if (parts.Length < 3)
                {
                    return "❌ 使用方式: /session use <sessionId>";
                }
                return await SetActiveSessionAsync(chatId, parts[2]);
            case "new":
                return await CreateAndUseSessionAsync(chatId);
            case "close":
                if (parts.Length < 3)
                {
                    return "❌ 使用方式: /session close <sessionId>";
                }
                return await CloseSessionAsync(chatId, parts[2]);
            case "status":
                if (parts.Length < 3)
                {
                    var activeSessionId = await GetActiveSessionAsync(chatId);
                    return BuildSessionStatusReply(activeSessionId);
                }
                return BuildSessionStatusReply(parts[2]);
            default:
                return GetSessionHelpText();
        }
    }

    private async Task<string> HandleTaskCommandAsync(long chatId, string prompt)
    {
        var parts = prompt.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return "使用方式:\n• /task <prompt> (自動建立新 session)\n• /task <編號[,編號2]> <prompt>";
        }

        string? promptText;
        var targetIds = new List<string>();

        if (parts.Length == 2)
        {
            promptText = parts[1].Trim();
            var sessionId = await CreateSessionOnlyAsync();
            targetIds.Add(sessionId);
            _activeSessions[chatId] = sessionId;
            _chatSessions[chatId] = sessionId;
        }
        else
        {
            var inputIds = parts[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            promptText = parts[2].Trim();

            foreach (var inputId in inputIds)
            {
                var resolved = ResolveSessionId(inputId);
                if (resolved == null)
                {
                    return $"❌ 找不到 session: {inputId}";
                }
                targetIds.Add(resolved);
            }
        }

        if (targetIds.Count == 0)
        {
            return "❌ SessionId 不可為空";
        }

        if (string.IsNullOrWhiteSpace(promptText))
        {
            return "❌ Prompt 不可為空";
        }

        var results = await _copilotService.SendBatchAsync(targetIds, promptText);
        var sb = new StringBuilder();
        sb.AppendLine("✅ 任務已完成：");
        foreach (var result in results)
        {
            var status = result.Status ?? "unknown";
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                sb.AppendLine($"• {result.SessionId} [{status}] ❌ {result.Error}");
            }
            else
            {
                sb.AppendLine($"• {result.SessionId} [{status}] {BuildPreview(result.Content)}");
            }
        }

        return sb.ToString();
    }

    private string GetSessionHelpText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("📌 Session 指令：");
        sb.AppendLine("• /session list - 查看 session 列表");
        sb.AppendLine("• /session use <編號|sessionId> - 切換使用的 session");
        sb.AppendLine("• /session new - 建立並切換到新 session");
        sb.AppendLine("• /session close <編號|sessionId> - 關閉 session");
        sb.AppendLine("• /session status [編號|sessionId] - 查看 session 狀態");
        sb.AppendLine("• /task <prompt> - 自動建立新 session 指派任務");
        sb.AppendLine("• /task <編號[,編號2]> <prompt> - 指派任務");
        return sb.ToString();
    }

    private string BuildSessionListReply(long chatId)
    {
        var activeSessionId = _activeSessions.TryGetValue(chatId, out var active) ? active : null;
        var sessions = _copilotService.GetSessionStatuses();
        if (sessions.Count == 0)
        {
            return "尚無任何 session，請使用 /session new 建立。";
        }

        var sb = new StringBuilder();
        sb.AppendLine("📋 Session 列表（可用 /use <編號>）：");
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            var marker = session.SessionId == activeSessionId ? "✓" : " ";
            sb.AppendLine($"{marker} {i + 1}. {session.SessionId} [{session.Status}] {session.Model}");
        }
        return sb.ToString();
    }

    private async Task<string> SetActiveSessionAsync(long chatId, string sessionId)
    {
        var resolvedId = ResolveSessionId(sessionId);
        var status = resolvedId == null ? null : _copilotService.GetSessionStatus(resolvedId);
        if (status == null)
        {
            return $"❌ 找不到 session: {sessionId}";
        }

        _activeSessions[chatId] = status.SessionId;
        _chatSessions[chatId] = status.SessionId;
        return $"✅ 已切換 session: {status.SessionId}";
    }

    private async Task<string> CreateAndUseSessionAsync(long chatId)
    {
        var newSessionId = await _copilotService.CreateSessionAsync(_options.DefaultModel);
        _chatSessions[chatId] = newSessionId;
        _activeSessions[chatId] = newSessionId;
        return $"✅ 已建立並切換到新 session: {newSessionId}";
    }

    private async Task<string> CreateSessionOnlyAsync()
    {
        return await _copilotService.CreateSessionAsync(_options.DefaultModel);
    }

    private async Task<string> HandleUseCommandAsync(long chatId, string prompt)
    {
        var parts = prompt.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return "❌ 使用方式: /use <sessionId>";
        }

        return await SetActiveSessionAsync(chatId, parts[1]);
    }

    private async Task<string> HandleCloseCommandAsync(long chatId, string prompt)
    {
        var parts = prompt.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return "❌ 使用方式: /close <sessionId>";
        }

        var resolvedId = ResolveSessionId(parts[1]);
        if (resolvedId == null)
        {
            return $"❌ 找不到 session: {parts[1]}";
        }
        return await CloseSessionAsync(chatId, resolvedId);
    }

    private async Task<string> HandleStatusCommandAsync(long chatId, string prompt)
    {
        var parts = prompt.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            var activeSessionId = await GetActiveSessionAsync(chatId);
            return BuildSessionStatusReply(activeSessionId);
        }

        var resolvedId = ResolveSessionId(parts[1]);
        return resolvedId == null ? $"❌ 找不到 session: {parts[1]}" : BuildSessionStatusReply(resolvedId);
    }

    private string GetHelpText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("📖 指令列表：");
        sb.AppendLine("• /help - 顯示所有指令");
        sb.AppendLine("• /new - 建立新 session 並切換");
        sb.AppendLine("• /use <編號|sessionId> - 切換 session");
        sb.AppendLine("• /list - 列出所有 session");
        sb.AppendLine("• /close <編號|sessionId> - 關閉 session");
        sb.AppendLine("• /status [編號|sessionId] - 查看 session 狀態");
        sb.AppendLine("• /task <prompt> - 自動建立新 session 指派任務");
        sb.AppendLine("• /task <編號[,編號2]> <prompt> - 指派任務");
        sb.AppendLine("• /model [model] - 切換模型");
        sb.AppendLine("• /cd [dir] - 列出或切換工作目錄");
        return sb.ToString();
    }

    private string? ResolveSessionId(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        var sessions = _copilotService.GetSessionStatuses();
        if (int.TryParse(trimmed, out var index))
        {
            if (index >= 1 && index <= sessions.Count)
            {
                return sessions[index - 1].SessionId;
            }
            return null;
        }

        var exact = sessions.FirstOrDefault(s => s.SessionId.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact.SessionId;
        }

        var matches = sessions.Where(s => s.SessionId.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count == 1 ? matches[0].SessionId : null;
    }

    private async Task<string> CloseSessionAsync(long chatId, string sessionId)
    {
        await _copilotService.DeleteSessionAsync(sessionId);
        if (_activeSessions.TryGetValue(chatId, out var active) && active == sessionId)
        {
            _activeSessions.TryRemove(chatId, out _);
        }
        if (_chatSessions.TryGetValue(chatId, out var current) && current == sessionId)
        {
            _chatSessions.Remove(chatId);
        }
        return $"✅ 已關閉 session: {sessionId}";
    }

    private string BuildSessionStatusReply(string sessionId)
    {
        var status = _copilotService.GetSessionStatus(sessionId);
        if (status == null)
        {
            return $"❌ 找不到 session: {sessionId}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"🧾 Session: {status.SessionId}");
        sb.AppendLine($"Model: {status.Model}");
        sb.AppendLine($"Status: {status.Status}");
        sb.AppendLine($"Last Updated: {status.LastUpdatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        if (!string.IsNullOrWhiteSpace(status.LastResponsePreview))
        {
            sb.AppendLine($"Last Response: {status.LastResponsePreview}");
        }
        if (!string.IsNullOrWhiteSpace(status.LastError))
        {
            sb.AppendLine($"Error: {status.LastError}");
        }
        return sb.ToString();
    }

    private async Task<string> GetActiveSessionAsync(long chatId)
    {
        if (_activeSessions.TryGetValue(chatId, out var sessionId))
        {
            return sessionId;
        }

        sessionId = await GetOrCreateSessionAsync(chatId);
        _activeSessions[chatId] = sessionId;
        return sessionId;
    }

    private string BuildTelegramReply(string sessionId, string status, string content, DateTime? updatedAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🧾 Session: {sessionId}");
        sb.AppendLine($"Status: {status}");
        if (updatedAt.HasValue)
        {
            sb.AppendLine($"Updated: {updatedAt:HH:mm:ss} UTC");
        }
        sb.AppendLine();
        sb.AppendLine(content);
        return sb.ToString();
    }

    private string BuildPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var trimmed = content.Trim();
        return trimmed.Length <= 120 ? trimmed : trimmed.Substring(0, 120) + "...";
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
                var baseDir = GetBaseDirectoryOrThrow();
                
                var sb = new StringBuilder();
                sb.AppendLine($"📂 當前目錄: {Path.GetFileName(currentDir)}");
                sb.AppendLine($"🏠 主目錄: {baseDir}");
                sb.AppendLine();
                sb.AppendLine("📋 可用目錄列表：");
                
                if (Directory.Exists(baseDir))
                {
                    var directories = GetAvailableDirectories(baseDir);
                    
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
            var baseDir = GetBaseDirectoryOrThrow();
            var directories = GetAvailableDirectories(baseDir);

            if (directories.Count == 0)
            {
                return "❌ 主目錄下沒有可切換的資料夾";
            }

            // 嘗試解析為數字
            if (int.TryParse(input, out var dirIndex) && dirIndex >= 1)
            {
                if (dirIndex <= directories.Count)
                {
                    targetDirectory = directories[dirIndex - 1].FullName;
                }
                else
                {
                    return $"❌ 無效的目錄編號，請選擇 1-{directories.Count}";
                }
            }
            // 嘗試作為目錄名稱
            else
            {
                var match = directories.FirstOrDefault(d => string.Equals(d.Name, input, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    targetDirectory = match.FullName;
                }
                else
                {
                    if (Path.IsPathRooted(input))
                    {
                        var fullInput = Path.GetFullPath(input);
                        if (directories.Any(d => string.Equals(d.FullName, fullInput, StringComparison.OrdinalIgnoreCase)))
                        {
                            targetDirectory = fullInput;
                        }
                    }

                    if (targetDirectory == null)
                    {
                        return $"❌ 找不到可切換的目錄: {input}";
                    }
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

    private static string? ResolveBaseDirectory(string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(baseDirectory);
        return Directory.Exists(fullPath) ? fullPath : null;
    }

    private string GetBaseDirectoryOrThrow()
    {
        if (!string.IsNullOrWhiteSpace(_baseDirectory) && Directory.Exists(_baseDirectory))
        {
            return _baseDirectory;
        }

        throw new InvalidOperationException("Working directory not configured");
    }

    private static List<DirectoryInfo> GetAvailableDirectories(string baseDir)
    {
        return Directory.GetDirectories(baseDir)
            .Select(d => new DirectoryInfo(d))
            .Where(d => !d.Name.StartsWith("."))
            .OrderBy(d => d.Name)
            .ToList();
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
