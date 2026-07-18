namespace Application.Features.Twitch;

public record TwitchResponse
{
    [JsonPropertyName("metadata")]
    public Metadata Metadata { get; init; } = null!;

    [JsonPropertyName("payload")]
    public Payload Payload { get; init; } = null!;
}

public record Metadata
{
    [JsonPropertyName("message_id")]
    public string MessageId { get; init; } = null!;

    [JsonPropertyName("message_type")]
    public string MessageType { get; init; } = null!;

    [JsonPropertyName("message_timestamp")]
    public DateTime MessageTimestamp { get; init; }

    [JsonPropertyName("subscription_type")]
    public string? SubscriptionType { get; init; }

    [JsonPropertyName("subscription_version")]
    public string? SubscriptionVersion { get; init; }
}

public record Payload
{
    [JsonPropertyName("session")]
    public Session Session { get; init; } = null!;
}

public record Session
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;

    [JsonPropertyName("connected_at")]
    public DateTime ConnectedAt { get; init; }

    [JsonPropertyName("keepalive_timeout_seconds")]
    public int KeepaliveTimeoutSeconds { get; init; }

    [JsonPropertyName("reconnect_url")]
    public string? ReconnectUrl { get; init; }
}
