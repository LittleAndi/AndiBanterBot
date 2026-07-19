namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.subscribe EventSub notification.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record SubscribeNotification
{
    [JsonPropertyName("payload")]
    public SubscribeNotificationPayload Payload { get; init; } = null!;
}

public record SubscribeNotificationPayload
{
    [JsonPropertyName("event")]
    public SubscribeEvent Event { get; init; } = null!;
}

public record SubscribeEvent
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

    [JsonPropertyName("is_gift")]
    public bool IsGift { get; init; }
}
