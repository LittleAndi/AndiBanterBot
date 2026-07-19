namespace Application.Features.Twitch;

public interface ITwitchChatService
{
    Task<ChatSendResult> SendMessageAsync(string message, string? replyParentMessageId = null, CancellationToken cancellationToken = default);
}

public record ChatSendResult(bool Sent, string? MessageId, string? DropReason, bool RateLimited = false, DateTimeOffset? RateLimitResetAt = null);

/// <summary>
/// Sends chat messages through the Helix API (POST /helix/chat/messages) as the
/// bot account. Authenticates via an app access token, relying on the broadcaster's
/// channel:bot grant and the bot's user:bot grant rather than a refreshed bot user token.
/// </summary>
public class TwitchChatService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ITwitchTokenStore tokenStore,
    ITwitchUserApi twitchUserApi,
    ILogger<TwitchChatService> logger) : ITwitchChatService
{
    private readonly HttpClient twitchHttpClientAppAccess = httpClientFactory.CreateClient("TwitchClientAppAccess");
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

        var response = await twitchHttpClientAppAccess.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var resetAt = TryParseRateLimitReset(response.Headers);
            logger.LogWarning("Chat message to #{Broadcaster} was rate limited by Twitch. Resets at {ResetAt}", broadcasterUsername, resetAt);
            return new ChatSendResult(false, null, "Rate limited by Twitch", RateLimited: true, RateLimitResetAt: resetAt);
        }

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
        string? dropReasonCode = null;
        if (!isSent && data.TryGetProperty("drop_reason", out var drop) && drop.ValueKind == JsonValueKind.Object)
        {
            dropReason = drop.GetProperty("message").GetString();
            dropReasonCode = drop.TryGetProperty("code", out var codeProperty) ? codeProperty.GetString() : null;
        }

        if (isSent)
        {
            logger.LogInformation("Sent chat message {MessageId} to #{Broadcaster}", messageId, broadcasterUsername);
        }
        else
        {
            logger.LogWarning("Chat message to #{Broadcaster} was dropped ({DropReasonCode}): {DropReason}", broadcasterUsername, dropReasonCode, dropReason);
        }

        return new ChatSendResult(isSent, messageId, dropReason);
    }

    /// <summary>
    /// Twitch's Helix rate limit headers report the bucket reset time as a Unix
    /// timestamp in <c>Ratelimit-Reset</c>, regardless of endpoint.
    /// </summary>
    private static DateTimeOffset? TryParseRateLimitReset(HttpResponseHeaders headers)
    {
        if (headers.TryGetValues("Ratelimit-Reset", out var values) &&
            long.TryParse(values.FirstOrDefault(), out var epochSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
        }

        return null;
    }
}
