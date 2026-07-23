namespace Application.Features.Twitch;

/// <summary>
/// Buffered shape of a recognized chat command (see ChatCommandParser), captured from the
/// same channel.chat.message notifications TwitchChatFeedService buffers.
/// </summary>
public record ChatCommandFeedEntry(
    string MessageId,
    DateTimeOffset OccurredAt,
    string ChatterUserId,
    string ChatterUserLogin,
    string ChatterUserName,
    string Name,
    IReadOnlyList<string> Args,
    string RawText);
