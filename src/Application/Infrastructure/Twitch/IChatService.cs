namespace Application.Infrastructure.Twitch;

/// <summary>
/// Chat abstraction for the feature layer. The Helix-based implementation lives
/// in the TwitchService project (EventSub channel.chat.message for receiving,
/// POST /helix/chat/messages for sending); wiring the feature layer to it is a
/// follow-up to the TwitchLib removal.
/// </summary>
public interface IChatService
{
    event Func<ChatMessage, string, Task>? MessageReceived;
    event Func<WhisperMessage, Task>? WhisperReceived;
    Task StartAsync(string accessToken, CancellationToken cancellationToken);
    Task SendMessage(string channel, string message, CancellationToken cancellationToken = default);
    Task SendReply(string channel, string replyToMessageId, string reply, CancellationToken cancellationToken = default);
    Task JoinChannel(string channel, CancellationToken cancellationToken = default);
    IReadOnlyList<string> JoinedChannels { get; }
}
