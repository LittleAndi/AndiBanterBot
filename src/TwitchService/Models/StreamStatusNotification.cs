namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a stream.online EventSub notification.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record StreamOnlineNotification
{
    [JsonPropertyName("payload")]
    public StreamOnlineNotificationPayload Payload { get; init; } = null!;
}

public record StreamOnlineNotificationPayload
{
    [JsonPropertyName("event")]
    public StreamOnlineEvent Event { get; init; } = null!;
}

public record StreamOnlineEvent
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; init; } = null!;

    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; init; }
}

/// <summary>
/// Envelope for a stream.offline EventSub notification.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record StreamOfflineNotification
{
    [JsonPropertyName("payload")]
    public StreamOfflineNotificationPayload Payload { get; init; } = null!;
}

public record StreamOfflineNotificationPayload
{
    [JsonPropertyName("event")]
    public StreamOfflineEvent Event { get; init; } = null!;
}

public record StreamOfflineEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;
}

/// <summary>
/// Unified view of a stream.online/stream.offline notification, raised regardless of which
/// subscription type fired so consumers (e.g. the Web dashboard) only need one event to watch.
/// </summary>
public record StreamStatusChangedEvent(
    bool IsLive,
    string BroadcasterUserId,
    string BroadcasterUserLogin,
    string BroadcasterUserName,
    DateTimeOffset? StartedAt);
