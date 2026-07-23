namespace Application.Features.Twitch;

public interface ITwitchChatCommandFeedService
{
    IReadOnlyList<ChatCommandFeedEntry> GetRecent();
}

/// <summary>
/// Buffers recognized chat commands (ChatCommandParser) the same ring-buffer way
/// TwitchChatFeedService buffers raw messages, so a future dispatch mechanism - chat games,
/// suggestions, built later in OverlayService by polling this endpoint - has something to
/// consume. Recognition and exposure only; no handling/dispatch happens here.
/// </summary>
public class TwitchChatCommandFeedService(
    ITwitchWebSocketService twitchWebSocketService) : ITwitchChatCommandFeedService, IHostedService
{
    private const int MaxEntries = 100;
    private readonly Lock gate = new();
    private readonly LinkedList<ChatCommandFeedEntry> entries = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        twitchWebSocketService.ChatMessageReceived += OnChatMessageReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        twitchWebSocketService.ChatMessageReceived -= OnChatMessageReceived;
        return Task.CompletedTask;
    }

    public IReadOnlyList<ChatCommandFeedEntry> GetRecent()
    {
        lock (gate)
        {
            return [.. entries];
        }
    }

    private void OnChatMessageReceived(object? sender, ChatMessageEvent e)
    {
        var command = ChatCommandParser.TryParse(e.Message.Text);
        if (command is null) return;

        var entry = new ChatCommandFeedEntry(
            e.MessageId,
            DateTimeOffset.UtcNow,
            e.ChatterUserId,
            e.ChatterUserLogin,
            e.ChatterUserName,
            command.Name,
            command.Args,
            e.Message.Text);

        lock (gate)
        {
            entries.AddFirst(entry);
            while (entries.Count > MaxEntries)
            {
                entries.RemoveLast();
            }
        }
    }
}
