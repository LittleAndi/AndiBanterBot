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
        this.eventSubWebsocketClient.ChannelSubscribe += OnChannelSubscribe;
        this.eventSubWebsocketClient.ChannelSubscriptionMessage += OnChannelSubscriptionMessage;
    }


    // The channel.subscribe subscription type sends a notification when a specified channel receives a subscriber. This does not include resubscribes.
    private async Task OnChannelSubscribe(object sender, ChannelSubscribeArgs args)
    {
        var userName = args.Notification.Payload.Event.BroadcasterUserName;
        var isGift = args.Notification.Payload.Event.IsGift;
        var tier = args.Notification.Payload.Event.Tier;
        var subscriberUserName = args.Notification.Payload.Event.UserName;
        var broadcasterUserId = args.Notification.Payload.Event.BroadcasterUserLogin;

        logger.LogInformation("Websocket OnChannelSubscribe: {SubscriberUserName} subscribed to {BroadcasterUserName}, tier: {Tier}", subscriberUserName, userName, tier);

        var processInstructionCommand = new ProcessInstructionCommand(
            $"We got a new subscriber! Send love and thanks to {subscriberUserName}!",
            broadcasterUserId
        );
        await mediator.Send(processInstructionCommand);
    }

    private async Task OnChannelSubscriptionMessage(object sender, ChannelSubscriptionMessageArgs args)
    {
        var userName = args.Notification.Payload.Event.BroadcasterUserName;
        var cumulativeMonths = args.Notification.Payload.Event.CumulativeMonths;
        var tier = args.Notification.Payload.Event.Tier;
        var subscriberUserName = args.Notification.Payload.Event.UserName;
        var broadcasterUserId = args.Notification.Payload.Event.BroadcasterUserLogin;

        logger.LogInformation("Websocket OnChannelSubscribe: {SubscriberUserName} resubscribed to {BroadcasterUserName}, tier: {Tier}", subscriberUserName, userName, tier);

        var processInstructionCommand = new ProcessInstructionCommand(
            $"{subscriberUserName} resubscribed! They subscribed for a total of {cumulativeMonths} months! Send love and cheers to {subscriberUserName}!",
            broadcasterUserId
        );
        await mediator.Send(processInstructionCommand);
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

    private async Task OnChannelFollow(object? sender, ChannelFollowArgs e)
    {
        var newFollower = e.Notification.Payload.Event.UserName;
        var broadcasterUserId = e.Notification.Payload.Event.BroadcasterUserLogin;

        logger.LogInformation("{UserName} followed {BroadcasterUserName} at {FollowedAt}", newFollower, e.Notification.Payload.Event.BroadcasterUserName, e.Notification.Payload.Event.FollowedAt);

        var processInstructionCommand = new ProcessInstructionCommand($"Welcome {newFollower} as a new follower! Post some hype in chat for the new follower!", broadcasterUserId);
        await mediator.Send(processInstructionCommand);
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

    private async Task OnChannelAdBreakBegin(object sender, ChannelAdBreakBeginArgs args)
    {
        var adDurationSeconds = args.Notification.Payload.Event.DurationSeconds;
        var broadcasterUserId = args.Notification.Payload.Event.BroadcasterUserLogin;
        logger.LogInformation("WebSocket ChannelAdBreakBegin: {DurationSeconds} seconds", adDurationSeconds);

        var processInstructionCommand = new ProcessInstructionCommand(
            $@"Tell the chat an AD started, it will be over in {adDurationSeconds} seconds.
            Give chat a suggestion what to do in the meantime.
            If nothing else you can invite them to join our Discord server with this link {options.DiscordJoinLink}
            Remind the chat that they can use their Prime Sub to sub to the channel to avoid ads.",
            broadcasterUserId
        );

        await mediator.Send(processInstructionCommand);
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

            // channel.subscribe / channel.subscription.message
            try
            {
                var conditions = new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", "165699060" }
                };
                await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.subscribe",
                    "1",
                    conditions,
                    EventSubTransportMethod.Websocket,
                    websocketSessionId: eventSubWebsocketClient.SessionId
                );
                await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.subscription.message",
                    "1",
                    conditions,
                    EventSubTransportMethod.Websocket,
                    websocketSessionId: eventSubWebsocketClient.SessionId
                );
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "channel.subscribe/subscription.message");
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
