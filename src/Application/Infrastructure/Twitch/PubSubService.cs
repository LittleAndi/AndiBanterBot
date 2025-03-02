using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace Application.Infrastructure.Twitch;

public interface IPubSubService
{
    public void Start(string authToken);
}

public class PubSubService(ILogger<PubSubService> logger, ILogger<TwitchPubSub> twitchPubSubLogger, IMediator mediator) : IPubSubService
{
    private readonly ILogger<PubSubService> logger = logger;
    private readonly ILogger<TwitchPubSub> twitchPubSubLogger = twitchPubSubLogger;
    private readonly IMediator mediator = mediator;
    private static TwitchPubSub? client;
    private string authToken = string.Empty;

    public void Start(string authToken)
    {
        this.authToken = authToken;

        client = new TwitchPubSub(twitchPubSubLogger);
        client.OnListenResponse += Client_OnListenResponse;
        client.OnPubSubServiceConnected += Client_OnPubSubServiceConnected;
        client.OnPubSubServiceClosed += Client_OnPubSubServiceClosed;
        client.OnPubSubServiceError += Client_OnPubSubServiceError;

        var channelId = "165699060";
        ListenToVideoPlayback(channelId);

        client.Connect();
    }

    private void ListenToVideoPlayback(string channelId)
    {
        client!.OnStreamUp += Client_OnStreamUp;
        client!.OnStreamDown += Client_OnStreamDown;
        client!.OnViewCount += Client_OnViewCount;
        client!.OnCommercial += Client_OnCommercial;
        client!.ListenToVideoPlayback(channelId);
    }

    private void Client_OnPubSubServiceClosed(object? sender, EventArgs e)
    {
        logger.LogDebug("PubSub_OnPubSubServiceClosed");
    }

    private void Client_OnPubSubServiceConnected(object? sender, EventArgs e)
    {
        logger.LogDebug("PubSub_OnPubSubServiceConnected");
        client!.ListenToChannelPoints("165699060");
        client!.SendTopics(authToken);
    }

    private void Client_OnViewCount(object? sender, OnViewCountArgs e)
    {
        logger.LogDebug("PubSub_OnViewCount: {Viewers}", e.Viewers);
    }

    private void Client_OnCommercial(object? sender, OnCommercialArgs e)
    {
        logger.LogDebug("PubSub_OnCommercial: {Length} seconds", e.Length);
    }

    private void Client_OnStreamUp(object? sender, OnStreamUpArgs e)
    {
        logger.LogDebug("PubSub_OnStreamUp: {ChannelId} {PlayDelay}", e.ChannelId, e.PlayDelay);
    }

    private void Client_OnStreamDown(object? sender, OnStreamDownArgs e)
    {
        logger.LogDebug("PubSub_OnStreamDown: {ChannelId}", e.ChannelId);
    }

    private void Client_OnListenResponse(object? sender, OnListenResponseArgs e)
    {
        logger.LogDebug("PubSub_OnListenResponse: {Channel} {Topic} {Successful}", e.ChannelId, e.Topic, e.Successful);
    }

    private void Client_OnPubSubServiceError(object? sender, OnPubSubServiceErrorArgs e)
    {
        logger.LogDebug("PubSub_OnPubSubServiceError: {Message}", e.Exception.Message);
    }
}
