using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace Application.Infrastructure.Twitch;

public interface IPubSubService
{
    public void Start(string authToken);
}

public class PubSubService(ILogger<PubSubService> logger, ILogger<TwitchPubSub> twitchPubSubLogger) : IPubSubService
{
    private readonly ILogger<PubSubService> logger = logger;
    private readonly ILogger<TwitchPubSub> twitchPubSubLogger = twitchPubSubLogger;
    private static TwitchPubSub? client;
    private string authToken = string.Empty;

    public void Start(string authToken)
    {
        this.authToken = authToken;

        client = new TwitchPubSub(twitchPubSubLogger);
        client.OnPubSubServiceConnected += Client_OnPubSubServiceConnected;
        client.OnListenResponse += Client_OnListenResponse;
        client.OnStreamUp += Client_OnStreamUp;
        client.OnStreamDown += Client_OnStreamDown;
        client.OnChannelPointsRewardRedeemed += Client_OnChannelPointsRewardRedeemed;
        client.OnPubSubServiceError += Client_OnPubSubServiceError;
        client.OnViewCount += Client_OnViewCount;

        client.ListenToVideoPlayback("165699060");
        //client.ListenToChannelPoints("165699060");

        client.Connect();
    }

    private void Client_OnPubSubServiceConnected(object? sender, EventArgs e)
    {
        logger.LogDebug("PubSub_OnPubSubServiceConnected");
        client!.SendTopics(authToken);
    }

    private void Client_OnViewCount(object? sender, OnViewCountArgs e)
    {
        logger.LogDebug("PubSub_OnViewCount: {Viewers}", e.Viewers);
    }

    private void Client_OnChannelPointsRewardRedeemed(object? sender, OnChannelPointsRewardRedeemedArgs e)
    {
        logger.LogDebug("PubSub_OnChannelPointsRewardRedeemed");
    }

    private void Client_OnStreamUp(object? sender, OnStreamUpArgs e)
    {
        logger.LogDebug("PubSub_OnStreamUp");
    }

    private void Client_OnStreamDown(object? sender, OnStreamDownArgs e)
    {
        logger.LogDebug("PubSub_OnStreamDown");
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