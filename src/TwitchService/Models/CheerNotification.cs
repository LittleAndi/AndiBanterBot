namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.cheer EventSub notification.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record CheerNotification
{
    [JsonPropertyName("payload")]
    public CheerNotificationPayload Payload { get; init; } = null!;
}

public record CheerNotificationPayload
{
    [JsonPropertyName("event")]
    public CheerEvent Event { get; init; } = null!;
}

public record CheerEvent
{
    [JsonPropertyName("is_anonymous")]
    public bool IsAnonymous { get; init; }

    // Null when is_anonymous is true.
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("bits")]
    public int Bits { get; init; }
}
