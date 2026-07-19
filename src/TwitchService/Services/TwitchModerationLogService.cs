namespace Application.Features.Twitch;

public interface ITwitchModerationLogService
{
    IReadOnlyList<ModerationLogEvent> GetRecent();
}

/// <summary>
/// Buffers the ban/unban/moderate EventSub notifications in memory so the Web dashboard's
/// moderation log panel has something to poll - nothing else durably tracks these events.
/// The buffer is process-lifetime only: a TwitchService restart clears history, same as
/// TwitchActivityFeedService.
/// </summary>
public class TwitchModerationLogService(
    ITwitchWebSocketService twitchWebSocketService) : ITwitchModerationLogService, IHostedService
{
    private const int MaxEntries = 50;
    private readonly Lock gate = new();
    private readonly LinkedList<ModerationLogEvent> entries = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        twitchWebSocketService.BanReceived += OnBanReceived;
        twitchWebSocketService.UnbanReceived += OnUnbanReceived;
        twitchWebSocketService.ModerateReceived += OnModerateReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        twitchWebSocketService.BanReceived -= OnBanReceived;
        twitchWebSocketService.UnbanReceived -= OnUnbanReceived;
        twitchWebSocketService.ModerateReceived -= OnModerateReceived;
        return Task.CompletedTask;
    }

    public IReadOnlyList<ModerationLogEvent> GetRecent()
    {
        lock (gate)
        {
            return [.. entries];
        }
    }

    private void OnBanReceived(object? sender, BanEvent e) =>
        Add(ModerationEventKind.Ban, e.ModeratorUserName, e.UserName,
            string.IsNullOrWhiteSpace(e.Reason) ? "banned" : $"banned: \"{e.Reason}\"",
            e.BannedAt);

    private void OnUnbanReceived(object? sender, UnbanEvent e) =>
        Add(ModerationEventKind.Unban, e.ModeratorUserName, e.UserName, "unbanned");

    // channel.moderate also reports ban/unban as actions ("ban", "unban"), duplicating
    // the dedicated channel.ban/channel.unban notifications above. Those two kinds are
    // skipped here so a single ban doesn't produce two log entries; every other action
    // (timeouts especially - there is no dedicated channel.timeout subscription type)
    // only ever arrives through channel.moderate.
    private void OnModerateReceived(object? sender, ModerateEvent e)
    {
        switch (e.Action)
        {
            case "ban":
            case "unban":
                return;
            case "timeout" when e.Timeout is { } timeout:
                Add(ModerationEventKind.Timeout, e.ModeratorUserName, timeout.UserName,
                    string.IsNullOrWhiteSpace(timeout.Reason)
                        ? $"timed out until {timeout.ExpiresAt.ToLocalTime():t}"
                        : $"timed out until {timeout.ExpiresAt.ToLocalTime():t}: \"{timeout.Reason}\"");
                return;
            case "untimeout" when e.Untimeout is { } untimeout:
                Add(ModerationEventKind.Untimeout, e.ModeratorUserName, untimeout.UserName, "timeout removed");
                return;
            case "delete" when e.Delete is { } delete:
                Add(ModerationEventKind.Delete, e.ModeratorUserName, delete.UserName, $"message deleted: \"{delete.MessageBody}\"");
                return;
            case "warn" when e.Warn is { } warn:
                Add(ModerationEventKind.Warn, e.ModeratorUserName, warn.UserName,
                    string.IsNullOrWhiteSpace(warn.Reason) ? "warned" : $"warned: \"{warn.Reason}\"");
                return;
            default:
                Add(ModerationEventKind.Other, e.ModeratorUserName, string.Empty, e.Action);
                return;
        }
    }

    private void Add(ModerationEventKind kind, string moderatorName, string targetName, string summary, DateTimeOffset? occurredAt = null)
    {
        lock (gate)
        {
            entries.AddFirst(new ModerationLogEvent(kind, occurredAt ?? DateTimeOffset.UtcNow, moderatorName, targetName, summary));
            while (entries.Count > MaxEntries)
            {
                entries.RemoveLast();
            }
        }
    }
}
