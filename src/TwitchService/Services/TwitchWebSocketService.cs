namespace Application.Features.Twitch;

public interface ITwitchWebSocketService
{
    Task Start(CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    Task SubscribeToBroadcasterSubscriptions(CancellationToken cancellationToken = default);
    TwitchWebSocketStatus GetStatus();
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

public class TwitchWebSocketService(
    IWebSocketClient webSocketClient,
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

    private readonly SemaphoreSlim startGate = new(1, 1);
    private string? broadcasterId;
    private string? userId;
    private bool connectingOrConnected = false;
    private bool handlersAttached = false;
    private string sessionId = string.Empty;
    private string? pendingReconnectUrl;
    private bool resumingSession = false;
    private DateTime lastMessageAtUtc = DateTime.MinValue;
    private StreamStatusSnapshot streamStatus = StreamStatusSnapshot.Unknown;

    // Each entry is a self-contained addition: a new subscription type gets its own
    // handler method and its own line here, instead of a shared branch everyone touches.
    // Built in Start() rather than a field initializer, since primary-constructor field
    // initializers can't reference instance methods.
    private Dictionary<string, Action<string>> notificationHandlers = [];

    public event EventHandler<ChatMessageEvent>? ChatMessageReceived;
    public event EventHandler<RewardRedemptionEvent>? RewardRedemptionReceived;
    public event EventHandler<StreamStatusChangedEvent>? StreamStatusChanged;
    public event EventHandler<RaidEvent>? RaidReceived;
    public event EventHandler<FollowEvent>? FollowReceived;
    public event EventHandler<SubscribeEvent>? SubscribeReceived;
    public event EventHandler<SubscriptionGiftEvent>? SubscriptionGiftReceived;
    public event EventHandler<SubscriptionMessageEvent>? SubscriptionMessageReceived;
    public event EventHandler<CheerEvent>? CheerReceived;

    public async Task Start(CancellationToken cancellationToken = default)
    {
        await startGate.WaitAsync(cancellationToken);
        try
        {
            if (connectingOrConnected) return;

            // Get user IDs before starting
            await InitializeUserIds(cancellationToken);

            if (!handlersAttached)
            {
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

                // Subscriptions created from the welcome message must survive the caller's
                // request, so they are tied to application shutdown instead
                var stoppingToken = hostApplicationLifetime.ApplicationStopping;
                webSocketClient.OnWelcomeReceived += async (sender, e) => await HandleWelcomeMessage(sender, e, stoppingToken).ConfigureAwait(false);
                webSocketClient.OnMessageReceived += async (sender, e) => await HandleMessageReceived(sender, e).ConfigureAwait(false);
                webSocketClient.OnCloseReceived += HandleCloseMessage;
                handlersAttached = true;
            }

            // A session_reconnect message from Twitch supplies a one-shot URL that resumes
            // the session with its subscriptions intact
            var url = pendingReconnectUrl ?? DefaultWebSocketUrl;
            resumingSession = pendingReconnectUrl is not null;
            pendingReconnectUrl = null;

            await webSocketClient.ConnectAsync(url, cancellationToken);
            connectingOrConnected = true;
            lastMessageAtUtc = DateTime.UtcNow;

            _ = ReceiveLoopAsync(hostApplicationLifetime.ApplicationStopping);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error starting TwitchWebSocketService");
            connectingOrConnected = false;
            throw;
        }
        finally
        {
            startGate.Release();
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await webSocketClient.CloseAsync(cancellationToken);
    }

    public TwitchWebSocketStatus GetStatus()
    {
        return new TwitchWebSocketStatus(connectingOrConnected, sessionId, lastMessageAtUtc, TimeSpan.FromSeconds(KeepaliveTimeoutSeconds));
    }

    public StreamStatusSnapshot GetStreamStatus() => streamStatus;

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await webSocketClient.ReceiveMessagesAsync(cancellationToken);
        }
        finally
        {
            connectingOrConnected = false;
            sessionId = string.Empty;
            logger.LogWarning("EventSub receive loop ended, a new Start call is required to reconnect");
        }
    }

    private void HandleCloseMessage(object? sender, EventArgs e)
    {
        logger.LogWarning("WebSocket connection closed");
    }

    private async Task InitializeUserIds(CancellationToken cancellationToken)
    {
        broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        userId = await twitchUserApi.GetUserIdAsync(monitoredUsername, cancellationToken);

        if (string.IsNullOrEmpty(broadcasterId) || string.IsNullOrEmpty(userId))
        {
            throw new InvalidOperationException("Failed to get user IDs from usernames");
        }
    }

    private async Task HandleWelcomeMessage(object? sender, TwitchMessageEventArgs e, CancellationToken cancellationToken)
    {
        connectingOrConnected = true;

        logger.LogInformation("Welcome received: {TwitchMessageEventArgs}", e.RawMessage);

        sessionId = e.Response?.Payload.Session.Id ?? string.Empty;
        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogError("Session ID is null or empty");
            return;
        }

        if (resumingSession)
        {
            resumingSession = false;
            logger.LogInformation("Session resumed after reconnect, existing subscriptions are preserved");
            return;
        }

        if (tokenStore.HasToken(TwitchUserRole.Bot))
        {
            await SubscribeToChannelChatMessages(sessionId, cancellationToken);
        }
        else
        {
            logger.LogWarning("No bot token stored, skipping chat message subscription. Log in with the bot account to enable it.");
        }

        if (tokenStore.HasToken(TwitchUserRole.Broadcaster))
        {
            await SubscribeToChannelPointsRedemption(sessionId, cancellationToken);
            await SubscribeToStreamOnline(sessionId, cancellationToken);
            await SubscribeToStreamOffline(sessionId, cancellationToken);
            await RefreshStreamStatusAsync(cancellationToken);
            await SubscribeToChannelRaid(sessionId, cancellationToken);
            await SubscribeToChannelFollow(sessionId, cancellationToken);
            await SubscribeToChannelSubscribe(sessionId, cancellationToken);
            await SubscribeToChannelSubscriptionGift(sessionId, cancellationToken);
            await SubscribeToChannelSubscriptionMessage(sessionId, cancellationToken);
            await SubscribeToChannelCheer(sessionId, cancellationToken);
        }
        else
        {
            logger.LogWarning("No broadcaster token stored, skipping channel points, stream status, raid, follow, subscription, and cheer subscriptions. Log in with the broadcaster account to enable them.");
        }
    }

    private async Task HandleMessageReceived(object? sender, TwitchMessageEventArgs e)
    {
        lastMessageAtUtc = DateTime.UtcNow;

        logger.LogInformation("Message received: {TwitchMessageEventArgs}", e.RawMessage);

        if (e.Response?.Metadata.MessageType == "session_reconnect")
        {
            var reconnectUrl = e.Response.Payload.Session?.ReconnectUrl;
            if (!string.IsNullOrEmpty(reconnectUrl))
            {
                logger.LogInformation("Twitch requested a session reconnect, moving to new edge server");
                pendingReconnectUrl = reconnectUrl;
                // Closing ends the receive loop; the supervisor then reconnects using the pending URL
                await webSocketClient.CloseAsync();
            }
            else
            {
                logger.LogError("session_reconnect message without a reconnect URL");
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
        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogInformation("WebSocket session not established yet, broadcaster subscriptions will be created on welcome");
            return;
        }

        await SubscribeToChannelPointsRedemption(sessionId: sessionId, cancellationToken);
        await SubscribeToStreamOnline(sessionId: sessionId, cancellationToken);
        await SubscribeToStreamOffline(sessionId: sessionId, cancellationToken);
        await RefreshStreamStatusAsync(cancellationToken);
        await SubscribeToChannelRaid(sessionId: sessionId, cancellationToken);
        await SubscribeToChannelFollow(sessionId: sessionId, cancellationToken);
        await SubscribeToChannelSubscribe(sessionId: sessionId, cancellationToken);
        await SubscribeToChannelSubscriptionGift(sessionId: sessionId, cancellationToken);
        await SubscribeToChannelSubscriptionMessage(sessionId: sessionId, cancellationToken);
        await SubscribeToChannelCheer(sessionId: sessionId, cancellationToken);
    }
}
