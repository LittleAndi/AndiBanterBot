namespace Application.Features.Twitch;

public interface ITwitchWebSocketService
{
    Task Start(CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    Task SubscribeToBroadcasterSubscriptions(CancellationToken cancellationToken = default);
    TwitchWebSocketStatus GetStatus();
    event EventHandler<ChatMessageEvent>? ChatMessageReceived;
    event EventHandler<RewardRedemptionEvent>? RewardRedemptionReceived;
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

    public event EventHandler<ChatMessageEvent>? ChatMessageReceived;
    public event EventHandler<RewardRedemptionEvent>? RewardRedemptionReceived;

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
        }
        else
        {
            logger.LogWarning("No broadcaster token stored, skipping channel points subscription. Log in with the broadcaster account to enable it.");
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
            switch (e.Response.Metadata.SubscriptionType)
            {
                case "channel.chat.message":
                    HandleChatMessage(e.RawMessage);
                    break;
                case "channel.channel_points_custom_reward_redemption.add":
                    HandleRewardRedemption(e.RawMessage);
                    break;
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

    public async Task SubscribeToBroadcasterSubscriptions(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogInformation("WebSocket session not established yet, channel points subscription will be created on welcome");
            return;
        }

        await SubscribeToChannelPointsRedemption(sessionId: sessionId, cancellationToken);
    }
}
