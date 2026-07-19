namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.ban EventSub notification. Only fires for permanent bans -
/// timeouts arrive via channel.moderate's "timeout" action instead.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record BanNotification
{
    [JsonPropertyName("payload")]
    public BanNotificationPayload Payload { get; init; } = null!;
}

public record BanNotificationPayload
{
    [JsonPropertyName("event")]
    public BanEvent Event { get; init; } = null!;
}

public record BanEvent
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

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    [JsonPropertyName("banned_at")]
    public DateTimeOffset BannedAt { get; init; }

    [JsonPropertyName("ends_at")]
    public DateTimeOffset? EndsAt { get; init; }

    [JsonPropertyName("is_permanent")]
    public bool IsPermanent { get; init; }
}
