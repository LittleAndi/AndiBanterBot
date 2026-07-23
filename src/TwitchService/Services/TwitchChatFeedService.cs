namespace Application.Features.Twitch;

public interface ITwitchChatFeedService
{
    IReadOnlyList<ChatFeedMessage> GetRecent();
}

/// <summary>
/// Buffers channel.chat.message (and channel.chat.message_delete) EventSub notifications
/// in memory so overlay consumers have something to poll for chat rendering - same shape
/// and process-lifetime-only caveat as TwitchActivityFeedService.
/// </summary>
public class TwitchChatFeedService(
    ITwitchWebSocketService twitchWebSocketService) : ITwitchChatFeedService, IHostedService
{
    private const int MaxEntries = 100;
    private readonly Lock gate = new();
    private readonly LinkedList<ChatFeedMessage> entries = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        twitchWebSocketService.ChatMessageReceived += OnChatMessageReceived;
        twitchWebSocketService.ChatMessageDeleted += OnChatMessageDeleted;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        twitchWebSocketService.ChatMessageReceived -= OnChatMessageReceived;
        twitchWebSocketService.ChatMessageDeleted -= OnChatMessageDeleted;
        return Task.CompletedTask;
    }

    public IReadOnlyList<ChatFeedMessage> GetRecent()
    {
        lock (gate)
        {
            return [.. entries];
        }
    }

    private void OnChatMessageReceived(object? sender, ChatMessageEvent e)
    {
        var message = new ChatFeedMessage(
            e.MessageId,
            DateTimeOffset.UtcNow,
            e.ChatterUserId,
            e.ChatterUserLogin,
            e.ChatterUserName,
            e.Message.Text,
            [.. e.Message.Fragments.Select(ToFragment)],
            e.Color,
            [.. e.Badges.Select(ToBadge)],
            e.Reply?.ParentMessageId);

        lock (gate)
        {
            entries.AddFirst(message);
            while (entries.Count > MaxEntries)
            {
                entries.RemoveLast();
            }
        }
    }

    private void OnChatMessageDeleted(object? sender, ChatMessageDeleteEvent e)
    {
        lock (gate)
        {
            var match = entries.FirstOrDefault(m => m.MessageId == e.MessageId);
            if (match is not null)
            {
                match.IsDeleted = true;
            }
        }
    }

    private static ChatFeedFragment ToFragment(ChatMessageFragment fragment) =>
        new(fragment.Type, fragment.Text, fragment.Emote is null
            ? null
            : new ChatFeedEmote(fragment.Emote.Id, fragment.Emote.EmoteSetId, fragment.Emote.OwnerId, fragment.Emote.Format));

    private static ChatFeedBadge ToBadge(ChatMessageBadge badge) =>
        new(badge.SetId, badge.Id, badge.Info);
}
