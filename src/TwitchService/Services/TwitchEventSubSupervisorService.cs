namespace Application.Features.Twitch;

/// <summary>
/// Keeps the EventSub websockets alive: starts each (Bot, Broadcaster) connection when
/// its user token is available (at startup or after the first browser login), reconnects
/// when it drops or Twitch requests a session reconnect, and force-reconnects when no
/// message has arrived within the keepalive window. The two connections are supervised
/// independently since they're separate Twitch sessions with independent lifecycles.
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
            var botOk = await TrySupervise(TwitchUserRole.Bot, stoppingToken);
            var broadcasterOk = await TrySupervise(TwitchUserRole.Broadcaster, stoppingToken);

            if (botOk && broadcasterOk)
            {
                consecutiveFailures = 0;
                await Task.Delay(PollInterval, stoppingToken);
            }
            else
            {
                consecutiveFailures++;
                var backoff = Backoff(consecutiveFailures);
                logger.LogError("EventSub supervision failed ({ConsecutiveFailures} in a row), next attempt in {Backoff}",
                    consecutiveFailures, backoff);
                await Task.Delay(backoff, stoppingToken);
            }
        }
    }

    private async Task<bool> TrySupervise(TwitchUserRole role, CancellationToken stoppingToken)
    {
        try
        {
            await SuperviseOnce(role, stoppingToken);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Role} EventSub supervision failed", role);
            return false;
        }
    }

    private async Task SuperviseOnce(TwitchUserRole role, CancellationToken stoppingToken)
    {
        if (!tokenStore.HasToken(role))
        {
            return;
        }

        var status = twitchWebSocketService.GetStatus(role);

        if (!status.Connected)
        {
            logger.LogInformation("{Role} EventSub websocket is not connected, starting", role);
            await twitchWebSocketService.Start(role, stoppingToken);
            return;
        }

        var silence = DateTime.UtcNow - status.LastMessageAtUtc;
        if (silence > status.KeepaliveTimeout + KeepaliveGrace)
        {
            logger.LogWarning("No {Role} EventSub message for {Silence:c} (keepalive timeout {KeepaliveTimeout:c}), assuming dead connection and reconnecting",
                role, silence, status.KeepaliveTimeout);
            await twitchWebSocketService.CloseAsync(role, stoppingToken);
            // The next supervision pass reconnects once the receive loop has ended
        }
    }

    private static TimeSpan Backoff(int failures)
    {
        var seconds = Math.Min(PollInterval.TotalSeconds * Math.Pow(2, failures - 1), MaxFailureBackoff.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
