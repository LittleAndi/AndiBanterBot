namespace Application.Features.Twitch;

public interface ITwitchActivityFeedService
{
    IReadOnlyList<ActivityFeedEvent> GetRecent();
}

/// <summary>
/// Buffers the six raid/follow/sub/gift/resub/cheer EventSub notifications in memory so the
/// Web dashboard's activity feed panel has something to poll - nothing else durably tracks
/// these events (see TwitchActivityChatReactionService, which only reacts to them in chat).
/// The buffer is process-lifetime only: a TwitchService restart clears history, same as the
/// dashboard's other status tiles which reflect live state rather than a persisted log.
/// </summary>
public class TwitchActivityFeedService(
    ITwitchWebSocketService twitchWebSocketService) : ITwitchActivityFeedService, IHostedService
{
    private const int MaxEntries = 50;
    private readonly Lock gate = new();
    private readonly LinkedList<ActivityFeedEvent> entries = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        twitchWebSocketService.RaidReceived += OnRaidReceived;
        twitchWebSocketService.FollowReceived += OnFollowReceived;
        twitchWebSocketService.SubscribeReceived += OnSubscribeReceived;
        twitchWebSocketService.SubscriptionGiftReceived += OnSubscriptionGiftReceived;
        twitchWebSocketService.SubscriptionMessageReceived += OnSubscriptionMessageReceived;
        twitchWebSocketService.CheerReceived += OnCheerReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        twitchWebSocketService.RaidReceived -= OnRaidReceived;
        twitchWebSocketService.FollowReceived -= OnFollowReceived;
        twitchWebSocketService.SubscribeReceived -= OnSubscribeReceived;
        twitchWebSocketService.SubscriptionGiftReceived -= OnSubscriptionGiftReceived;
        twitchWebSocketService.SubscriptionMessageReceived -= OnSubscriptionMessageReceived;
        twitchWebSocketService.CheerReceived -= OnCheerReceived;
        return Task.CompletedTask;
    }

    public IReadOnlyList<ActivityFeedEvent> GetRecent()
    {
        lock (gate)
        {
            return [.. entries];
        }
    }

    private void OnRaidReceived(object? sender, RaidEvent e) =>
        Add(ActivityEventKind.Raid, e.FromBroadcasterUserName, $"raided with {e.Viewers} viewer{(e.Viewers == 1 ? "" : "s")}");

    private void OnFollowReceived(object? sender, FollowEvent e) =>
        Add(ActivityEventKind.Follow, e.UserName, "followed", e.FollowedAt);

    // Twitch also raises channel.subscribe for each recipient of a gift-sub batch
    // (is_gift: true), on top of the single channel.subscription.gift event for the
    // batch itself. Skipped here so gifted subs don't show up twice in the feed -
    // same reasoning as TwitchActivityChatReactionService.
    private void OnSubscribeReceived(object? sender, SubscribeEvent e)
    {
        if (e.IsGift) return;
        Add(ActivityEventKind.Subscribe, e.UserName, $"subscribed (Tier {SubscriptionTierLabel.For(e.Tier)})");
    }

    private void OnSubscriptionGiftReceived(object? sender, SubscriptionGiftEvent e)
    {
        var gifter = e.IsAnonymous ? "An anonymous legend" : e.UserName ?? "Someone";
        Add(ActivityEventKind.SubscriptionGift, gifter, $"gifted {e.Total} sub{(e.Total == 1 ? "" : "s")} (Tier {SubscriptionTierLabel.For(e.Tier)})");
    }

    private void OnSubscriptionMessageReceived(object? sender, SubscriptionMessageEvent e) =>
        Add(ActivityEventKind.Resubscribe, e.UserName, $"resubscribed for {e.CumulativeMonths} months (Tier {SubscriptionTierLabel.For(e.Tier)})");

    private void OnCheerReceived(object? sender, CheerEvent e)
    {
        var cheerer = e.IsAnonymous ? "An anonymous cheerer" : e.UserName ?? "Someone";
        Add(ActivityEventKind.Cheer, cheerer, $"cheered {e.Bits} bit{(e.Bits == 1 ? "" : "s")}");
    }

    private void Add(ActivityEventKind kind, string displayName, string summary, DateTimeOffset? occurredAt = null)
    {
        lock (gate)
        {
            entries.AddFirst(new ActivityFeedEvent(kind, occurredAt ?? DateTimeOffset.UtcNow, displayName, summary));
            while (entries.Count > MaxEntries)
            {
                entries.RemoveLast();
            }
        }
    }
}
