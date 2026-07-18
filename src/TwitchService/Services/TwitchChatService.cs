namespace Application.Features.Twitch;

public interface ITwitchChatService
{
    Task<ChatSendResult> SendMessageAsync(string message, string? replyParentMessageId = null, CancellationToken cancellationToken = default);
}

public record ChatSendResult(bool Sent, string? MessageId, string? DropReason);

/// <summary>
/// Sends chat messages through the Helix API (POST /helix/chat/messages) as the
/// bot account. Requires the bot token to carry the user:write:chat scope.
/// </summary>
public class TwitchChatService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ITwitchTokenStore tokenStore,
    ITwitchUserApi twitchUserApi,
    ILogger<TwitchChatService> logger) : ITwitchChatService
{
    private readonly HttpClient twitchHttpClientUserAccess = httpClientFactory.CreateClient("TwitchClientUserAccess");
    private readonly string broadcasterUsername = configuration["Twitch:BroadcasterUsername"] ?? throw new InvalidOperationException("BroadcasterUsername not configured");

    public async Task<ChatSendResult> SendMessageAsync(string message, string? replyParentMessageId = null, CancellationToken cancellationToken = default)
    {
        if (!tokenStore.GetStatus().TryGetValue(TwitchUserRole.Bot, out var bot) || string.IsNullOrEmpty(bot.UserId))
        {
            return new ChatSendResult(false, null, "No bot token stored, log in with the bot account first");
        }

        var broadcasterId = await twitchUserApi.GetUserIdAsync(broadcasterUsername, cancellationToken);
        if (broadcasterId is null)
        {
            return new ChatSendResult(false, null, $"Could not resolve broadcaster id for {broadcasterUsername}");
        }

        var body = new Dictionary<string, string>
        {
            ["broadcaster_id"] = broadcasterId,
            ["sender_id"] = bot.UserId,
            ["message"] = message,
        };
        if (!string.IsNullOrEmpty(replyParentMessageId))
        {
            body["reply_parent_message_id"] = replyParentMessageId;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "helix/chat/messages")
        {
            Content = JsonContent.Create(body)
        };
        request.Options.Set(HttpRequestOptionKeys.UserRole, TwitchUserRole.Bot);

        var response = await twitchHttpClientUserAccess.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to send chat message. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
            return new ChatSendResult(false, null, $"Twitch returned {(int)response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var data = doc.RootElement.GetProperty("data")[0];
        var messageId = data.GetProperty("message_id").GetString();
        var isSent = data.GetProperty("is_sent").GetBoolean();
        string? dropReason = null;
        if (!isSent && data.TryGetProperty("drop_reason", out var drop) && drop.ValueKind == JsonValueKind.Object)
        {
            dropReason = drop.GetProperty("message").GetString();
        }

        if (isSent)
        {
            logger.LogInformation("Sent chat message {MessageId} to #{Broadcaster}", messageId, broadcasterUsername);
        }
        else
        {
            logger.LogWarning("Chat message to #{Broadcaster} was dropped: {DropReason}", broadcasterUsername, dropReason);
        }

        return new ChatSendResult(isSent, messageId, dropReason);
    }
}
