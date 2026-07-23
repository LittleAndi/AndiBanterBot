namespace Application.Features.Twitch;

/// <summary>
/// Envelope for a channel.chat.message EventSub notification. Only the fields
/// the bot consumes are mapped; the raw message carries the full payload.
/// </summary>
public record ChatMessageNotification
{
    [JsonPropertyName("payload")]
    public ChatMessageNotificationPayload Payload { get; init; } = null!;
}

public record ChatMessageNotificationPayload
{
    [JsonPropertyName("event")]
    public ChatMessageEvent Event { get; init; } = null!;
}

public record ChatMessageEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = null!;

    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = null!;

    [JsonPropertyName("chatter_user_id")]
    public string ChatterUserId { get; init; } = null!;

    [JsonPropertyName("chatter_user_login")]
    public string ChatterUserLogin { get; init; } = null!;

    [JsonPropertyName("chatter_user_name")]
    public string ChatterUserName { get; init; } = null!;

    [JsonPropertyName("message_id")]
    public string MessageId { get; init; } = null!;

    [JsonPropertyName("message")]
    public ChatMessageBody Message { get; init; } = null!;

    [JsonPropertyName("reply")]
    public ChatMessageReply? Reply { get; init; }

    [JsonPropertyName("color")]
    public string? Color { get; init; }

    [JsonPropertyName("badges")]
    public List<ChatMessageBadge> Badges { get; init; } = [];
}

public record ChatMessageBody
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = null!;

    [JsonPropertyName("fragments")]
    public List<ChatMessageFragment> Fragments { get; init; } = [];
}

// type is "text", "cheermote", "emote", or "mention" - only emote is mapped further
// since it's the only one the overlay needs beyond the fragment's own text.
public record ChatMessageFragment
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = null!;

    [JsonPropertyName("text")]
    public string Text { get; init; } = null!;

    [JsonPropertyName("emote")]
    public ChatMessageEmote? Emote { get; init; }
}

public record ChatMessageEmote
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("emote_set_id")]
    public string EmoteSetId { get; init; } = null!;

    [JsonPropertyName("owner_id")]
    public string OwnerId { get; init; } = null!;

    [JsonPropertyName("format")]
    public List<string> Format { get; init; } = [];
}

public record ChatMessageBadge
{
    [JsonPropertyName("set_id")]
    public string SetId { get; init; } = null!;

    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    [JsonPropertyName("info")]
    public string Info { get; init; } = null!;
}

public record ChatMessageReply
{
    [JsonPropertyName("parent_message_id")]
    public string ParentMessageId { get; init; } = null!;

    [JsonPropertyName("parent_user_login")]
    public string ParentUserLogin { get; init; } = null!;
}
