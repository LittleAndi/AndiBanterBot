namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.hype_train.begin or channel.hype_train.progress EventSub
/// notification. Both subscription types share the same event payload shape.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record HypeTrainNotification
{
    [JsonPropertyName("payload")]
    public HypeTrainNotificationPayload Payload { get; init; } = null!;
}

public record HypeTrainNotificationPayload
{
    [JsonPropertyName("event")]
    public HypeTrainEvent Event { get; init; } = null!;
}

public record HypeTrainEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("level")]
    public int Level { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("progress")]
    public int Progress { get; init; }

    [JsonPropertyName("goal")]
    public int Goal { get; init; }
}

/// <summary>
/// Envelope for a channel.hype_train.end EventSub notification. Adds ended_at on top of
/// the begin/progress fields; kept as a separate type rather than an optional field on
/// <see cref="HypeTrainEvent"/> to match how other end-of-something notifications
/// (e.g. stream.offline) are modeled in this codebase.
/// </summary>
public record HypeTrainEndNotification
{
    [JsonPropertyName("payload")]
    public HypeTrainEndNotificationPayload Payload { get; init; } = null!;
}

public record HypeTrainEndNotificationPayload
{
    [JsonPropertyName("event")]
    public HypeTrainEndEvent Event { get; init; } = null!;
}

public record HypeTrainEndEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("level")]
    public int Level { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("goal")]
    public int Goal { get; init; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset EndedAt { get; init; }
}
