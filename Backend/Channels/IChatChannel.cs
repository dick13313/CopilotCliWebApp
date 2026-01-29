namespace CopilotApi.Channels;

public interface IChatChannel
{
    string Name { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
