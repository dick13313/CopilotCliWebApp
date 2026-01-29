using GitHub.Copilot.SDK;
using CopilotApi.Models;
using System.Collections.Concurrent;

namespace CopilotApi.Services;

public class CopilotService : IDisposable
{
    private CopilotClient? _client;
    private readonly ConcurrentDictionary<string, CopilotSession> _sessions = new();
    private readonly ConcurrentDictionary<string, IDisposable> _eventSubscriptions = new();
    private readonly ILogger<CopilotService> _logger;

    public CopilotService(ILogger<CopilotService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_client == null)
        {
            try
            {
                _client = new CopilotClient();
                await _client.StartAsync();
                _logger.LogInformation("Copilot CLI client started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Copilot CLI client");
                throw;
            }
        }
    }

    public async Task<string> CreateSessionAsync(string model = "claude-sonnet-4.5")
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
        _logger.LogInformation("Created session: {SessionId}", session.SessionId);
        
        return session.SessionId;
    }

    public async Task<List<ChatMessage>> SendMessageAsync(string sessionId, string prompt)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        var messages = new List<ChatMessage>();
        var completionSource = new TaskCompletionSource<bool>();

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
                        break;

                    case SessionIdleEvent:
                        completionSource.TrySetResult(true);
                        break;

                    case SessionErrorEvent err:
                        _logger.LogError("Session error: {Message}", err.Data.Message);
                        completionSource.TrySetException(new Exception(err.Data.Message));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event handler");
                completionSource.TrySetException(ex);
            }
        });

        // 保存訂閱以便後續清理
        _eventSubscriptions[sessionId] = subscription;

        try
        {
            await session.SendAsync(new MessageOptions { Prompt = prompt });
            await completionSource.Task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            throw;
        }

        return messages;
    }

    public List<string> GetActiveSessions()
    {
        return _sessions.Keys.ToList();
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

        // 停止 client
        _client?.StopAsync().Wait();
        _client?.Dispose();
    }
}
