namespace Application.Features.Twitch;

public interface ITwitchAutoClipFeedService
{
    IReadOnlyList<AutoClipEvent> GetRecent();

    void Add(string clipId, string highlightDescription);
}

/// <summary>
/// Buffers the clips TwitchClipTriggerService auto-creates so the highlights overlay has
/// something cheap to poll, instead of hitting the live Helix clips/recent endpoint (see
/// TwitchActivityFeedService for the same reasoning applied to the dashboard activity feed).
/// The buffer is process-lifetime only: a TwitchService restart clears history.
/// </summary>
public class TwitchAutoClipFeedService : ITwitchAutoClipFeedService
{
    private const int MaxEntries = 20;
    private readonly Lock gate = new();
    private readonly LinkedList<AutoClipEvent> entries = new();

    public IReadOnlyList<AutoClipEvent> GetRecent()
    {
        lock (gate)
        {
            return [.. entries];
        }
    }

    public void Add(string clipId, string highlightDescription)
    {
        lock (gate)
        {
            entries.AddFirst(new AutoClipEvent(clipId, DateTimeOffset.UtcNow, highlightDescription));
            while (entries.Count > MaxEntries)
            {
                entries.RemoveLast();
            }
        }
    }
}
