using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace Application.Infrastructure.Twitch;

public interface IWebsocketService
{
    public Task StartAsync(string accessToken, CancellationToken cancellationToken = default);
}
public class WebsocketService : IWebsocketService
{
    private readonly ILogger<WebsocketService> logger;
    private readonly EventSubWebsocketClient eventSubWebsocketClient;
    private readonly ChatOptions options;
    private readonly IMediator mediator;
    private readonly TwitchAPI twitchApi = new();
    private string userId = string.Empty;
    private string broadcasterId = string.Empty;

    public WebsocketService(
        ILogger<WebsocketService> logger,
        EventSubWebsocketClient eventSubWebsocketClient,
        ChatOptions options,
        IMediator mediator
    )
    {
        this.logger = logger;

        this.eventSubWebsocketClient = eventSubWebsocketClient;
        this.options = options;
        this.mediator = mediator;
        this.eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
        this.eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
        this.eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
        this.eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

        this.eventSubWebsocketClient.StreamOnline += OnStreamOnline;
        this.eventSubWebsocketClient.StreamOffline += OnStreamOffline;
        this.eventSubWebsocketClient.ChannelFollow += OnChannelFollow;
        this.eventSubWebsocketClient.ChannelVipAdd += OnChannelVipAdd;
        this.eventSubWebsocketClient.ChannelVipRemove += OnChannelVipRemove;
        this.eventSubWebsocketClient.ChannelAdBreakBegin += OnChannelAdBreakBegin;
    }

    private Task OnStreamOnline(object sender, StreamOnlineArgs args)
    {
        var userId = args.Notification.Payload.Event.BroadcasterUserId;
        var userName = args.Notification.Payload.Event.BroadcasterUserName;
        var userLogin = args.Notification.Payload.Event.BroadcasterUserLogin;
        logger.LogInformation("Websocket OnStreamOnline: {BroadcasterUserName} is now online! {BroadcasterUserLogin}/{BroadcasterUserId}", userName, userLogin, userId);
        return Task.CompletedTask;
    }

    private Task OnStreamOffline(object sender, StreamOfflineArgs args)
    {
        var userId = args.Notification.Payload.Event.BroadcasterUserId;
        var userName = args.Notification.Payload.Event.BroadcasterUserName;
        var userLogin = args.Notification.Payload.Event.BroadcasterUserLogin;
        logger.LogInformation("Websocket OnStreamOffline: {BroadcasterUserName} is now offline! {BroadcasterUserLogin}/{BroadcasterUserId}", userName, userLogin, userId);
        return Task.CompletedTask;
    }

    private Task OnChannelFollow(object? sender, ChannelFollowArgs e)
    {
        var eventData = e.Notification.Payload.Event;
        logger.LogInformation("{UserName} followed {BroadcasterUserName} at {FollowedAt}", eventData.UserName, eventData.BroadcasterUserName, eventData.FollowedAt);
        return Task.CompletedTask;
    }

    private async Task OnChannelVipAdd(object sender, ChannelVipArgs args)
    {
        logger.LogInformation("WebSocket ChannelVipAdd");

        var newVip = args.Notification.Payload.Event.UserName;
        var broadcasterUserId = args.Notification.Payload.Event.BroadcasterUserLogin;

        var processInstructionCommand = new ProcessInstructionCommand($"Welcome {newVip} as a VIP member! Give some cheers by writing a limerick about the new VIP!", broadcasterUserId);
        await mediator.Send(processInstructionCommand);
    }

    private Task OnChannelVipRemove(object sender, ChannelVipArgs args)
    {
        logger.LogInformation("WebSocket ChannelVipRemove");
        return Task.CompletedTask;
    }

    private Task OnChannelAdBreakBegin(object sender, ChannelAdBreakBeginArgs args)
    {
        var adDurationSeconds = args.Notification.Payload.Event.DurationSeconds;
        logger.LogInformation("WebSocket ChannelAdBreakBegin: {DurationSeconds} seconds", adDurationSeconds);
        return Task.CompletedTask;
    }

    public async Task StartAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var validateAccessTokenResponse = await twitchApi.Auth.ValidateAccessTokenAsync(accessToken);
        logger.LogInformation("ValidateAccessTokenAsync: {UserId}", validateAccessTokenResponse.UserId);

        // Save for later use
        userId = validateAccessTokenResponse.UserId;
        broadcasterId = validateAccessTokenResponse.UserId;

        twitchApi.Settings.ClientId = options.ClientId;
        twitchApi.Settings.AccessToken = accessToken;

        await eventSubWebsocketClient.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await eventSubWebsocketClient.DisconnectAsync();
    }

    private async Task OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
    {
        logger.LogInformation("Websocket {SessionId} connected!", eventSubWebsocketClient.SessionId);

        if (!e.IsRequestedReconnect)
        {
            /*
               Subscribe to topics via the TwitchApi.Helix.EventSub object, this example shows how to subscribe
               to the channel follow event used in the example above.

               var conditions = new Dictionary<string, string>()
               {
                   { "broadcaster_user_id", someUserId }
               };
               var subscriptionResponse = await TwitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.follow", "2", conditions,
               EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId);

               You can find more examples on the subscription types and their requirements here https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types/
               Prerequisite: Twitchlib.Api nuget package installed (included in the Twitchlib package automatically)
           */

            // channel.follow
            try
            {
                var conditions = new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", "165699060" },
                    { "moderator_user_id", "165699060" }
                };

                var subscriptionResponse = await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.follow",
                    "2",
                    conditions,
                    EventSubTransportMethod.Websocket,
                    websocketSessionId: eventSubWebsocketClient.SessionId
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "channel.follow");
            }

            // channel.vip.add/remove
            try
            {
                var conditions = new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", "165699060" }
                };
                await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.vip.add",
                    "1",
                    conditions,
                    EventSubTransportMethod.Websocket,
                    websocketSessionId: eventSubWebsocketClient.SessionId
                );
                await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.vip.remove",
                    "1",
                    conditions,
                    EventSubTransportMethod.Websocket,
                    eventSubWebsocketClient.SessionId
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "channel.vip.add/remove");
            }

            // channel.ad_break.begin
            try
            {
                var conditions = new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", "165699060" }
                };
                await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.ad_break.begin",
                    "1",
                    conditions,
                    EventSubTransportMethod.Websocket,
                    websocketSessionId: eventSubWebsocketClient.SessionId
                );
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "channel.ad_break.begin");
            }

            // stream.online/offline
            try
            {
                var conditions = new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", "165699060" }
                };
                await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "stream.online",
                    "1",
                    conditions,
                    EventSubTransportMethod.Websocket,
                    websocketSessionId: eventSubWebsocketClient.SessionId
                );
                await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "stream.offline",
                    "1",
                    conditions,
                    EventSubTransportMethod.Websocket,
                    websocketSessionId: eventSubWebsocketClient.SessionId
                );
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "stream.online/offline");
            }
        }
    }

    private async Task OnWebsocketDisconnected(object? sender, EventArgs e)
    {
        logger.LogError("Websocket {SessionId} disconnected!", eventSubWebsocketClient.SessionId);

        // Don't do this in production. You should implement a better reconnect strategy with exponential backoff
        while (!await eventSubWebsocketClient.ReconnectAsync())
        {
            logger.LogError("Websocket reconnect failed!");
            await Task.Delay(1000);
        }
    }

    private Task OnWebsocketReconnected(object? sender, EventArgs e)
    {
        logger.LogWarning("Websocket {SessionId} reconnected", eventSubWebsocketClient.SessionId);
        return Task.CompletedTask;
    }

    private Task OnErrorOccurred(object? sender, ErrorOccuredArgs e)
    {
        logger.LogError(e.Exception, "Websocket {SessionId} - Error occurred!", eventSubWebsocketClient.SessionId);
        return Task.CompletedTask;
    }
}
