namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.raid EventSub notification.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record RaidNotification
{
    [JsonPropertyName("payload")]
    public RaidNotificationPayload Payload { get; init; } = null!;
}

public record RaidNotificationPayload
{
    [JsonPropertyName("event")]
    public RaidEvent Event { get; init; } = null!;
}

public record RaidEvent
{
    [JsonPropertyName("from_broadcaster_user_id")]
    public string FromBroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("from_broadcaster_user_login")]
    public string FromBroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("from_broadcaster_user_name")]
    public string FromBroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("to_broadcaster_user_id")]
    public string ToBroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("to_broadcaster_user_login")]
    public string ToBroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("to_broadcaster_user_name")]
    public string ToBroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("viewers")]
    public int Viewers { get; init; }
}
