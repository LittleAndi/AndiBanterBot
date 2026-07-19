namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.follow EventSub notification.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record FollowNotification
{
    [JsonPropertyName("payload")]
    public FollowNotificationPayload Payload { get; init; } = null!;
}

public record FollowNotificationPayload
{
    [JsonPropertyName("event")]
    public FollowEvent Event { get; init; } = null!;
}

public record FollowEvent
{
    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = null!;

    [JsonPropertyName("user_login")]
    public string UserLogin { get; init; } = null!;

    [JsonPropertyName("user_name")]
    public string UserName { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("followed_at")]
    public DateTimeOffset FollowedAt { get; init; }
}
