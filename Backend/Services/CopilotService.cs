using GitHub.Copilot.SDK;
using CopilotApi.Models;
using System.Collections.Concurrent;

namespace CopilotApi.Services;

public class CopilotService : IDisposable
{
    private CopilotClient? _client;
    private readonly ConcurrentDictionary<string, CopilotSession> _sessions = new();
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
        var currentContent = new System.Text.StringBuilder();

        session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    currentContent.Append(delta.Data.DeltaContent);
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
                    completionSource.SetResult(true);
                    break;

                case SessionErrorEvent err:
                    _logger.LogError("Session error: {Message}", err.Data.Message);
                    completionSource.SetException(new Exception(err.Data.Message));
                    break;
            }
        });

        await session.SendAsync(new MessageOptions { Prompt = prompt });
        await completionSource.Task;

        return messages;
    }

    public List<string> GetActiveSessions()
    {
        return _sessions.Keys.ToList();
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            await session.DisposeAsync();
            _logger.LogInformation("Deleted session: {SessionId}", sessionId);
        }
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.DisposeAsync().AsTask().Wait();
        }
        _sessions.Clear();

        _client?.StopAsync().Wait();
        _client?.Dispose();
    }
}
