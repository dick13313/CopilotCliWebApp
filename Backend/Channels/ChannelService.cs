namespace CopilotApi.Channels;

public class ChannelService : IHostedService
{
    private readonly IEnumerable<IChatChannel> _channels;
    private readonly ILogger<ChannelService> _logger;

    public ChannelService(IEnumerable<IChatChannel> channels, ILogger<ChannelService> logger)
    {
        _channels = channels;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var channel in _channels)
        {
            try
            {
                await channel.StartAsync(cancellationToken);
                _logger.LogInformation("Channel started: {Channel}", channel.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start channel: {Channel}", channel.Name);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var channel in _channels)
        {
            try
            {
                await channel.StopAsync(cancellationToken);
                _logger.LogInformation("Channel stopped: {Channel}", channel.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop channel: {Channel}", channel.Name);
            }
        }
    }
}
