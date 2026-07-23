namespace Application.Features.Twitch;

public record ChatFeedEmote(string Id, string EmoteSetId, string OwnerId, IReadOnlyList<string> Format);

public record ChatFeedFragment(string Type, string Text, ChatFeedEmote? Emote);

public record ChatFeedBadge(string SetId, string Id, string Info);

/// <summary>
/// Buffered shape of a channel.chat.message notification. IsDeleted is set in place by a
/// matching channel.chat.message_delete notification rather than removing the entry, so
/// the chat overlay can render its own removal treatment instead of the message just
/// vanishing from the feed.
/// </summary>
public record ChatFeedMessage(
    string MessageId,
    DateTimeOffset OccurredAt,
    string ChatterUserId,
    string ChatterUserLogin,
    string ChatterUserName,
    string Text,
    IReadOnlyList<ChatFeedFragment> Fragments,
    string? Color,
    IReadOnlyList<ChatFeedBadge> Badges,
    string? ReplyParentMessageId)
{
    public bool IsDeleted { get; set; }
}
