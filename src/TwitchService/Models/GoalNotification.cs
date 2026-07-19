namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.goal.begin or channel.goal.progress EventSub notification.
/// Both subscription types share the same event payload shape.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record GoalNotification
{
    [JsonPropertyName("payload")]
    public GoalNotificationPayload Payload { get; init; } = null!;
}

public record GoalNotificationPayload
{
    [JsonPropertyName("event")]
    public GoalEvent Event { get; init; } = null!;
}

public record GoalEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("current_amount")]
    public int CurrentAmount { get; init; }

    [JsonPropertyName("target_amount")]
    public int TargetAmount { get; init; }
}

/// <summary>
/// Envelope for a channel.goal.end EventSub notification. Adds is_achieved/ended_at on
/// top of the begin/progress fields; kept as a separate type rather than optional fields
/// on <see cref="GoalEvent"/> to match how channel.hype_train.end is modeled.
/// </summary>
public record GoalEndNotification
{
    [JsonPropertyName("payload")]
    public GoalEndNotificationPayload Payload { get; init; } = null!;
}

public record GoalEndNotificationPayload
{
    [JsonPropertyName("event")]
    public GoalEndEvent Event { get; init; } = null!;
}

public record GoalEndEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("current_amount")]
    public int CurrentAmount { get; init; }

    [JsonPropertyName("target_amount")]
    public int TargetAmount { get; init; }

    [JsonPropertyName("is_achieved")]
    public bool IsAchieved { get; init; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset EndedAt { get; init; }
}
