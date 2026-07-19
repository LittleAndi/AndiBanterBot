namespace Application.Features.Twitch;

public enum ModerationEventKind
{
    Ban,
    Unban,
    Timeout,
    Untimeout,
    Delete,
    Warn,
    Other,
}

/// <summary>
/// Unified shape for the ban/unban/moderate EventSub notifications, so a single log can
/// display them without callers needing to know each event's own type.
/// </summary>
public record ModerationLogEvent(
    ModerationEventKind Kind,
    DateTimeOffset OccurredAt,
    string ModeratorName,
    string TargetName,
    string Summary);
