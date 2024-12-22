namespace Application.Features;

public record ProcessMessageCommand(ChatMessage ChatMessage, string ThreadId) : IRequest;

public partial class ProcessMessageCommandHandler(
    IChatService chatService,
    IAIClient aiClient,
    IAssistantClient assistantClient,
    IPubgAIClient aiPubgAIClient,
    IModerationClient moderationClient,
    IMediator mediator,
    ChatOptions options,
    IPubgApiClient pubgApiClient,
    ILogger<ProcessMessageCommandHandler> logger,
    ILoggerFactory loggerFactory
    ) : IRequestHandler<ProcessMessageCommand>
{
    private readonly IChatService chatService = chatService;
    private readonly IAIClient aiClient = aiClient;
    private readonly IAssistantClient assistantClient = assistantClient;
    private readonly IPubgAIClient aiPubgAIClient = aiPubgAIClient;
    private readonly IModerationClient moderationClient = moderationClient;
    private readonly IMediator mediator = mediator;
    private readonly ChatOptions options = options;
    private readonly IPubgApiClient pubgApiClient = pubgApiClient;
    private readonly ILogger<ProcessMessageCommandHandler> logger = logger;
    private readonly ILogger moderationLogger = loggerFactory.CreateLogger("Moderation");
    private readonly FixedMessageQueue messages = new(5);
    private readonly Random random = new((int)DateTime.Now.Ticks);
    private double RandomResponseChance => options.RandomResponseChance;

    [GeneratedRegex(@"^!match\s([a-f0-9-]+)\s(\S+)\s(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex MatchRegex();

    public async Task Handle(ProcessMessageCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Channel} - {Username}: {Message}", request.ChatMessage.Channel, request.ChatMessage.Username, request.ChatMessage.Message);

        // Check for moderation classification
        var classificationResult = await moderationClient.Classify(request.ChatMessage.Message, cancellationToken);
        var classificationResultJson = JsonSerializer.Serialize(classificationResult);
        // logger.LogInformation("Moderation: {ModerationResult}", classificationResultJson);
        moderationLogger.LogInformation("ModerationLogger: {ModerationResult}", classificationResultJson);

        // Check if the bot is connected to this channel
        if (!chatService.JoinedChannels.Any(c => c.Channel.Equals(request.ChatMessage.Channel, StringComparison.CurrentCultureIgnoreCase)))
        {
            logger.LogWarning("Bot is not connected to channel {Channel}", request.ChatMessage.Channel);

            // Try to join the channel
            await chatService.JoinChannel(request.ChatMessage.Channel, cancellationToken);
            return;
        }

        if (request.ChatMessage.Message.Equals("!clip", StringComparison.CurrentCultureIgnoreCase))
        {
            // Create a clip
            CreateClipCommand clipCommand = new(request.ChatMessage.Channel);
            await mediator.Send(clipCommand, cancellationToken);
            return;
        }

        if (request.ChatMessage.Message.StartsWith("!match", StringComparison.CurrentCultureIgnoreCase))
        {
            try
            {
                if (MatchRegex().IsMatch(request.ChatMessage.Message))
                {
                    var regexMatch = MatchRegex().Match(request.ChatMessage.Message);

                    // Find the match stats
                    var matchId = regexMatch.Groups[1].Value;
                    var mainParticipantName = regexMatch.Groups[2].Value;
                    var match = await pubgApiClient.GetMatch(matchId, cancellationToken);

                    var additionalPrompt = regexMatch.Groups[3].Value;

                    var response = await aiPubgAIClient.GetPubgCompletion(additionalPrompt, match, mainParticipantName);

                    await chatService.SendReply(request.ChatMessage.Channel, request.ChatMessage.Id, response, cancellationToken);
                }
                else
                {
                    await chatService.SendReply(request.ChatMessage.Channel, request.ChatMessage.Id, "Didn't understand that !match command", cancellationToken);
                }
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex.Message);
            }

            return;
        }

        messages.Enqueue(new HistoryMessage(request.ChatMessage.Channel, request.ChatMessage.Username, request.ChatMessage.Message, DateTime.Now));
        logger.LogTrace("Messages: {Messages}", string.Join(", ", messages));
        logger.LogTrace("Summary: {Summary}", messages.GetExtractiveSummary(request.ChatMessage.Channel));

        await assistantClient.AddMessage(request.ThreadId, request.ChatMessage.Username, string.Empty, request.ChatMessage.Message);

        // Ignore messages from some users (like yourself)
        if (
            request.ChatMessage.Username.Equals(options.Username, StringComparison.CurrentCultureIgnoreCase)
            || options.IgnoreChatMessagesFrom.Contains(request.ChatMessage.Username, StringComparer.CurrentCultureIgnoreCase)
        )
        {
            return;
        }

        var randomResponseChance = random.NextDouble();
        logger.LogDebug("Random value: {RandomValue}", randomResponseChance);

        if (request.ChatMessage.Message.Contains(options.Username, StringComparison.CurrentCultureIgnoreCase) || randomResponseChance > RandomResponseChance)
        {
            // If the message contains the bot's name, use only that message as a prompt
            string completion = await assistantClient.RunAndWait(request.ThreadId);

            logger.LogDebug("Completion ({Channel}): {Completion}", request.ChatMessage.Channel, completion);

            if (request.ChatMessage.Channel.Equals(options.Channel, StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    await chatService.SendReply(request.ChatMessage.Channel, request.ChatMessage.Id, completion, cancellationToken);
                }
                catch (System.Exception ex)
                {
                    logger.LogError(ex, "Error sending reply");
                    throw;
                }
            }
        }
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

#pragma warning restore OPENAI001
