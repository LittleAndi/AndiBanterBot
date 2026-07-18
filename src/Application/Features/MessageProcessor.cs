namespace Application.Features;

public interface IMessageProcessor
{
    public Task ProcessMessage(ChatMessage chatMessage, string threadId, CancellationToken cancellationToken = default);
    public Task ProcessWhisper(WhisperMessage whisperMessage, CancellationToken cancellationToken = default);
}

public partial class MessageProcessor(
    IChatService chatService,
    IAIClient aiClient,
    IAssistantClient assistantClient,
    // IPubgAIClient aiPubgAIClient,
    IModerationClient moderationClient,
    IClipService clipService,
    ChatOptions options,
    // IPubgApiClient pubgApiClient,
    // IPubgStorageClient pubgStorageClient,
    ILogger<MessageProcessor> logger,
    ILoggerFactory loggerFactory
    ) : IMessageProcessor
{
    private readonly IChatService chatService = chatService;
    private readonly IAIClient aiClient = aiClient;
    private readonly IAssistantClient assistantClient = assistantClient;
    // private readonly IPubgAIClient aiPubgAIClient = aiPubgAIClient;
    private readonly IModerationClient moderationClient = moderationClient;
    private readonly IClipService clipService = clipService;
    private readonly ChatOptions options = options;
    // private readonly IPubgApiClient pubgApiClient = pubgApiClient;
    // private readonly IPubgStorageClient pubgStorageClient = pubgStorageClient;
    private readonly ILogger<MessageProcessor> logger = logger;
    private readonly ILogger moderationLogger = loggerFactory.CreateLogger("Moderation");
    private readonly FixedMessageQueue messages = new(5);
    private readonly Random random = new((int)DateTime.Now.Ticks);
    private double RandomResponseChance => options.RandomResponseChance;

    [GeneratedRegex(@"^!match\s([a-f0-9-]+)\s(\S+)\s(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex MatchRegex();

    [GeneratedRegex(@"^join channel (?<channel>[\S\d]*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex WhisperChannelRegex();

    public async Task ProcessMessage(ChatMessage chatMessage, string threadId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("{Channel} - {Username}: {Message}", chatMessage.Channel, chatMessage.Username, chatMessage.Message);

        // Check for moderation classification
        var classificationResult = await moderationClient.Classify(chatMessage.Message, cancellationToken);
        var classificationResultJson = JsonSerializer.Serialize(classificationResult);
        moderationLogger.LogInformation("ModerationLogger: {ModerationResult}", classificationResultJson);

        // Check if the bot is connected to this channel
        if (!chatService.JoinedChannels.Any(c => c.Equals(chatMessage.Channel, StringComparison.CurrentCultureIgnoreCase)))
        {
            logger.LogWarning("Bot is not connected to channel {Channel}", chatMessage.Channel);

            // Try to join the channel
            await chatService.JoinChannel(chatMessage.Channel, cancellationToken);
            return;
        }

        if (chatMessage.Message.Equals("!clip", StringComparison.CurrentCultureIgnoreCase))
        {
            await clipService.CreateClipAsync(chatMessage.Channel, cancellationToken);
            return;
        }

        messages.Enqueue(new HistoryMessage(chatMessage.Channel, chatMessage.Username, chatMessage.Message, DateTime.Now));
        logger.LogTrace("Messages: {Messages}", string.Join(", ", messages));
        logger.LogTrace("Summary: {Summary}", messages.GetExtractiveSummary(chatMessage.Channel));

        await assistantClient.AddMessage(threadId, chatMessage.Username, string.Empty, chatMessage.Message);

        // Ignore messages from some users (like yourself)
        if (
            chatMessage.Username.Equals(options.Username, StringComparison.CurrentCultureIgnoreCase)
            || options.IgnoreChatMessagesFrom.Contains(chatMessage.Username, StringComparer.CurrentCultureIgnoreCase)
        )
        {
            return;
        }

        var randomResponseChance = random.NextDouble();
        logger.LogDebug("Random value: {RandomValue}", randomResponseChance);

        if (chatMessage.Message.Contains(options.Username, StringComparison.CurrentCultureIgnoreCase) || randomResponseChance > RandomResponseChance)
        {
            // If the message contains the bot's name, use only that message as a prompt
            string completion = await assistantClient.RunAndWait(threadId);

            logger.LogDebug("Completion ({Channel}): {Completion}", chatMessage.Channel, completion);

            if (chatMessage.Channel.Equals(options.Channel, StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    await chatService.SendReply(chatMessage.Channel, chatMessage.Id, completion, cancellationToken);
                }
                catch (System.Exception ex)
                {
                    logger.LogError(ex, "Error sending reply");
                    throw;
                }
            }
        }
    }

    public async Task ProcessWhisper(WhisperMessage whisperMessage, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Whisper from {Username}: {Message}", whisperMessage.Username, whisperMessage.Message);

        // Only accept whispers from the specified users
        if (!options.AcceptWhispersFrom.Contains(whisperMessage.Username, StringComparer.CurrentCultureIgnoreCase)) return;

        var prompt = whisperMessage.Message;

        // If the whisper is a command to join a channel, join the channel
        if (WhisperChannelRegex().IsMatch(prompt))
        {
            var match = WhisperChannelRegex().Match(prompt);
            var channel = match.Groups["channel"].Value;
            await chatService.JoinChannel(channel, cancellationToken);
            return;
        }

        // Otherwise, generate a completion and send it to the main channel
        var completion = await aiClient.GetCompletion(prompt);
        await chatService.SendMessage(options.Channel, completion, cancellationToken);
    }
}

internal class FixedMessageQueue(int capacity) : Queue<HistoryMessage>
{
    private readonly int _capacity = capacity;

    public new void Enqueue(HistoryMessage item)
    {
        // Dequeue everything older than 5 minutes
        DequeueOldMessages(5);

        // If the queue is full, dequeue the oldest message
        if (Count == _capacity)
        {
            Dequeue();
        }

        base.Enqueue(item);
    }

    private void DequeueOldMessages(int minutes)
    {
        var now = DateTime.Now;
        while (Count > 0 && Peek().Timestamp < now - TimeSpan.FromMinutes(minutes))
        {
            Dequeue();
        }
    }

    public string GetExtractiveSummary(string channel, int numSentences = 3)
    {
        var text = string.Join('\n', this.Where(hm => hm.Channel.Equals(channel)).Select(s => s.Message).ToArray());

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Split the text into sentences
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        // Calculate sentence scores based on factors like word count and position
        var scores = new List<double>(sentences.Count);
        for (int i = 0; i < sentences.Count; i++)
        {
            var sentence = sentences[i].Trim();
            double score = sentence.Length; // Base score based on word count
            if (i == 0 || i == sentences.Count - 1)
            {
                score *= 1.2; // Increase score for beginning and end sentences
            }
            scores.Add(score);
        }

        // Select the top scoring sentences
        var selectedSentences = sentences.OrderByDescending(x => scores[sentences.IndexOf(x)]).Take(numSentences).ToList();

        // Join the selected sentences and return the summary
        return string.Join(" ", selectedSentences);
    }
}

public record HistoryMessage(string Channel, string Username, string Message, DateTime Timestamp);
