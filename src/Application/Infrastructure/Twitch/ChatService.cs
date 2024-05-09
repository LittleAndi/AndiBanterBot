using System.Text.RegularExpressions;
using Application.Infrastructure.OpenAI;
using Microsoft.Extensions.Logging;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace Application.Infrastructure.Twitch;

public interface IChatService
{
    public Task StartAsync(string accessToken, CancellationToken cancellationToken);
}

public partial class ChatService(IAIClient aiClient, ILoggerFactory loggerFactory, ILogger<ChatService> logger, ChatOptions options) : IChatService
{
    readonly TwitchClient client = new(loggerFactory: loggerFactory);
    private readonly IAIClient aiClient = aiClient;
    private readonly ILogger<ChatService> logger = logger;
    private readonly ChatOptions options = options;
    private readonly Random random = new((int)DateTime.Now.Ticks);
    private readonly FixedMessageQueue messages = new(5);

    private Task Client_OnConnected(object? sender, OnConnectedEventArgs e)
    {
        logger.LogDebug("Connected to Twitch chats with {BotUsername}", e.BotUsername);
        return Task.CompletedTask;
    }

    private Task Client_OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
    {
        logger.LogDebug("New subscriber {Subscriber} in {Channel}", e.Subscriber.DisplayName, e.Channel);
        return Task.CompletedTask;
    }

    [GeneratedRegex(@"^join channel (?<channel>[\S\d]*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex WhisperChannelRegex();

    private async Task Client_OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
    {
        logger.LogInformation("Whisper from {Username}: {Message}", e.WhisperMessage.Username, e.WhisperMessage.Message);

        // Only accept whispers from the specified users
        if (!options.AcceptWhispersFrom.Contains(e.WhisperMessage.Username, StringComparer.CurrentCultureIgnoreCase)) return;

        var prompt = e.WhisperMessage.Message;

        // If the whisper is a command to join a channel, join the channel
        if (WhisperChannelRegex().IsMatch(prompt))
        {
            var match = WhisperChannelRegex().Match(prompt);
            var channel = match.Groups["channel"].Value;
            await client.JoinChannelAsync(channel);
            return;
        }

        // Otherwise, generate a completion and send it to the main channel
        var completion = await aiClient.GetCompletion(prompt);
        await client.SendMessageAsync(options.Channel, completion);
    }

    private async Task Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        logger.LogInformation("{Channel} - {Username}: {Message}", e.ChatMessage.Channel, e.ChatMessage.Username, e.ChatMessage.Message);

        // Check if the bot is connected to this channel
        if (!client.JoinedChannels.Any(c => c.Channel.Equals(e.ChatMessage.Channel, StringComparison.CurrentCultureIgnoreCase)))
        {
            logger.LogWarning("Bot is not connected to channel {Channel}", e.ChatMessage.Channel);
            return;
        }

        // foreach (var joinedChannel in client.JoinedChannels)
        // {
        //     logger.LogDebug("Joined channel: {Channel}", joinedChannel.Channel);
        // }

        messages.Enqueue(new HistoryMessage(e.ChatMessage.Channel, e.ChatMessage.Username, e.ChatMessage.Message, DateTime.Now));
        logger.LogTrace("Messages: {Messages}", string.Join(", ", messages));
        logger.LogTrace("Summary: {Summary}", messages.GetExtractiveSummary(e.ChatMessage.Channel));

        // Ignore messages from some users (like yourself)
        if (
            e.ChatMessage.Username.Equals(options.Username, StringComparison.CurrentCultureIgnoreCase)
            || options.IgnoreChatMessagesFrom.Contains(e.ChatMessage.Username, StringComparer.CurrentCultureIgnoreCase)
        )
        {
            return;
        }

        var randomResponseChance = random.NextDouble();
        logger.LogDebug("Random value: {RandomValue}", randomResponseChance);

        if (e.ChatMessage.Message.Contains(options.Username, StringComparison.CurrentCultureIgnoreCase))
        {
            // If the message contains the bot's name, use only that message as a prompt
            string completion = await aiClient.GetCompletion(e.ChatMessage.Message);

            logger.LogDebug("Completion ({Channel}): {Completion}", e.ChatMessage.Channel, completion);

            if (e.ChatMessage.Channel.Equals(options.Channel, StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    await client.SendReplyAsync(e.ChatMessage.Channel, e.ChatMessage.Id, completion);
                }
                catch (System.Exception ex)
                {
                    logger.LogError(ex, "Error sending reply");
                    throw;
                }
            }
        }
        else if (randomResponseChance > options.RandomResponseChance)
        {
            // If this is a random response, use some of the chat history to generate a response
            // This also empties the history queue

            // Get the messages into a list
            var historyMessages = new List<string>();
            while (messages.TryDequeue(out var message))
            {
                historyMessages.Add(message.Message);
            }
            string completion = await aiClient.GetAwareCompletion(historyMessages);
            logger.LogInformation("History aware completion ({Channel}): {Completion}", e.ChatMessage.Channel, completion);
            await client.SendMessageAsync(e.ChatMessage.Channel, completion);
        }
    }

    private Task Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        logger.LogDebug("{BotUsername} joined channel {Channel}", e.BotUsername, e.Channel);
        return Task.CompletedTask;
    }

    [GeneratedRegex(@"Received: PING|Received: PONG|Writing: PONG")]
    private static partial Regex PingPongRegex();

    public async Task StartAsync(string accessToken, CancellationToken cancellationToken)
    {
        ConnectionCredentials credentials = new(options.Username, accessToken);
        client.Initialize(credentials, options.Channel);

        client.OnJoinedChannel += Client_OnJoinedChannel;
        client.OnMessageReceived += Client_OnMessageReceived;
        client.OnWhisperReceived += Client_OnWhisperReceived;
        client.OnNewSubscriber += Client_OnNewSubscriber;
        client.OnConnected += Client_OnConnected;

        await client.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.DisconnectAsync();
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
