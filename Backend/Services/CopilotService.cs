using GitHub.Copilot.SDK;
using CopilotApi.Models;
using CopilotApi.Options;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CopilotApi.Services;

public class CopilotService : IDisposable
{
    private CopilotClient? _client;
    private readonly ConcurrentDictionary<string, CopilotSession> _sessions = new();
    private readonly ConcurrentDictionary<string, IDisposable> _eventSubscriptions = new();
    private readonly ConcurrentDictionary<string, SessionState> _sessionStates = new();
    private readonly ILogger<CopilotService> _logger;
    private readonly CopilotCliOptions _cliOptions;
    private string _currentDirectory;

    public CopilotService(ILogger<CopilotService> logger, IOptions<CopilotCliOptions> cliOptions)
    {
        _logger = logger;
        _cliOptions = cliOptions.Value;
        _currentDirectory = _cliOptions.WorkingDirectory ?? Directory.GetCurrentDirectory();
    }

    public async Task InitializeAsync()
    {
        if (_client == null)
        {
            try
            {
                var clientOptions = new CopilotClientOptions();
                if (!string.IsNullOrWhiteSpace(_currentDirectory))
                {
                    clientOptions.Cwd = _currentDirectory;
                }

                _client = new CopilotClient(clientOptions);
                await _client.StartAsync();
                _logger.LogInformation("Copilot CLI client started successfully with directory: {Directory}", _currentDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Copilot CLI client");
                throw;
            }
        }
    }

    public async Task<string> CreateSessionAsync(string model = "claude-sonnet-4.5", string channel = "web")
    {
        if (_client == null)
        {
            await InitializeAsync();
        }

        var session = await _client!.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            Streaming = true
        });

        _sessions[session.SessionId] = session;
        _sessionStates[session.SessionId] = new SessionState
        {
            SessionId = session.SessionId,
            Model = model,
            Channel = channel,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            Status = "idle"
        };
        _logger.LogInformation("Created session: {SessionId} for channel: {Channel}", session.SessionId, channel);
        
        return session.SessionId;
    }

    public async Task<List<ChatMessage>> SendMessageAsync(string sessionId, string prompt, List<UserMessageDataAttachmentsItem>? attachments = null)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        var messages = new List<ChatMessage>();
        var completionSource = new TaskCompletionSource<bool>();

        UpdateSessionState(sessionId, state =>
        {
            state.Status = "running";
            state.LastPrompt = prompt;
            state.LastError = null;
            state.LastUpdatedAt = DateTime.UtcNow;
        });

        // 清理舊的事件訂閱
        if (_eventSubscriptions.TryRemove(sessionId, out var oldSubscription))
        {
            oldSubscription?.Dispose();
        }

        // 創建新的事件訂閱
        var subscription = session.On(evt =>
        {
            try
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent delta:
                        // 串流增量內容（可選）
                        break;

                    case AssistantMessageEvent msg:
                        messages.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = msg.Data.Content,
                            Timestamp = DateTime.UtcNow
                        });
                        UpdateSessionState(sessionId, state =>
                        {
                            state.LastResponse = msg.Data.Content;
                            state.LastResponsePreview = BuildPreview(msg.Data.Content);
                            state.LastUpdatedAt = DateTime.UtcNow;
                        });
                        break;

                    case SessionIdleEvent:
                        UpdateSessionState(sessionId, state =>
                        {
                            state.Status = "idle";
                            state.LastUpdatedAt = DateTime.UtcNow;
                        });
                        completionSource.TrySetResult(true);
                        break;

                    case SessionErrorEvent err:
                        _logger.LogError("Session error: {Message}", err.Data.Message);
                        UpdateSessionState(sessionId, state =>
                        {
                            state.Status = "error";
                            state.LastError = err.Data.Message;
                            state.LastUpdatedAt = DateTime.UtcNow;
                        });
                        completionSource.TrySetException(new Exception(err.Data.Message));
                        break;

                    case SessionModelChangeEvent modelChange:
                        _logger.LogInformation("Session {SessionId} model changed to {Model}", sessionId, modelChange.Data.NewModel);
                        UpdateSessionState(sessionId, state =>
                        {
                            state.Model = modelChange.Data.NewModel;
                            state.LastUpdatedAt = DateTime.UtcNow;
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event handler");
                UpdateSessionState(sessionId, state =>
                {
                    state.Status = "error";
                    state.LastError = ex.Message;
                    state.LastUpdatedAt = DateTime.UtcNow;
                });
                completionSource.TrySetException(ex);
            }
        });

        // 保存訂閱以便後續清理
        _eventSubscriptions[sessionId] = subscription;

        try
        {
            var messageOptions = new MessageOptions { Prompt = prompt };
            if (attachments is { Count: > 0 })
            {
                messageOptions.Attachments = attachments;
            }

            await session.SendAsync(messageOptions);
            await completionSource.Task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            UpdateSessionState(sessionId, state =>
            {
                state.Status = "error";
                state.LastError = ex.Message;
                state.LastUpdatedAt = DateTime.UtcNow;
            });
            throw;
        }

        return messages;
    }

    public async Task UpdateSessionModelAsync(string sessionId, string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model is required", nameof(model));
        }

        if (!_sessions.TryGetValue(sessionId, out _))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        // 發送 /model 命令切換 model
        await SendMessageAsync(sessionId, $"/model {model}");
        
        // 確保 state 立即反映新的 model（不等待 SessionModelChangeEvent）
        UpdateSessionState(sessionId, state =>
        {
            state.Model = model;
            state.LastUpdatedAt = DateTime.UtcNow;
        });
        
        _logger.LogInformation("Session {SessionId} model switched to {Model}", sessionId, model);
    }

    public List<string> GetActiveSessions()
    {
        return _sessions.Keys.ToList();
    }

    public List<SessionStatusInfo> GetSessionStatuses(string? channel = null)
    {
        var query = _sessionStates.Values.AsEnumerable();
        if (!string.IsNullOrEmpty(channel))
        {
            query = query.Where(s => s.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));
        }
        return query
            .OrderByDescending(s => s.LastUpdatedAt)
            .Select(MapSessionStatus)
            .ToList();
    }

    public SessionStatusInfo? GetSessionStatus(string sessionId)
    {
        return _sessionStates.TryGetValue(sessionId, out var state)
            ? MapSessionStatus(state)
            : null;
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        // 清理事件訂閱
        if (_eventSubscriptions.TryRemove(sessionId, out var subscription))
        {
            subscription?.Dispose();
        }

        // 刪除 session
        if (_sessions.TryRemove(sessionId, out var session))
        {
            await session.DisposeAsync();
            _logger.LogInformation("Deleted session: {SessionId}", sessionId);
        }

        _sessionStates.TryRemove(sessionId, out _);
    }

    public async Task<List<SessionTaskResult>> SendBatchAsync(List<string> sessionIds, string prompt)
    {
        if (sessionIds == null || sessionIds.Count == 0)
        {
            throw new ArgumentException("SessionIds is required", nameof(sessionIds));
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required", nameof(prompt));
        }

        var tasks = sessionIds.Distinct(StringComparer.OrdinalIgnoreCase).Select(async sessionId =>
        {
            try
            {
                var messages = await SendMessageAsync(sessionId, prompt);
                var lastMessage = messages.LastOrDefault();
                return new SessionTaskResult
                {
                    SessionId = sessionId,
                    Status = "completed",
                    Content = lastMessage?.Content ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                return new SessionTaskResult
                {
                    SessionId = sessionId,
                    Status = "error",
                    Error = ex.Message
                };
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    public string GetCurrentDirectory()
    {
        return _currentDirectory;
    }

    public async Task SwitchDirectoryAsync(string newDirectory)
    {
        if (string.IsNullOrWhiteSpace(newDirectory))
        {
            throw new ArgumentException("Directory path is required", nameof(newDirectory));
        }

        if (!Directory.Exists(newDirectory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {newDirectory}");
        }

        _logger.LogInformation("Switching directory from {OldDir} to {NewDir}", _currentDirectory, newDirectory);

        // 清理舊的 session 與訂閱（不要再 dispose session，避免 JsonRpc disposed 例外）
        foreach (var subscription in _eventSubscriptions.Values)
        {
            subscription?.Dispose();
        }
        _eventSubscriptions.Clear();

        _sessions.Clear();
        _sessionStates.Clear();

        // 停止舊的 client
        if (_client != null)
        {
            await _client.StopAsync();
            _client.Dispose();
            _client = null;
        }

        // 更新目錄
        _currentDirectory = newDirectory;

        // 重新初始化 client
        await InitializeAsync();

        _logger.LogInformation("Directory switched successfully to {Directory}", _currentDirectory);
    }

    public async Task ResetClientAsync()
    {
        _logger.LogWarning("Resetting Copilot CLI client and clearing sessions");

        foreach (var subscription in _eventSubscriptions.Values)
        {
            subscription?.Dispose();
        }
        _eventSubscriptions.Clear();

        foreach (var session in _sessions.Values)
        {
            await session.DisposeAsync();
        }
        _sessions.Clear();
        _sessionStates.Clear();

        if (_client != null)
        {
            await _client.StopAsync();
            _client.Dispose();
            _client = null;
        }

        await InitializeAsync();
    }

    public void Dispose()
    {
        // 清理所有事件訂閱
        foreach (var subscription in _eventSubscriptions.Values)
        {
            subscription?.Dispose();
        }
        _eventSubscriptions.Clear();

        // 清理所有 session
        foreach (var session in _sessions.Values)
        {
            session.DisposeAsync().AsTask().Wait();
        }
        _sessions.Clear();
        _sessionStates.Clear();

        // 停止 client
        _client?.StopAsync().Wait();
        _client?.Dispose();
    }

    private void UpdateSessionState(string sessionId, Action<SessionState> update)
    {
        if (_sessionStates.TryGetValue(sessionId, out var state))
        {
            lock (state)
            {
                update(state);
            }
        }
    }

    private static SessionStatusInfo MapSessionStatus(SessionState state)
    {
        return new SessionStatusInfo
        {
            SessionId = state.SessionId,
            Model = state.Model,
            Status = state.Status,
            Channel = state.Channel,
            CreatedAt = state.CreatedAt,
            LastUpdatedAt = state.LastUpdatedAt,
            LastPrompt = state.LastPrompt,
            LastResponse = state.LastResponse,
            LastResponsePreview = state.LastResponsePreview,
            LastError = state.LastError
        };
    }

    private static string? BuildPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var trimmed = content.Trim();
        return trimmed.Length <= 120 ? trimmed : trimmed.Substring(0, 120) + "...";
    }

    private sealed class SessionState
    {
        public string SessionId { get; set; } = string.Empty;
        public string Model { get; set; } = "claude-sonnet-4.5";
        public string Status { get; set; } = "idle";
        public string Channel { get; set; } = "web";
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public string? LastPrompt { get; set; }
        public string? LastResponse { get; set; }
        public string? LastResponsePreview { get; set; }
        public string? LastError { get; set; }
    }
}
