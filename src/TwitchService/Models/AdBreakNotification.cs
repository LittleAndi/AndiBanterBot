namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.ad_break.begin EventSub notification.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record AdBreakNotification
{
    [JsonPropertyName("payload")]
    public AdBreakNotificationPayload Payload { get; init; } = null!;
}

public record AdBreakNotificationPayload
{
    [JsonPropertyName("event")]
    public AdBreakEvent Event { get; init; } = null!;
}

public record AdBreakEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("requester_user_id")]
    public string RequesterUserId { get; init; } = null!;

    [JsonPropertyName("requester_user_login")]
    public string RequesterUserLogin { get; init; } = null!;

    [JsonPropertyName("requester_user_name")]
    public string RequesterUserName { get; init; } = null!;

    [JsonPropertyName("duration_seconds")]
    public int DurationSeconds { get; init; }

    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("is_automatic")]
    public bool IsAutomatic { get; init; }
}

/// <summary>
/// Current ad break state as known to the service. Twitch only sends a begin notification -
/// there is no channel.ad_break.end - so "active" is derived from StartedAt + DurationSeconds
/// against the current time rather than cleared by a matching end event, same idea as
/// RefreshStreamStatusAsync seeding state Twitch doesn't push a transition for.
/// </summary>
public record AdBreakStatusSnapshot(DateTimeOffset StartedAt, int DurationSeconds, bool IsAutomatic, string RequesterUserName);
