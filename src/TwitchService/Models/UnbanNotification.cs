namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.unban EventSub notification.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record UnbanNotification
{
    [JsonPropertyName("payload")]
    public UnbanNotificationPayload Payload { get; init; } = null!;
}

public record UnbanNotificationPayload
{
    [JsonPropertyName("event")]
    public UnbanEvent Event { get; init; } = null!;
}

public record UnbanEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("moderator_user_id")]
    public string ModeratorUserId { get; init; } = null!;

    [JsonPropertyName("moderator_user_login")]
    public string ModeratorUserLogin { get; init; } = null!;

    [JsonPropertyName("moderator_user_name")]
    public string ModeratorUserName { get; init; } = null!;

    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = null!;

    [JsonPropertyName("user_login")]
    public string UserLogin { get; init; } = null!;

    [JsonPropertyName("user_name")]
    public string UserName { get; init; } = null!;
}
