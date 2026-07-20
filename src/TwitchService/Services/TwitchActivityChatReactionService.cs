namespace Application.Features.Twitch;

/// <summary>
/// Reacts to raid/follow/sub/cheer EventSub notifications with a templated chat
/// message via ITwitchChatService, in the bot's own playful voice. A direct,
/// same-process hookup rather than routing through the (currently dormant)
/// Application project's AI chat pipeline - see issue #60 for why. Raids also get
/// an official Twitch shoutout via ITwitchShoutoutService alongside the templated
/// message, so incoming raiders show up on the raider's own channel page too.
/// </summary>
public class TwitchActivityChatReactionService(
    ITwitchWebSocketService twitchWebSocketService,
    ITwitchChatService twitchChatService,
    ITwitchShoutoutService twitchShoutoutService,
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

    private void OnRaidReceived(object? sender, RaidEvent e)
    {
        Send(BuildRaidMessage(e));
        Shoutout(e);
    }

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

    private void Shoutout(RaidEvent e)
    {
        _ = ShoutoutAsync(e);
    }

    private async Task ShoutoutAsync(RaidEvent e)
    {
        try
        {
            var result = await twitchShoutoutService.SendShoutoutAsync(e.FromBroadcasterUserLogin);
            if (!result.Success)
            {
                logger.LogWarning("Raid shoutout not sent: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send raid shoutout");
        }
    }

    private static string BuildRaidMessage(RaidEvent e) =>
        $"🚨 {e.FromBroadcasterUserName} is raiding with {e.Viewers} viewer{(e.Viewers == 1 ? "" : "s")}! Welcome raiders, make yourselves at home! 🎉";

    private static string BuildFollowMessage(FollowEvent e) =>
        $"👋 {e.UserName} just followed! Welcome to the crew!";

    private static string BuildSubscribeMessage(SubscribeEvent e) =>
        $"🎊 {e.UserName} just subscribed (Tier {SubscriptionTierLabel.For(e.Tier)})! Thank you so much!";

    private static string BuildSubscriptionGiftMessage(SubscriptionGiftEvent e)
    {
        var gifter = e.IsAnonymous ? "An anonymous legend" : e.UserName;
        return $"🎁 {gifter} just gifted {e.Total} sub{(e.Total == 1 ? "" : "s")} (Tier {SubscriptionTierLabel.For(e.Tier)})! Absolute legend!";
    }

    private static string BuildSubscriptionMessage(SubscriptionMessageEvent e) =>
        $"🔁 {e.UserName} just resubscribed for {e.CumulativeMonths} months (Tier {SubscriptionTierLabel.For(e.Tier)})! Thanks for sticking around!";

    private static string BuildCheerMessage(CheerEvent e)
    {
        var cheerer = e.IsAnonymous ? "An anonymous cheerer" : e.UserName;
        return $"💎 {cheerer} just cheered {e.Bits} bit{(e.Bits == 1 ? "" : "s")}! Thanks for the support!";
    }
}
