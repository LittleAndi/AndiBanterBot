namespace Application.Features.Twitch;

/// <summary>
/// Auto-captures a clip on notable moments - a hype train finishing, or a cheer at or above a
/// configurable bit threshold (Twitch:ClipBitThreshold, default 500) - so highlights get saved
/// without the streamer having to react in the moment. Same direct, same-process hookup as
/// TwitchActivityChatReactionService rather than routing through the dormant Application AI
/// pipeline; also reuses ITwitchChatService to announce the clip, matching how that service
/// reacts to other big moments in chat.
/// </summary>
public class TwitchClipTriggerService(
    ITwitchWebSocketService twitchWebSocketService,
    ITwitchClipService twitchClipService,
    ITwitchChatService twitchChatService,
    ITwitchAutoClipFeedService autoClipFeedService,
    IConfiguration configuration,
    ILogger<TwitchClipTriggerService> logger) : IHostedService
{
    private readonly int bitThreshold = configuration.GetValue("Twitch:ClipBitThreshold", 500);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        twitchWebSocketService.HypeTrainEndReceived += OnHypeTrainEndReceived;
        twitchWebSocketService.CheerReceived += OnCheerReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        twitchWebSocketService.HypeTrainEndReceived -= OnHypeTrainEndReceived;
        twitchWebSocketService.CheerReceived -= OnCheerReceived;
        return Task.CompletedTask;
    }

    private void OnHypeTrainEndReceived(object? sender, HypeTrainEndEvent e) =>
        _ = TriggerClipAsync($"🚂 that hype train finish (Level {e.Level})!");

    private void OnCheerReceived(object? sender, CheerEvent e)
    {
        if (e.Bits < bitThreshold) return;
        _ = TriggerClipAsync($"💎 that {e.Bits}-bit cheer!");
    }

    private async Task TriggerClipAsync(string highlightDescription)
    {
        try
        {
            var result = await twitchClipService.CreateClipAsync();
            if (!result.Success)
            {
                logger.LogWarning("Auto-clip not created: {Error}", result.Error);
                return;
            }

            logger.LogInformation("Auto-created clip {ClipId} for {Highlight}", result.ClipId, highlightDescription);
            autoClipFeedService.Add(result.ClipId!, highlightDescription);
            await AnnounceAsync(highlightDescription);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-create clip for {Highlight}", highlightDescription);
        }
    }

    private async Task AnnounceAsync(string highlightDescription)
    {
        try
        {
            var result = await twitchChatService.SendMessageAsync($"🎬 Clipped {highlightDescription}");
            if (!result.Sent)
            {
                logger.LogWarning("Auto-clip announcement not sent: {DropReason}", result.DropReason);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send auto-clip announcement");
        }
    }
}
