namespace Application.Infrastructure.Twitch;

/// <summary>
/// Twitch chat DTOs owned by this project, mapped from EventSub
/// channel.chat.message notifications (previously TwitchLib.Client models).
/// </summary>
public record ChatMessage(string Id, string Channel, string Username, string Message);

public record WhisperMessage(string Username, string Message);

public record RedemptionReward(string Id, string Title, int Cost);
