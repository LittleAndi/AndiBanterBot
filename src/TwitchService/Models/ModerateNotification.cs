namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.moderate (v2) EventSub notification. Twitch consolidates every
/// moderator action - bans, timeouts, deletes, AutoMod term changes, VIP/mod grants, and
/// more - into this single event type, discriminated by <see cref="ModerateEvent.Action"/>.
/// Only the nested payloads the moderation log cares about (ban/timeout/unban/untimeout/
/// delete/warn) are mapped; other actions still deserialize fine, they just carry no
/// action-specific detail beyond <see cref="ModerateEvent.Action"/> itself.
/// </summary>
public record ModerateNotification
{
    [JsonPropertyName("payload")]
    public ModerateNotificationPayload Payload { get; init; } = null!;
}

public record ModerateNotificationPayload
{
    [JsonPropertyName("event")]
    public ModerateEvent Event { get; init; } = null!;
}

public record ModerateEvent
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

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("ban")]
    public ModerateTargetWithReason? Ban { get; init; }

    [JsonPropertyName("timeout")]
    public ModerateTimeoutTarget? Timeout { get; init; }

    [JsonPropertyName("unban")]
    public ModerateTarget? Unban { get; init; }

    [JsonPropertyName("untimeout")]
    public ModerateTarget? Untimeout { get; init; }

    [JsonPropertyName("delete")]
    public ModerateDeleteTarget? Delete { get; init; }

    [JsonPropertyName("warn")]
    public ModerateTargetWithReason? Warn { get; init; }
}

public record ModerateTarget
{
    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = null!;

    [JsonPropertyName("user_login")]
    public string UserLogin { get; init; } = null!;

    [JsonPropertyName("user_name")]
    public string UserName { get; init; } = null!;
}

public record ModerateTargetWithReason : ModerateTarget
{
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}

public record ModerateTimeoutTarget : ModerateTargetWithReason
{
    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; init; }
}

public record ModerateDeleteTarget : ModerateTarget
{
    [JsonPropertyName("message_body")]
    public string MessageBody { get; init; } = string.Empty;
}
