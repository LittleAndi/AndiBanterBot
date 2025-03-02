using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
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
    private string botUserId = string.Empty;

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
        this.eventSubWebsocketClient.ChannelAdBreakBegin += OnChannelAdBreakBegin;
        this.eventSubWebsocketClient.ChannelBan += OnChannelBan;
        this.eventSubWebsocketClient.ChannelCharityCampaignDonate += OnChannelCharityCampaignDonate;
        this.eventSubWebsocketClient.ChannelCharityCampaignProgress += OnChannelCharityCampaignProgress;
        this.eventSubWebsocketClient.ChannelCharityCampaignStart += OnChannelCharityCampaignStart;
        this.eventSubWebsocketClient.ChannelChatMessage += OnChannelChatMessage;
        this.eventSubWebsocketClient.ChannelCheer += OnChannelCheer;
        this.eventSubWebsocketClient.ChannelFollow += OnChannelFollow;
        this.eventSubWebsocketClient.ChannelGoalBegin += OnChannelGoalBegin;
        this.eventSubWebsocketClient.ChannelGoalEnd += OnChannelGoalEnd;
        this.eventSubWebsocketClient.ChannelGoalProgress += OnChannelGoalProgress;
        this.eventSubWebsocketClient.ChannelGuestStarGuestUpdate += OnChannelGuestStarGuestUpdate;
        this.eventSubWebsocketClient.ChannelGuestStarSessionBegin += OnChannelGuestStarSessionBegin;
        this.eventSubWebsocketClient.ChannelGuestStarSessionEnd += OnChannelGuestStarSessionEnd;
        this.eventSubWebsocketClient.ChannelGuestStarSettingsUpdate += OnChannelGuestStarSettingsUpdate;
        this.eventSubWebsocketClient.ChannelGuestStarSlotUpdate += OnChannelGuestStarSlotUpdate;
        this.eventSubWebsocketClient.ChannelHypeTrainBegin += OnChannelHypeTrainBegin;
        this.eventSubWebsocketClient.ChannelHypeTrainEnd += OnChannelHypeTrainEnd;
        this.eventSubWebsocketClient.ChannelModeratorAdd += OnChannelModeratorAdd;
        this.eventSubWebsocketClient.ChannelModeratorRemove += OnChannelModeratorRemove;
        this.eventSubWebsocketClient.ChannelPointsAutomaticRewardRedemptionAdd += OnChannelPointsAutomaticRewardRedemption;
        this.eventSubWebsocketClient.ChannelPointsCustomRewardAdd += OnChannelPointsCustomRewardAdd;
        this.eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsCustomRewardRedemption;
        this.eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionUpdate += OnChannelPointsCustomRewardRedemptionUpdate;
        this.eventSubWebsocketClient.ChannelPointsCustomRewardRemove += OnChannelPointsCustomRewardRemove;
        this.eventSubWebsocketClient.ChannelPointsCustomRewardUpdate += OnChannelPointsCustomRewardUpdate;
        this.eventSubWebsocketClient.ChannelPollBegin += OnChannelPollBegin;
        this.eventSubWebsocketClient.ChannelPollEnd += OnChannelPollEnd;
        this.eventSubWebsocketClient.ChannelPollProgress += OnChannelPollProgress;
        this.eventSubWebsocketClient.ChannelPredictionBegin += OnChannelPredictionBegin;
        this.eventSubWebsocketClient.ChannelPredictionEnd += OnChannelPredictionEnd;
        this.eventSubWebsocketClient.ChannelPredictionLock += OnChannelPredictionLock;
        this.eventSubWebsocketClient.ChannelPredictionProgress += OnChannelPredictionProgress;
        this.eventSubWebsocketClient.ChannelRaid += OnChannelRaid;
        this.eventSubWebsocketClient.ChannelShieldModeBegin += OnChannelShieldModeBegin;
        this.eventSubWebsocketClient.ChannelShieldModeEnd += OnChannelShieldModeEnd;
        this.eventSubWebsocketClient.ChannelShoutoutCreate += OnChannelShoutoutCreate;
        this.eventSubWebsocketClient.ChannelShoutoutReceive += OnChannelShoutoutReceive;
        this.eventSubWebsocketClient.ChannelSubscribe += OnChannelSubscribe;
        this.eventSubWebsocketClient.ChannelSubscriptionEnd += OnChannelSubscriptionEnd;
        this.eventSubWebsocketClient.ChannelSubscriptionGift += OnChannelSubscriptionGift;
        this.eventSubWebsocketClient.ChannelSubscriptionMessage += OnChannelSubscriptionMessage;
        this.eventSubWebsocketClient.ChannelSuspiciousUserMessage += OnChannelSuspiciousUserMessage;
        this.eventSubWebsocketClient.ChannelSuspiciousUserUpdate += OnChannelSuspiciousUserUpdate;
        this.eventSubWebsocketClient.ChannelUnban += OnChannelUnban;
        this.eventSubWebsocketClient.ChannelUpdate += OnChannelUpdate;
        this.eventSubWebsocketClient.ChannelVipAdd += OnChannelVipAdd;
        this.eventSubWebsocketClient.ChannelVipRemove += OnChannelVipRemove;
        this.eventSubWebsocketClient.ChannelWarningAcknowledge += OnChannelWarningAcknowledge;
        this.eventSubWebsocketClient.ChannelWarningSend += OnChannelWarningSend;
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

    private Task OnChannelBan(object sender, ChannelBanArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelCharityCampaignDonate(object sender, ChannelCharityCampaignDonateArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelCharityCampaignProgress(object sender, ChannelCharityCampaignProgressArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelCharityCampaignStart(object sender, ChannelCharityCampaignStartArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelCheer(object sender, ChannelCheerArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
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

    private Task OnChannelGoalBegin(object sender, ChannelGoalBeginArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelGoalEnd(object sender, ChannelGoalEndArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelGoalProgress(object sender, ChannelGoalProgressArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelGuestStarGuestUpdate(object sender, ChannelGuestStarGuestUpdateArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelGuestStarSessionBegin(object sender, ChannelGuestStarSessionBegin args)
    {
        logger.LogDebug("OnChannelUpdate: {@ChannelGuestStarSessionBegin}", args);
        return Task.CompletedTask;
    }

    private Task OnChannelGuestStarSessionEnd(object sender, ChannelGuestStarSessionEnd args)
    {
        logger.LogDebug("OnChannelUpdate: {@ChannelGuestStarSessionEnd}", args);
        return Task.CompletedTask;
    }

    private Task OnChannelGuestStarSettingsUpdate(object sender, ChannelGuestStarSettingsUpdateArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelGuestStarSlotUpdate(object sender, ChannelGuestStarSlotUpdateArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelHypeTrainBegin(object sender, ChannelHypeTrainBeginArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelHypeTrainEnd(object sender, ChannelHypeTrainEndArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelModeratorAdd(object sender, ChannelModeratorArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelModeratorRemove(object sender, ChannelModeratorArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPointsAutomaticRewardRedemption(object sender, ChannelPointsAutomaticRewardRedemptionArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPointsCustomRewardAdd(object sender, ChannelPointsCustomRewardArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPointsCustomRewardRedemption(object sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        logger.LogDebug("OnChannelPointsCustomRewardRedemption: {@Notification}", args.Notification);
        var command = new ProcessRewardRedeemCommand(args.Notification.Payload.Event.Reward, args.Notification.Payload.Event.UserInput);
        mediator.Send(command);
        return Task.CompletedTask;
    }

    private Task OnChannelPointsCustomRewardRedemptionUpdate(object sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPointsCustomRewardRemove(object sender, ChannelPointsCustomRewardArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPointsCustomRewardUpdate(object sender, ChannelPointsCustomRewardArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPollBegin(object sender, ChannelPollBeginArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPollEnd(object sender, ChannelPollEndArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPollProgress(object sender, ChannelPollProgressArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPredictionBegin(object sender, ChannelPredictionBeginArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPredictionEnd(object sender, ChannelPredictionEndArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPredictionLock(object sender, ChannelPredictionLockArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelPredictionProgress(object sender, ChannelPredictionProgressArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelRaid(object sender, ChannelRaidArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelShieldModeBegin(object sender, ChannelShieldModeBeginArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelShieldModeEnd(object sender, ChannelShieldModeEndArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelShoutoutCreate(object sender, ChannelShoutoutCreateArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelShoutoutReceive(object sender, ChannelShoutoutReceiveArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
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

    private Task OnChannelSubscriptionEnd(object sender, ChannelSubscriptionEndArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelSubscriptionGift(object sender, ChannelSubscriptionGiftArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
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

    private Task OnChannelSuspiciousUserMessage(object sender, ChannelSuspiciousUserMessageArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelSuspiciousUserUpdate(object sender, ChannelSuspiciousUserUpdateArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelUnban(object sender, ChannelUnbanArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelUpdate(object sender, ChannelUpdateArgs args)
    {
        logger.LogDebug("OnChannelUpdate: {@Notification}", args.Notification);
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

    private Task OnChannelWarningAcknowledge(object sender, ChannelWarningAcknowledgeArgs args)
    {
        logger.LogDebug("OnChannelWarningAcknowledge: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelWarningSend(object sender, ChannelWarningSendArgs args)
    {
        logger.LogDebug("OnChannelWarningSend: {@Notification}", args.Notification);
        return Task.CompletedTask;
    }

    private Task OnChannelChatMessage(object sender, ChannelChatMessageArgs args)
    {
        logger.LogDebug("OnChannelChatMessage: {@Notification}", args.Notification);
        return Task.CompletedTask;
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

    private Task OnChannelVipRemove(object sender, ChannelVipArgs args)
    {
        logger.LogInformation("WebSocket ChannelVipRemove");
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

        var usersResponse = await twitchApi.Helix.Users.GetUsersAsync(ids: null, logins: ["andibanterbot"], accessToken);
        botUserId = usersResponse.Users.First().Id;

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
            var conditions = new Dictionary<string, string>()
            {
                { "broadcaster_user_id", broadcasterId },
            };

            await SubscribeToEvent("channel.follow", "2", new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", broadcasterId },
                    { "moderator_user_id", broadcasterId }
                });

            await SubscribeToEvent("channel.vip.add", "1", conditions);
            await SubscribeToEvent("channel.vip.remove", "1", conditions);

            await SubscribeToEvent("channel.ad_break.begin", "1", conditions);

            await SubscribeToEvent("stream.online", "1", conditions);
            await SubscribeToEvent("stream.offline", "1", conditions);

            await SubscribeToEvent("channel.subscribe", "1", conditions);
            await SubscribeToEvent("channel.subscription.message", "1", conditions);

            await SubscribeToEvent("channel.channel_points_custom_reward_redemption.add", "1", conditions);

            await SubscribeToEvent("channel.chat.message", "1", new Dictionary<string, string>()
                {
                    { "broadcaster_user_id", broadcasterId },
                    { "user_id", broadcasterId }
                });
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

    private async Task SubscribeToEvent(string eventType, string version, Dictionary<string, string> conditions)
    {
        try
        {
            await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                eventType,
                version,
                conditions,
                EventSubTransportMethod.Websocket,
                websocketSessionId: eventSubWebsocketClient.SessionId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{EventType} subscription failed", eventType);
        }
    }
}
