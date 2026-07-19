namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.poll.begin or channel.poll.progress EventSub notification.
/// Both subscription types share the same event payload shape.
/// Only the fields the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record PollNotification
{
    [JsonPropertyName("payload")]
    public PollNotificationPayload Payload { get; init; } = null!;
}

public record PollNotificationPayload
{
    [JsonPropertyName("event")]
    public PollEvent Event { get; init; } = null!;
}

public record PollEvent
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

    [JsonPropertyName("choices")]
    public List<PollChoice> Choices { get; init; } = [];

    [JsonPropertyName("bits_voting")]
    public PollVoting BitsVoting { get; init; } = null!;

    [JsonPropertyName("channel_points_voting")]
    public PollVoting ChannelPointsVoting { get; init; } = null!;

    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("ends_at")]
    public DateTimeOffset EndsAt { get; init; }
}

public record PollChoice
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("bits_votes")]
    public int BitsVotes { get; init; }

    [JsonPropertyName("channel_points_votes")]
    public int ChannelPointsVotes { get; init; }

    [JsonPropertyName("votes")]
    public int Votes { get; init; }
}

public record PollVoting
{
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; init; }

    [JsonPropertyName("amount_per_vote")]
    public int AmountPerVote { get; init; }
}

/// <summary>
/// Envelope for a channel.poll.end EventSub notification. Adds status/ended_at on top of
/// the begin/progress fields; kept as a separate type rather than optional fields on
/// <see cref="PollEvent"/> to match how channel.goal.end is modeled.
/// </summary>
public record PollEndNotification
{
    [JsonPropertyName("payload")]
    public PollEndNotificationPayload Payload { get; init; } = null!;
}

public record PollEndNotificationPayload
{
    [JsonPropertyName("event")]
    public PollEndEvent Event { get; init; } = null!;
}

public record PollEndEvent
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

    [JsonPropertyName("choices")]
    public List<PollChoice> Choices { get; init; } = [];

    [JsonPropertyName("bits_voting")]
    public PollVoting BitsVoting { get; init; } = null!;

    [JsonPropertyName("channel_points_voting")]
    public PollVoting ChannelPointsVoting { get; init; } = null!;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset EndedAt { get; init; }
}

/// <summary>
/// Current poll state as known to the service, or null when no poll is active.
/// Set from begin/progress notifications and cleared on end.
/// </summary>
public record PollStatusSnapshot(string Title, IReadOnlyList<PollChoiceStatus> Choices);

public record PollChoiceStatus(string Title, int Votes);
