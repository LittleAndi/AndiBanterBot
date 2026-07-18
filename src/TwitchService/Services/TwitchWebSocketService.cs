namespace Application.Features.Twitch;

public interface ITwitchWebSocketService
{
    Task Start(CancellationToken cancellationToken = default);
    Task SubscribeToBroadcasterSubscriptions(CancellationToken cancellationToken = default);
}

public class TwitchWebSocketService(
    IWebSocketClient webSocketClient,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ITwitchTokenStore tokenStore,
    // IHostApplicationLifetime hostApplicationLifetime,
    ILogger<TwitchWebSocketService> logger) : ITwitchWebSocketService
{
    private readonly HttpClient twitchHttpClientAppAccess = httpClientFactory.CreateClient("TwitchClientAppAccess");
    private readonly HttpClient twitchHttpClientUserAccess = httpClientFactory.CreateClient("TwitchClientUserAccess");
    private readonly string broadcasterUsername = configuration["Twitch:BroadcasterUsername"] ?? throw new InvalidOperationException("BroadcasterUsername not configured");
    private readonly string monitoredUsername = configuration["Twitch:MonitoredUsername"] ?? throw new InvalidOperationException("MonitoredUsername not configured");
    private string? broadcasterId;
    private string? userId;
    private bool connectingOrConnected = false;
    private string sessionId = string.Empty;
    public async Task Start(CancellationToken cancellationToken = default)
    {
        // Just block this if we think we're already connected
        if (connectingOrConnected) return;

        try
        {
            connectingOrConnected = true;

            // Get user IDs before starting
            await InitializeUserIds(cancellationToken);

            // Add event handlers
            webSocketClient.OnWelcomeReceived += async (sender, e) => await HandleWelcomeMessage(sender, e, cancellationToken).ConfigureAwait(false);
            webSocketClient.OnMessageReceived += async (sender, e) => await HandleMessageReceived(sender, e).ConfigureAwait(false);
            webSocketClient.OnCloseReceived += HandleCloseMessage;

            await webSocketClient.ConnectAsync("wss://eventsub.wss.twitch.tv/ws?keepalive_timeout_seconds=60", cancellationToken);

            // Keep the service running
            await webSocketClient.ReceiveMessagesAsync(cancellationToken);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    logger.LogInformation("WebSocket client is still connected");
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Service is shutting down");
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Service is shutting down");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in TwitchWebSocketService");
        }
    }

    private void HandleCloseMessage(object? sender, EventArgs e)
    {
        logger.LogWarning("WebSocket connection closed. Initiating graceful shutdown.");
        // hostApplicationLifetime.StopApplication();
    }

    private async Task InitializeUserIds(CancellationToken cancellationToken)
    {
        broadcasterId = await GetUserIdFromUsername(broadcasterUsername, cancellationToken);
        userId = await GetUserIdFromUsername(monitoredUsername, cancellationToken);

        if (string.IsNullOrEmpty(broadcasterId) || string.IsNullOrEmpty(userId))
        {
            throw new InvalidOperationException("Failed to get user IDs from usernames");
        }
    }

    private async Task<string?> GetUserIdFromUsername(string username, CancellationToken cancellationToken)
    {
        try
        {
            var response = await twitchHttpClientAppAccess.GetAsync($"helix/users?login={username}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to get user ID for {Username}. Status: {StatusCode}",
                    username, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            var users = doc.RootElement.GetProperty("data");

            if (users.GetArrayLength() == 0)
            {
                logger.LogError("User {Username} not found", username);
                return null;
            }

            var userId = users[0].GetProperty("id").GetString();
            logger.LogInformation("Retrieved user ID {UserId} for username {Username}", userId, username);
            return userId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user ID for username {Username}", username);
            return null;
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
        logger.LogInformation("Message received: {TwitchMessageEventArgs}", e.RawMessage);

        if (e.Response?.Metadata.MessageType == "channel.chat.message")
        {
            logger.LogInformation("Chat message received: {Message}", e.RawMessage);
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
