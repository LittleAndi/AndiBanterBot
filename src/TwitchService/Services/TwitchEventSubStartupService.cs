namespace Application.Features.Twitch;

/// <summary>
/// Starts the EventSub websocket at application startup when persisted user tokens
/// are available, so the bot comes up without requiring a browser login.
/// </summary>
public class TwitchEventSubStartupService(
    ITwitchTokenStore tokenStore,
    ITwitchWebSocketService twitchWebSocketService,
    ILogger<TwitchEventSubStartupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hasBotToken = tokenStore.HasToken(TwitchUserRole.Bot);
        var hasBroadcasterToken = tokenStore.HasToken(TwitchUserRole.Broadcaster);

        if (!hasBotToken && !hasBroadcasterToken)
        {
            logger.LogInformation("No persisted Twitch user tokens found, EventSub will start after the first browser login");
            return;
        }

        logger.LogInformation("Persisted Twitch user tokens found (bot: {HasBotToken}, broadcaster: {HasBroadcasterToken}), starting EventSub",
            hasBotToken, hasBroadcasterToken);

        try
        {
            await twitchWebSocketService.Start(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start EventSub at startup, a browser login or restart is required");
        }
    }
}
