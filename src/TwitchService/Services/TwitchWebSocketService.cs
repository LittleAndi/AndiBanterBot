namespace Application.Features.Twitch;

public interface ITwitchWebSocketService
{
    Task Start(TwitchUserRole role, CancellationToken cancellationToken = default);
    Task CloseAsync(TwitchUserRole role, CancellationToken cancellationToken = default);
    Task SubscribeToBroadcasterSubscriptions(CancellationToken cancellationToken = default);
    TwitchWebSocketStatus GetStatus(TwitchUserRole role);
    StreamStatusSnapshot GetStreamStatus();
    event EventHandler<ChatMessageEvent>? ChatMessageReceived;
    event EventHandler<RewardRedemptionEvent>? RewardRedemptionReceived;
    event EventHandler<StreamStatusChangedEvent>? StreamStatusChanged;
    event EventHandler<RaidEvent>? RaidReceived;
    event EventHandler<FollowEvent>? FollowReceived;
    event EventHandler<SubscribeEvent>? SubscribeReceived;
    event EventHandler<SubscriptionGiftEvent>? SubscriptionGiftReceived;
    event EventHandler<SubscriptionMessageEvent>? SubscriptionMessageReceived;
    event EventHandler<CheerEvent>? CheerReceived;
}

public record TwitchWebSocketStatus(bool Connected, string SessionId, DateTime LastMessageAtUtc, TimeSpan KeepaliveTimeout);

// Twitch ties a WebSocket session's identity to whichever user token authorized its
// first subscription: mixing subscriptions from two different user tokens on one
// session fails with "websocket transport cannot have subscriptions created by
// different users". Chat (channel.chat.message) is authorized by the Bot token; every
// other subscription is authorized by the Broadcaster token. So this service runs two
// entirely independent WebSocket sessions, one per identity, each with its own
// connection/session/reconnect state.
public class TwitchWebSocketService(
    [FromKeyedServices(TwitchUserRole.Bot)] IWebSocketClient botWebSocketClient,
    [FromKeyedServices(TwitchUserRole.Broadcaster)] IWebSocketClient broadcasterWebSocketClient,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ITwitchTokenStore tokenStore,
    ITwitchUserApi twitchUserApi,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<TwitchWebSocketService> logger) : ITwitchWebSocketService
{
    private readonly HttpClient twitchHttpClientUserAccess = httpClientFactory.CreateClient("TwitchClientUserAccess");
    private readonly HttpClient twitchHttpClientAppAccess = httpClientFactory.CreateClient("TwitchClientAppAccess");
    private readonly string broadcasterUsername = configuration["Twitch:BroadcasterUsername"] ?? throw new InvalidOperationException("BroadcasterUsername not configured");
    private readonly string monitoredUsername = configuration["Twitch:MonitoredUsername"] ?? throw new InvalidOperationException("MonitoredUsername not configured");
    private const int KeepaliveTimeoutSeconds = 60;
    private static readonly string DefaultWebSocketUrl = $"wss://eventsub.wss.twitch.tv/ws?keepalive_timeout_seconds={KeepaliveTimeoutSeconds}";

    private readonly EventSubConnection bot = new(TwitchUserRole.Bot, botWebSocketClient);
    private readonly EventSubConnection broadcaster = new(TwitchUserRole.Broadcaster, broadcasterWebSocketClient);

    private readonly SemaphoreSlim userIdsGate = new(1, 1);
    private bool userIdsInitialized = false;
    private string? broadcasterId;
    private string? userId;
    private StreamStatusSnapshot streamStatus = StreamStatusSnapshot.Unknown;

    // Each entry is a self-contained addition: a new subscription type gets its own
    // handler method and its own line here, instead of a shared branch everyone touches.
    // Shared across both connections: chat notifications only ever arrive on the bot
    // connection and every other type only ever arrives on the broadcaster connection,
    // so dispatch-by-type works the same regardless of which connection received it.
    private Dictionary<string, Action<string>> notificationHandlers = [];
    private bool notificationHandlersBuilt = false;

    public event EventHandler<ChatMessageEvent>? ChatMessageReceived;
    public event EventHandler<RewardRedemptionEvent>? RewardRedemptionReceived;
    public event EventHandler<StreamStatusChangedEvent>? StreamStatusChanged;
    public event EventHandler<RaidEvent>? RaidReceived;
    public event EventHandler<FollowEvent>? FollowReceived;
    public event EventHandler<SubscribeEvent>? SubscribeReceived;
    public event EventHandler<SubscriptionGiftEvent>? SubscriptionGiftReceived;
    public event EventHandler<SubscriptionMessageEvent>? SubscriptionMessageReceived;
    public event EventHandler<CheerEvent>? CheerReceived;

    // Per-connection mutable state. Everything that used to be an instance field tracking
    // "the" connection now lives here once per identity (Bot, Broadcaster).
    private sealed class EventSubConnection(TwitchUserRole role, IWebSocketClient client)
    {
        public TwitchUserRole Role { get; } = role;
        public IWebSocketClient Client { get; } = client;
        public SemaphoreSlim StartGate { get; } = new(1, 1);
        public bool ConnectingOrConnected { get; set; }
        public bool HandlersAttached { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string? PendingReconnectUrl { get; set; }
        public bool ResumingSession { get; set; }
        public DateTime LastMessageAtUtc { get; set; } = DateTime.MinValue;
    }

    private EventSubConnection GetConnection(TwitchUserRole role) => role switch
    {
        TwitchUserRole.Bot => bot,
        TwitchUserRole.Broadcaster => broadcaster,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public async Task Start(TwitchUserRole role, CancellationToken cancellationToken = default)
    {
        if (!tokenStore.HasToken(role))
        {
            logger.LogWarning("No {Role} token stored, skipping websocket start. Log in with the {Role} account to enable it.", role, role);
            return;
        }

        var connection = GetConnection(role);

        await connection.StartGate.WaitAsync(cancellationToken);
        try
        {
            if (connection.ConnectingOrConnected) return;

            await InitializeUserIds(cancellationToken);

            if (!connection.HandlersAttached)
            {
                EnsureNotificationHandlersBuilt();
                AttachHandlers(connection);
                connection.HandlersAttached = true;
            }

            // A session_reconnect message from Twitch supplies a one-shot URL that resumes
            // the session with its subscriptions intact
            var url = connection.PendingReconnectUrl ?? DefaultWebSocketUrl;
            connection.ResumingSession = connection.PendingReconnectUrl is not null;
            connection.PendingReconnectUrl = null;

            await connection.Client.ConnectAsync(url, cancellationToken);
            connection.ConnectingOrConnected = true;
            connection.LastMessageAtUtc = DateTime.UtcNow;

            _ = ReceiveLoopAsync(connection, hostApplicationLifetime.ApplicationStopping);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error starting {Role} EventSub websocket", connection.Role);
            connection.ConnectingOrConnected = false;
            throw;
        }
        finally
        {
            connection.StartGate.Release();
        }
    }

    public async Task CloseAsync(TwitchUserRole role, CancellationToken cancellationToken = default)
    {
        await GetConnection(role).Client.CloseAsync(cancellationToken);
    }

    public TwitchWebSocketStatus GetStatus(TwitchUserRole role)
    {
        var connection = GetConnection(role);
        return new TwitchWebSocketStatus(connection.ConnectingOrConnected, connection.SessionId, connection.LastMessageAtUtc, TimeSpan.FromSeconds(KeepaliveTimeoutSeconds));
    }

    public StreamStatusSnapshot GetStreamStatus() => streamStatus;

    private void EnsureNotificationHandlersBuilt()
    {
        if (notificationHandlersBuilt) return;

        notificationHandlers = new Dictionary<string, Action<string>>
        {
            ["channel.chat.message"] = HandleChatMessage,
            ["channel.channel_points_custom_reward_redemption.add"] = HandleRewardRedemption,
            ["stream.online"] = HandleStreamOnline,
            ["stream.offline"] = HandleStreamOffline,
            ["channel.raid"] = HandleRaid,
            ["channel.follow"] = HandleFollow,
            ["channel.subscribe"] = HandleSubscribe,
            ["channel.subscription.gift"] = HandleSubscriptionGift,
            ["channel.subscription.message"] = HandleSubscriptionMessage,
            ["channel.cheer"] = HandleCheer,
        };
        notificationHandlersBuilt = true;
    }

    private void AttachHandlers(EventSubConnection connection)
    {
        // Subscriptions created from the welcome message must survive the caller's
        // request, so they are tied to application shutdown instead
        var stoppingToken = hostApplicationLifetime.ApplicationStopping;
        connection.Client.OnWelcomeReceived += async (sender, e) => await HandleWelcomeMessage(connection, sender, e, stoppingToken).ConfigureAwait(false);
        connection.Client.OnMessageReceived += async (sender, e) => await HandleMessageReceived(connection, sender, e).ConfigureAwait(false);
        connection.Client.OnCloseReceived += (sender, e) => logger.LogWarning("{Role} WebSocket connection closed", connection.Role);
    }

    private async Task ReceiveLoopAsync(EventSubConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await connection.Client.ReceiveMessagesAsync(cancellationToken);
        }
        finally
        {
            connection.ConnectingOrConnected = false;
            connection.SessionId = string.Empty;
            logger.LogWarning("{Role} EventSub receive loop ended, a new Start call is required to reconnect", connection.Role);
        }
    }

    private async Task InitializeUserIds(CancellationToken cancellationToken)
    {
        if (userIdsInitialized) return;

        await userIdsGate.WaitAsync(cancellationToken);
        try
        {
            if (userIdsInitialized) return;

            broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
            userId = await twitchUserApi.GetUserIdAsync(monitoredUsername, cancellationToken);

            if (string.IsNullOrEmpty(broadcasterId) || string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException("Failed to get user IDs from usernames");
            }

            userIdsInitialized = true;
        }
        finally
        {
            userIdsGate.Release();
        }
    }

    private async Task HandleWelcomeMessage(EventSubConnection connection, object? sender, TwitchMessageEventArgs e, CancellationToken cancellationToken)
    {
        connection.ConnectingOrConnected = true;

        logger.LogInformation("{Role} welcome received: {TwitchMessageEventArgs}", connection.Role, e.RawMessage);

        connection.SessionId = e.Response?.Payload.Session.Id ?? string.Empty;
        if (string.IsNullOrEmpty(connection.SessionId))
        {
            logger.LogError("{Role} session ID is null or empty", connection.Role);
            return;
        }

        if (connection.ResumingSession)
        {
            connection.ResumingSession = false;
            logger.LogInformation("{Role} session resumed after reconnect, existing subscriptions are preserved", connection.Role);
            return;
        }

        if (connection.Role == TwitchUserRole.Bot)
        {
            await SubscribeToChannelChatMessages(connection.SessionId, cancellationToken);
        }
        else
        {
            await SubscribeToChannelPointsRedemption(connection.SessionId, cancellationToken);
            await SubscribeToStreamOnline(connection.SessionId, cancellationToken);
            await SubscribeToStreamOffline(connection.SessionId, cancellationToken);
            await RefreshStreamStatusAsync(cancellationToken);
            await SubscribeToChannelRaid(connection.SessionId, cancellationToken);
            await SubscribeToChannelFollow(connection.SessionId, cancellationToken);
            await SubscribeToChannelSubscribe(connection.SessionId, cancellationToken);
            await SubscribeToChannelSubscriptionGift(connection.SessionId, cancellationToken);
            await SubscribeToChannelSubscriptionMessage(connection.SessionId, cancellationToken);
            await SubscribeToChannelCheer(connection.SessionId, cancellationToken);
        }
    }

    private async Task HandleMessageReceived(EventSubConnection connection, object? sender, TwitchMessageEventArgs e)
    {
        connection.LastMessageAtUtc = DateTime.UtcNow;

        logger.LogInformation("{Role} message received: {TwitchMessageEventArgs}", connection.Role, e.RawMessage);

        if (e.Response?.Metadata.MessageType == "session_reconnect")
        {
            var reconnectUrl = e.Response.Payload.Session?.ReconnectUrl;
            if (!string.IsNullOrEmpty(reconnectUrl))
            {
                logger.LogInformation("Twitch requested a {Role} session reconnect, moving to new edge server", connection.Role);
                connection.PendingReconnectUrl = reconnectUrl;
                // Closing ends the receive loop; the supervisor then reconnects using the pending URL
                await connection.Client.CloseAsync();
            }
            else
            {
                logger.LogError("{Role} session_reconnect message without a reconnect URL", connection.Role);
            }
            return;
        }

        // Notifications carry the event type in metadata.subscription_type,
        // metadata.message_type is always "notification"
        if (e.Response?.Metadata.MessageType == "notification")
        {
            var subscriptionType = e.Response.Metadata.SubscriptionType;
            if (subscriptionType is not null && notificationHandlers.TryGetValue(subscriptionType, out var handler))
            {
                handler(e.RawMessage);
            }
            else
            {
                logger.LogWarning("No notification handler registered for subscription type {SubscriptionType}", subscriptionType);
            }
        }
    }

    private void HandleChatMessage(string rawMessage)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<ChatMessageNotification>(rawMessage);
            var chatMessage = notification?.Payload.Event;
            if (chatMessage is null)
            {
                logger.LogWarning("channel.chat.message notification without an event payload");
                return;
            }

            logger.LogInformation("Chat #{Broadcaster} {Chatter}: {Text}",
                chatMessage.BroadcasterUserLogin, chatMessage.ChatterUserName, chatMessage.Message.Text);

            ChatMessageReceived?.Invoke(this, chatMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse channel.chat.message notification");
        }
    }

    private void HandleRewardRedemption(string rawMessage)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<RewardRedemptionNotification>(rawMessage);
            var redemption = notification?.Payload.Event;
            if (redemption is null)
            {
                logger.LogWarning("channel.channel_points_custom_reward_redemption.add notification without an event payload");
                return;
            }

            logger.LogInformation("Reward redeemed #{Broadcaster} {User}: {Reward}",
                redemption.BroadcasterUserLogin, redemption.UserLogin, redemption.Reward.Title);

            RewardRedemptionReceived?.Invoke(this, redemption);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse channel.channel_points_custom_reward_redemption.add notification");
        }
    }

    private void HandleStreamOnline(string rawMessage)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<StreamOnlineNotification>(rawMessage);
            var streamOnline = notification?.Payload.Event;
            if (streamOnline is null)
            {
                logger.LogWarning("stream.online notification without an event payload");
                return;
            }

            logger.LogInformation("Stream online #{Broadcaster} (type: {Type})", streamOnline.BroadcasterUserLogin, streamOnline.Type);

            streamStatus = new StreamStatusSnapshot(true, streamOnline.StartedAt);

            StreamStatusChanged?.Invoke(this, new StreamStatusChangedEvent(
                IsLive: true,
                BroadcasterUserId: streamOnline.BroadcasterUserId,
                BroadcasterUserLogin: streamOnline.BroadcasterUserLogin,
                BroadcasterUserName: streamOnline.BroadcasterUserName,
                StartedAt: streamOnline.StartedAt));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse stream.online notification");
        }
    }

    private void HandleStreamOffline(string rawMessage)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<StreamOfflineNotification>(rawMessage);
            var streamOffline = notification?.Payload.Event;
            if (streamOffline is null)
            {
                logger.LogWarning("stream.offline notification without an event payload");
                return;
            }

            logger.LogInformation("Stream offline #{Broadcaster}", streamOffline.BroadcasterUserLogin);

            streamStatus = new StreamStatusSnapshot(false, null);

            StreamStatusChanged?.Invoke(this, new StreamStatusChangedEvent(
                IsLive: false,
                BroadcasterUserId: streamOffline.BroadcasterUserId,
                BroadcasterUserLogin: streamOffline.BroadcasterUserLogin,
                BroadcasterUserName: streamOffline.BroadcasterUserName,
                StartedAt: null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse stream.offline notification");
        }
    }

    private void HandleRaid(string rawMessage)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<RaidNotification>(rawMessage);
            var raid = notification?.Payload.Event;
            if (raid is null)
            {
                logger.LogWarning("channel.raid notification without an event payload");
                return;
            }

            logger.LogInformation("Raid #{ToBroadcaster} from {FromBroadcaster} ({Viewers} viewers)",
                raid.ToBroadcasterUserLogin, raid.FromBroadcasterUserLogin, raid.Viewers);

            RaidReceived?.Invoke(this, raid);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse channel.raid notification");
        }
    }

    private void HandleFollow(string rawMessage)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<FollowNotification>(rawMessage);
            var follow = notification?.Payload.Event;
            if (follow is null)
            {
                logger.LogWarning("channel.follow notification without an event payload");
                return;
            }

            logger.LogInformation("New follower #{Broadcaster}: {User}", follow.BroadcasterUserLogin, follow.UserLogin);

            FollowReceived?.Invoke(this, follow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse channel.follow notification");
        }
    }

    private void HandleSubscribe(string rawMessage)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<SubscribeNotification>(rawMessage);
            var subscribe = notification?.Payload.Event;
            if (subscribe is null)
            {
                logger.LogWarning("channel.subscribe notification without an event payload");
                return;
            }

            logger.LogInformation("New subscriber #{Broadcaster}: {User} (tier {Tier}, gift: {IsGift})",
                subscribe.BroadcasterUserLogin, subscribe.UserLogin, subscribe.Tier, subscribe.IsGift);

            SubscribeReceived?.Invoke(this, subscribe);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse channel.subscribe notification");
        }
    }

    private void HandleSubscriptionGift(string rawMessage)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<SubscriptionGiftNotification>(rawMessage);
            var gift = notification?.Payload.Event;
            if (gift is null)
            {
                logger.LogWarning("channel.subscription.gift notification without an event payload");
                return;
            }

            logger.LogInformation("Gift subs #{Broadcaster}: {User} gifted {Total} (tier {Tier})",
                gift.BroadcasterUserLogin, gift.IsAnonymous ? "Anonymous" : gift.UserLogin, gift.Total, gift.Tier);

            SubscriptionGiftReceived?.Invoke(this, gift);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse channel.subscription.gift notification");
        }
    }

    private void HandleSubscriptionMessage(string rawMessage)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<SubscriptionMessageNotification>(rawMessage);
            var resub = notification?.Payload.Event;
            if (resub is null)
            {
                logger.LogWarning("channel.subscription.message notification without an event payload");
                return;
            }

            logger.LogInformation("Resub #{Broadcaster}: {User} ({CumulativeMonths} months, tier {Tier})",
                resub.BroadcasterUserLogin, resub.UserLogin, resub.CumulativeMonths, resub.Tier);

            SubscriptionMessageReceived?.Invoke(this, resub);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse channel.subscription.message notification");
        }
    }

    private void HandleCheer(string rawMessage)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<CheerNotification>(rawMessage);
            var cheer = notification?.Payload.Event;
            if (cheer is null)
            {
                logger.LogWarning("channel.cheer notification without an event payload");
                return;
            }

            logger.LogInformation("Cheer #{Broadcaster}: {User} ({Bits} bits)",
                cheer.BroadcasterUserLogin, cheer.IsAnonymous ? "Anonymous" : cheer.UserLogin, cheer.Bits);

            CheerReceived?.Invoke(this, cheer);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse channel.cheer notification");
        }
    }

    // Twitch rejects EventSub subscriptions created over WebSocket transport when
    // authenticated with an app access token ("invalid transport and auth combination"),
    // even with the broadcaster's channel:bot and the bot's user:bot grants in place.
    // The app-access model only applies to webhook-transport subscriptions and to plain
    // Helix calls (e.g. chat send); WebSocket-delivered subscriptions require a user token.
    private async Task SubscribeToChannelChatMessages(string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionRequest = new
            {
                type = "channel.chat.message",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = broadcasterId,
                    user_id = userId
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "helix/eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscriptionRequest)
            };
            request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Bot);
            var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, content);
                return;
            }

            logger.LogInformation("Successfully created chat message subscription. Response: {Response}", content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating chat message subscription");
        }
    }

    // channel.channel_points_custom_reward_redemption.add
    private async Task SubscribeToChannelPointsRedemption(string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionRequest = new
            {
                type = "channel.channel_points_custom_reward_redemption.add",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = broadcasterId,
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId,
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "helix/eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscriptionRequest)
            };
            request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);
            var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, content);
                return;
            }

            logger.LogInformation("Successfully created channel points redemption subscription. Response: {Response}", content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating channel points redemption subscription");
        }
    }

    private async Task SubscribeToStreamOnline(string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionRequest = new
            {
                type = "stream.online",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = broadcasterId,
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId,
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "helix/eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscriptionRequest)
            };
            request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);
            var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, content);
                return;
            }

            logger.LogInformation("Successfully created stream.online subscription. Response: {Response}", content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating stream.online subscription");
        }
    }

    private async Task SubscribeToStreamOffline(string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionRequest = new
            {
                type = "stream.offline",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = broadcasterId,
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId,
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "helix/eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscriptionRequest)
            };
            request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);
            var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, content);
                return;
            }

            logger.LogInformation("Successfully created stream.offline subscription. Response: {Response}", content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating stream.offline subscription");
        }
    }

    // Condition uses to_broadcaster_user_id (raids received by this channel), not
    // broadcaster_user_id like the other subscriptions. No extra scope required beyond
    // the broadcaster user token already used for WebSocket-transport subscriptions.
    private async Task SubscribeToChannelRaid(string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionRequest = new
            {
                type = "channel.raid",
                version = "1",
                condition = new
                {
                    to_broadcaster_user_id = broadcasterId,
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId,
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "helix/eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscriptionRequest)
            };
            request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);
            var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, content);
                return;
            }

            logger.LogInformation("Successfully created channel.raid subscription. Response: {Response}", content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating channel.raid subscription");
        }
    }

    // v2 requires moderator:read:followers on the token used to create the subscription
    // and a moderator_user_id in the condition. The broadcaster is a moderator of their
    // own channel, so their own user id satisfies both the condition and the token's scope.
    private async Task SubscribeToChannelFollow(string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionRequest = new
            {
                type = "channel.follow",
                version = "2",
                condition = new
                {
                    broadcaster_user_id = broadcasterId,
                    moderator_user_id = broadcasterId,
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId,
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "helix/eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscriptionRequest)
            };
            request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);
            var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, content);
                return;
            }

            logger.LogInformation("Successfully created channel.follow subscription. Response: {Response}", content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating channel.follow subscription");
        }
    }

    private async Task SubscribeToChannelSubscribe(string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionRequest = new
            {
                type = "channel.subscribe",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = broadcasterId,
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId,
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "helix/eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscriptionRequest)
            };
            request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);
            var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, content);
                return;
            }

            logger.LogInformation("Successfully created channel.subscribe subscription. Response: {Response}", content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating channel.subscribe subscription");
        }
    }

    private async Task SubscribeToChannelSubscriptionGift(string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionRequest = new
            {
                type = "channel.subscription.gift",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = broadcasterId,
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId,
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "helix/eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscriptionRequest)
            };
            request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);
            var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, content);
                return;
            }

            logger.LogInformation("Successfully created channel.subscription.gift subscription. Response: {Response}", content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating channel.subscription.gift subscription");
        }
    }

    private async Task SubscribeToChannelSubscriptionMessage(string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionRequest = new
            {
                type = "channel.subscription.message",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = broadcasterId,
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId,
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "helix/eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscriptionRequest)
            };
            request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);
            var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, content);
                return;
            }

            logger.LogInformation("Successfully created channel.subscription.message subscription. Response: {Response}", content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating channel.subscription.message subscription");
        }
    }

    // Requires bits:read on the broadcaster token.
    private async Task SubscribeToChannelCheer(string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionRequest = new
            {
                type = "channel.cheer",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = broadcasterId,
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId,
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "helix/eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscriptionRequest)
            };
            request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Broadcaster);
            var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, content);
                return;
            }

            logger.LogInformation("Successfully created channel.cheer subscription. Response: {Response}", content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating channel.cheer subscription");
        }
    }

    // stream.online/offline only fire on transitions, so a stream already live when the
    // service starts (or reconnects) would otherwise never be reflected. This Helix lookup
    // seeds the initial state; the EventSub handlers keep it current from there.
    private async Task RefreshStreamStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await twitchHttpClientAppAccess.GetAsync($"helix/streams?user_id={broadcasterId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to refresh stream status. Status: {StatusCode}", response.StatusCode);
                return;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            var data = doc.RootElement.GetProperty("data");

            streamStatus = data.GetArrayLength() == 0
                ? new StreamStatusSnapshot(false, null)
                : new StreamStatusSnapshot(true, data[0].GetProperty("started_at").GetDateTimeOffset());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing stream status");
        }
    }

    public async Task SubscribeToBroadcasterSubscriptions(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(broadcaster.SessionId))
        {
            logger.LogInformation("Broadcaster WebSocket session not established yet, broadcaster subscriptions will be created on welcome");
            return;
        }

        await SubscribeToChannelPointsRedemption(sessionId: broadcaster.SessionId, cancellationToken);
        await SubscribeToStreamOnline(sessionId: broadcaster.SessionId, cancellationToken);
        await SubscribeToStreamOffline(sessionId: broadcaster.SessionId, cancellationToken);
        await RefreshStreamStatusAsync(cancellationToken);
        await SubscribeToChannelRaid(sessionId: broadcaster.SessionId, cancellationToken);
        await SubscribeToChannelFollow(sessionId: broadcaster.SessionId, cancellationToken);
        await SubscribeToChannelSubscribe(sessionId: broadcaster.SessionId, cancellationToken);
        await SubscribeToChannelSubscriptionGift(sessionId: broadcaster.SessionId, cancellationToken);
        await SubscribeToChannelSubscriptionMessage(sessionId: broadcaster.SessionId, cancellationToken);
        await SubscribeToChannelCheer(sessionId: broadcaster.SessionId, cancellationToken);
    }
}
