namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.subscription.message EventSub notification (a resub with a message).
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record SubscriptionMessageNotification
{
    [JsonPropertyName("payload")]
    public SubscriptionMessageNotificationPayload Payload { get; init; } = null!;
}

public record SubscriptionMessageNotificationPayload
{
    [JsonPropertyName("event")]
    public SubscriptionMessageEvent Event { get; init; } = null!;
}

public record SubscriptionMessageEvent
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

    [JsonPropertyName("tier")]
    public string Tier { get; init; } = null!;

    [JsonPropertyName("message")]
    public SubscriptionMessageBody Message { get; init; } = null!;

    [JsonPropertyName("cumulative_months")]
    public int CumulativeMonths { get; init; }

    // Null if the resubscriber has opted out of sharing their streak.
    [JsonPropertyName("streak_months")]
    public int? StreakMonths { get; init; }

    [JsonPropertyName("duration_months")]
    public int DurationMonths { get; init; }
}

public record SubscriptionMessageBody
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("emotes")]
    public List<SubscriptionMessageEmote> Emotes { get; init; } = [];
}

public record SubscriptionMessageEmote
{
    [JsonPropertyName("begin")]
    public int Begin { get; init; }

    [JsonPropertyName("end")]
    public int End { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;
}
