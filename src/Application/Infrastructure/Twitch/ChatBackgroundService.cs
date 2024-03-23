using System.Text.RegularExpressions;
using Application.Infrastructure.OpenAI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace Application.Infrastructure.Twitch;

public partial class ChatBackgroundService : IHostedService
{
    readonly TwitchClient client;
    private readonly IAIClient aiClient;
    private readonly ILogger<ChatBackgroundService> logger;
    private readonly Random random = new((int)DateTime.Now.Ticks);
    private readonly FixedMessageQueue messages = new(5);

    public ChatBackgroundService(IAIClient aiClient, ILogger<ChatBackgroundService> logger, ChatOptions options)
    {
        ConnectionCredentials credentials = new(options.Username, options.AccessToken);
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };
        WebSocketClient customClient = new(clientOptions);
        client = new TwitchClient(customClient);
        client.Initialize(credentials, options.Channel);

        client.OnLog += Client_OnLog;
        client.OnJoinedChannel += Client_OnJoinedChannel;
        client.OnMessageReceived += Client_OnMessageReceived;
        client.OnWhisperReceived += Client_OnWhisperReceived;
        client.OnNewSubscriber += Client_OnNewSubscriber;
        client.OnConnected += Client_OnConnected;

        this.aiClient = aiClient;
        this.logger = logger;
    }

    private void Client_OnConnected(object? sender, OnConnectedArgs e)
    {
        logger.LogDebug("Connected to Twitch chat {Channel}", e.AutoJoinChannel);
    }

    private void Client_OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
    {
        logger.LogDebug("New subscriber {Subscriber} in {Channel}", e.Subscriber.DisplayName, e.Channel);
    }

    [GeneratedRegex(@"^join channel (?<channel>[\S\d]*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex WhisperChannelRegex();

    private async void Client_OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
    {
        logger.LogDebug("Whisper from {Username}: {Message}", e.WhisperMessage.Username, e.WhisperMessage.Message);

        if (!e.WhisperMessage.Username.Equals("littleandi77", StringComparison.CurrentCultureIgnoreCase)) return;

        var prompt = e.WhisperMessage.Message;

        if (WhisperChannelRegex().IsMatch(prompt))
        {
            var match = WhisperChannelRegex().Match(prompt);
            var channel = match.Groups["channel"].Value;
            client.JoinChannel(channel);
            return;
        }

        if (e.WhisperMessage.Username.Equals("littleandi77", StringComparison.CurrentCultureIgnoreCase))
        {
            var completion = await aiClient.GetCompletion(prompt);
            client.SendMessage("littleandi77", completion);
        }
    }

    private async void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        logger.LogDebug("{Channel} - {Username}: {Message}", e.ChatMessage.Channel, e.ChatMessage.Username, e.ChatMessage.Message);

        messages.Enqueue(new HistoryMessage(e.ChatMessage.Channel, e.ChatMessage.Username, e.ChatMessage.Message, DateTime.Now));
        logger.LogDebug("Messages: {Messages}", string.Join(", ", messages));
        logger.LogDebug("Summary: {Summary}", messages.GetExtractiveSummary(e.ChatMessage.Channel));

        if (e.ChatMessage.Username.Equals("andibanterbot", StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        if (e.ChatMessage.Username.Equals("kofistreambot", StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        var randomValue = random.Next(100);
        logger.LogDebug("Random value: {RandomValue}", randomValue);

        if (e.ChatMessage.Message.Contains("andibanterbot", StringComparison.CurrentCultureIgnoreCase))
        {
            // If the message contains the bot's name, use only that message as a prompt
            string completion = await aiClient.GetCompletion(e.ChatMessage.Message);

            logger.LogDebug("Completion ({Channel}): {Completion}", e.ChatMessage.Channel, completion);

            if (e.ChatMessage.Channel.Equals("littleandi77", StringComparison.CurrentCultureIgnoreCase))
            {
                client.SendMessage(e.ChatMessage.Channel, completion);
            }
        }
        else if (randomValue > 70)
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
            logger.LogDebug("Completion ({Channel}): {Completion}", e.ChatMessage.Channel, completion);
            client.SendMessage(e.ChatMessage.Channel, completion);
        }
    }

    private void Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
    }

    [GeneratedRegex(@"Received: PING|Received: PONG|Writing: PONG")]
    private static partial Regex PingPongRegex();
    private void Client_OnLog(object? sender, OnLogArgs e)
    {
        if (PingPongRegex().IsMatch(e.Data)) return;

        logger.LogDebug("{Message}", e.Data);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        client.Connect();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        client.Disconnect();
        return Task.CompletedTask;
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
