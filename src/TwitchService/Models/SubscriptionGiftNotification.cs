namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.subscription.gift EventSub notification.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record SubscriptionGiftNotification
{
    [JsonPropertyName("payload")]
    public SubscriptionGiftNotificationPayload Payload { get; init; } = null!;
}

public record SubscriptionGiftNotificationPayload
{
    [JsonPropertyName("event")]
    public SubscriptionGiftEvent Event { get; init; } = null!;
}

public record SubscriptionGiftEvent
{
    // Null when is_anonymous is true.
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("tier")]
    public string Tier { get; init; } = null!;

    // Null if anonymous or the gifter has opted out of sharing this total.
    [JsonPropertyName("cumulative_total")]
    public int? CumulativeTotal { get; init; }

    [JsonPropertyName("is_anonymous")]
    public bool IsAnonymous { get; init; }
}
