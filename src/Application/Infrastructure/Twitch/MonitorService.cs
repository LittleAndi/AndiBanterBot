using TwitchLib.Api;
using TwitchLib.Api.Services;
using TwitchLib.Api.Services.Events;
using TwitchLib.Api.Services.Events.LiveStreamMonitor;

namespace Application.Infrastructure.Twitch;

public interface IMonitorService
{
    public void Start(string accessToken);
}

public class MonitorService(ILogger<MonitorService> logger, ChatOptions options) : IMonitorService
{
    private readonly ILogger<MonitorService> logger = logger;
    private readonly ChatOptions options = options;
    private readonly TwitchAPI twitchApi = new();
    private LiveStreamMonitorService? Monitor;


    public void Start(string accessToken)
    {
        twitchApi.Settings.ClientId = options.ClientId;
        twitchApi.Settings.AccessToken = accessToken;

        Monitor = new LiveStreamMonitorService(twitchApi, 60, 100);

        Monitor.OnStreamOnline += Monitor_OnStreamOnline;
        Monitor.OnStreamOffline += Monitor_OnStreamOffline;
        Monitor.OnStreamUpdate += Monitor_OnStreamUpdate;

        Monitor.OnServiceStarted += Monitor_OnServiceStarted;
        Monitor.OnChannelsSet += Monitor_OnChannelsSet;
        Monitor.OnServiceTick += Monitor_OnServiceTick;

        Monitor.SetChannelsByName([options.Channel]);

        Monitor.Start();
    }

    private void Monitor_OnServiceTick(object? sender, OnServiceTickArgs e)
    {
        if (sender == null)
        {
            logger.LogWarning("Montior_OnServiceStarted: no LiveStreamMonitorService");
            return;
        }
        var liveStreamMonitorService = (LiveStreamMonitorService)sender;
        logger.LogDebug(
            "Montior_OnServiceTick: Tick, ChannelsToMonitor {ChannelsToMonitor}, LiveStreams {Streams}",
            string.Join(", ", liveStreamMonitorService.ChannelsToMonitor),
            string.Join(", ", liveStreamMonitorService.LiveStreams.Keys)
        );
    }

    private void Monitor_OnChannelsSet(object? sender, OnChannelsSetArgs e)
    {
        logger.LogInformation("Montior_OnChannelsSet: {Channels}", string.Join(", ", e.Channels));
    }

    private void Monitor_OnServiceStarted(object? sender, OnServiceStartedArgs e)
    {
        if (sender == null)
        {
            logger.LogWarning("Montior_OnServiceStarted: no LiveStreamMonitorService");
            return;
        }
        var liveStreamMonitorService = (LiveStreamMonitorService)sender;
        logger.LogInformation("Montior_OnServiceStarted: Channels to monitor: {ChannelsToMonitor}", string.Join(", ", liveStreamMonitorService.ChannelsToMonitor));
    }

    private void Monitor_OnStreamUpdate(object? sender, OnStreamUpdateArgs e)
    {
        logger.LogInformation("Montior_Stream update: {Title} ({GameName})", e.Stream.Title, e.Stream.GameName);
    }

    private void Monitor_OnStreamOffline(object? sender, OnStreamOfflineArgs e)
    {
        logger.LogInformation("Montior_Stream offline {GameName}", e.Stream.GameName);
    }

    private void Monitor_OnStreamOnline(object? sender, OnStreamOnlineArgs e)
    {
        logger.LogInformation("Montior_Stream online: {Title} ({GameName})", e.Stream.Title, e.Stream.GameName);
    }
}
