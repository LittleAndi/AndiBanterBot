namespace Application.Features.Twitch;

public enum ActivityEventKind
{
    Raid,
    Follow,
    Subscribe,
    SubscriptionGift,
    Resubscribe,
    Cheer,
}

/// <summary>
/// Unified shape for the six raid/follow/sub/gift/resub/cheer EventSub notifications,
/// so a single feed can display them without callers needing to know each event's own type.
/// </summary>
public record ActivityFeedEvent(
    ActivityEventKind Kind,
    DateTimeOffset OccurredAt,
    string DisplayName,
    string Summary);
