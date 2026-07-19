namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.prediction.begin or channel.prediction.progress EventSub
/// notification. Both subscription types share the same event payload shape.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record PredictionNotification
{
    [JsonPropertyName("payload")]
    public PredictionNotificationPayload Payload { get; init; } = null!;
}

public record PredictionNotificationPayload
{
    [JsonPropertyName("event")]
    public PredictionEvent Event { get; init; } = null!;
}

public record PredictionEvent
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("outcomes")]
    public List<PredictionOutcome> Outcomes { get; init; } = [];

    [JsonPropertyName("prediction_window")]
    public int PredictionWindow { get; init; }

    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; init; }
}

public record PredictionOutcome
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; init; } = string.Empty;

    [JsonPropertyName("users")]
    public int Users { get; init; }

    [JsonPropertyName("channel_points")]
    public int ChannelPoints { get; init; }

    [JsonPropertyName("top_predictors")]
    public List<PredictionTopPredictor> TopPredictors { get; init; } = [];
}

public record PredictionTopPredictor
{
    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("user_login")]
    public string UserLogin { get; init; } = string.Empty;

    [JsonPropertyName("user_name")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("channel_points_won")]
    public int? ChannelPointsWon { get; init; }

    [JsonPropertyName("channel_points_used")]
    public int ChannelPointsUsed { get; init; }
}

/// <summary>
/// Envelope for a channel.prediction.lock EventSub notification. Swaps started_at for
/// locked_at; kept as a separate type rather than optional fields on
/// <see cref="PredictionEvent"/> to match how channel.poll.end is modeled.
/// </summary>
public record PredictionLockNotification
{
    [JsonPropertyName("payload")]
    public PredictionLockNotificationPayload Payload { get; init; } = null!;
}

public record PredictionLockNotificationPayload
{
    [JsonPropertyName("event")]
    public PredictionLockEvent Event { get; init; } = null!;
}

public record PredictionLockEvent
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("outcomes")]
    public List<PredictionOutcome> Outcomes { get; init; } = [];

    [JsonPropertyName("prediction_window")]
    public int PredictionWindow { get; init; }

    [JsonPropertyName("locked_at")]
    public DateTimeOffset LockedAt { get; init; }
}

/// <summary>
/// Envelope for a channel.prediction.end EventSub notification. Adds winning_outcome_id,
/// status and ended_at on top of the begin/progress fields.
/// </summary>
public record PredictionEndNotification
{
    [JsonPropertyName("payload")]
    public PredictionEndNotificationPayload Payload { get; init; } = null!;
}

public record PredictionEndNotificationPayload
{
    [JsonPropertyName("event")]
    public PredictionEndEvent Event { get; init; } = null!;
}

public record PredictionEndEvent
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("winning_outcome_id")]
    public string? WinningOutcomeId { get; init; }

    [JsonPropertyName("outcomes")]
    public List<PredictionOutcome> Outcomes { get; init; } = [];

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("prediction_window")]
    public int PredictionWindow { get; init; }

    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset EndedAt { get; init; }
}

/// <summary>
/// Current prediction state as known to the service, or null when no prediction is active.
/// Set from begin/progress notifications, updated (Locked: true) on lock, and cleared on end.
/// </summary>
public record PredictionStatusSnapshot(string Title, bool Locked, IReadOnlyList<PredictionOutcomeStatus> Outcomes);

public record PredictionOutcomeStatus(string Title, string Color, int Users, int ChannelPoints);
