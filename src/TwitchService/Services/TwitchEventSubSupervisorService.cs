namespace Application.Features.Twitch;

/// <summary>
/// Keeps the EventSub websocket alive: starts it when user tokens are available
/// (at startup or after the first browser login), reconnects when the connection
/// drops or Twitch requests a session reconnect, and force-reconnects when no
/// message has arrived within the keepalive window.
/// </summary>
public class TwitchEventSubSupervisorService(
    ITwitchTokenStore tokenStore,
    ITwitchWebSocketService twitchWebSocketService,
    ILogger<TwitchEventSubSupervisorService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan KeepaliveGrace = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxFailureBackoff = TimeSpan.FromMinutes(2);

    private int consecutiveFailures = 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!tokenStore.HasToken(TwitchUserRole.Bot) && !tokenStore.HasToken(TwitchUserRole.Broadcaster))
        {
            logger.LogInformation("No persisted Twitch user tokens found, EventSub will start after the first browser login");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SuperviseOnce(stoppingToken);
                consecutiveFailures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                var backoff = Backoff(consecutiveFailures);
                logger.LogError(ex, "EventSub supervision failed ({ConsecutiveFailures} in a row), next attempt in {Backoff}",
                    consecutiveFailures, backoff);
                await Task.Delay(backoff, stoppingToken);
                continue;
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task SuperviseOnce(CancellationToken stoppingToken)
    {
        if (!tokenStore.HasToken(TwitchUserRole.Bot) && !tokenStore.HasToken(TwitchUserRole.Broadcaster))
        {
            return;
        }

        var status = twitchWebSocketService.GetStatus();

        if (!status.Connected)
        {
            logger.LogInformation("EventSub websocket is not connected, starting");
            await twitchWebSocketService.Start(stoppingToken);
            return;
        }

        var silence = DateTime.UtcNow - status.LastMessageAtUtc;
        if (silence > status.KeepaliveTimeout + KeepaliveGrace)
        {
            logger.LogWarning("No EventSub message for {Silence:c} (keepalive timeout {KeepaliveTimeout:c}), assuming dead connection and reconnecting",
                silence, status.KeepaliveTimeout);
            await twitchWebSocketService.CloseAsync(stoppingToken);
            // The next supervision pass reconnects once the receive loop has ended
        }
    }

    private static TimeSpan Backoff(int failures)
    {
        var seconds = Math.Min(PollInterval.TotalSeconds * Math.Pow(2, failures - 1), MaxFailureBackoff.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
