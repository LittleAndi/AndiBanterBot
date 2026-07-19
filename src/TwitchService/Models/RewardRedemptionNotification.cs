namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.channel_points_custom_reward_redemption.add EventSub notification.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record RewardRedemptionNotification
{
    [JsonPropertyName("payload")]
    public RewardRedemptionNotificationPayload Payload { get; init; } = null!;
}

public record RewardRedemptionNotificationPayload
{
    [JsonPropertyName("event")]
    public RewardRedemptionEvent Event { get; init; } = null!;
}

public record RewardRedemptionEvent
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = null!;

    [JsonPropertyName("user_login")]
    public string UserLogin { get; init; } = null!;

    [JsonPropertyName("user_name")]
    public string UserName { get; init; } = null!;

    [JsonPropertyName("user_input")]
    public string UserInput { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;

    [JsonPropertyName("reward")]
    public RewardRedemptionReward Reward { get; init; } = null!;

    [JsonPropertyName("redeemed_at")]
    public DateTimeOffset RedeemedAt { get; init; }
}

public record RewardRedemptionReward
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; init; } = null!;

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;

    [JsonPropertyName("cost")]
    public int Cost { get; init; }
}
