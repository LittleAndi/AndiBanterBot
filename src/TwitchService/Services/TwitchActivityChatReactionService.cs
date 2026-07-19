namespace Application.Features.Twitch;

/// <summary>
/// Reacts to raid/follow/sub/cheer EventSub notifications with a templated chat
/// shoutout via ITwitchChatService, in the bot's own playful voice. A direct,
/// same-process hookup rather than routing through the (currently dormant)
/// Application project's AI chat pipeline - see issue #60 for why.
/// </summary>
public class TwitchActivityChatReactionService(
    ITwitchWebSocketService twitchWebSocketService,
    ITwitchChatService twitchChatService,
    ILogger<TwitchActivityChatReactionService> logger) : IHostedService
{
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

    private void OnRaidReceived(object? sender, RaidEvent e) =>
        Send(BuildRaidMessage(e));

    private void OnFollowReceived(object? sender, FollowEvent e) =>
        Send(BuildFollowMessage(e));

    // Twitch also raises channel.subscribe for each recipient of a gift-sub batch
    // (is_gift: true), on top of the single channel.subscription.gift event for the
    // batch itself. Reacting to both would spam chat with one message per recipient,
    // so gifted subscribes are skipped here - the gift event covers the shoutout.
    private void OnSubscribeReceived(object? sender, SubscribeEvent e)
    {
        if (e.IsGift) return;
        Send(BuildSubscribeMessage(e));
    }

    private void OnSubscriptionGiftReceived(object? sender, SubscriptionGiftEvent e) =>
        Send(BuildSubscriptionGiftMessage(e));

    private void OnSubscriptionMessageReceived(object? sender, SubscriptionMessageEvent e) =>
        Send(BuildSubscriptionMessage(e));

    private void OnCheerReceived(object? sender, CheerEvent e) =>
        Send(BuildCheerMessage(e));

    private void Send(string message)
    {
        _ = SendAsync(message);
    }

    private async Task SendAsync(string message)
    {
        try
        {
            var result = await twitchChatService.SendMessageAsync(message);
            if (!result.Sent)
            {
                logger.LogWarning("Activity chat reaction not sent: {DropReason}", result.DropReason);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send activity chat reaction");
        }
    }

    private static string BuildRaidMessage(RaidEvent e) =>
        $"🚨 {e.FromBroadcasterUserName} is raiding with {e.Viewers} viewer{(e.Viewers == 1 ? "" : "s")}! Welcome raiders, make yourselves at home! 🎉";

    private static string BuildFollowMessage(FollowEvent e) =>
        $"👋 {e.UserName} just followed! Welcome to the crew!";

    private static string BuildSubscribeMessage(SubscribeEvent e) =>
        $"🎊 {e.UserName} just subscribed (Tier {TierLabel(e.Tier)})! Thank you so much!";

    private static string BuildSubscriptionGiftMessage(SubscriptionGiftEvent e)
    {
        var gifter = e.IsAnonymous ? "An anonymous legend" : e.UserName;
        return $"🎁 {gifter} just gifted {e.Total} sub{(e.Total == 1 ? "" : "s")} (Tier {TierLabel(e.Tier)})! Absolute legend!";
    }

    private static string BuildSubscriptionMessage(SubscriptionMessageEvent e) =>
        $"🔁 {e.UserName} just resubscribed for {e.CumulativeMonths} months (Tier {TierLabel(e.Tier)})! Thanks for sticking around!";

    private static string BuildCheerMessage(CheerEvent e)
    {
        var cheerer = e.IsAnonymous ? "An anonymous cheerer" : e.UserName;
        return $"💎 {cheerer} just cheered {e.Bits} bit{(e.Bits == 1 ? "" : "s")}! Thanks for the support!";
    }

    private static string TierLabel(string tier) => tier switch
    {
        "1000" => "1",
        "2000" => "2",
        "3000" => "3",
        _ => tier
    };
}
