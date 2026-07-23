namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.chat.message_delete EventSub notification, raised when a
/// moderator removes a single message from chat (not a full user purge/ban).
/// </summary>
public record ChatMessageDeleteNotification
{
    [JsonPropertyName("payload")]
    public ChatMessageDeleteNotificationPayload Payload { get; init; } = null!;
}

public record ChatMessageDeleteNotificationPayload
{
    [JsonPropertyName("event")]
    public ChatMessageDeleteEvent Event { get; init; } = null!;
}

public record ChatMessageDeleteEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("target_user_id")]
    public string TargetUserId { get; init; } = null!;

    [JsonPropertyName("target_user_login")]
    public string TargetUserLogin { get; init; } = null!;

    [JsonPropertyName("target_user_name")]
    public string TargetUserName { get; init; } = null!;

    [JsonPropertyName("message_id")]
    public string MessageId { get; init; } = null!;
}
