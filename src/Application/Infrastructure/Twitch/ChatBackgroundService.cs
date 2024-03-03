using Application.Infrastructure.OpenAI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace Application.Infrastructure.Twitch;

public class ChatBackgroundService : IHostedService
{
    readonly TwitchClient client;
    private readonly IAIClient aiClient;
    private readonly ILogger<ChatBackgroundService> logger;
    private readonly Random random = new((int)DateTime.Now.Ticks);
    private readonly FixedQueue<string> messages = new(5);

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

    private async void Client_OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
    {
        logger.LogDebug("Whisper from {Username}: {Message}", e.WhisperMessage.Username, e.WhisperMessage.Message);

        if (e.WhisperMessage.Username.Equals("littleandi77", StringComparison.CurrentCultureIgnoreCase))
        {
            var prompt = e.WhisperMessage.Message;
            var completion = await aiClient.GetCompletion(prompt);
            client.SendMessage("littleandi77", completion);
        }
    }

    private async void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        logger.LogDebug("{Channel} - {Username}: {Message}", e.ChatMessage.Channel, e.ChatMessage.Username, e.ChatMessage.Message);

        messages.Enqueue(e.ChatMessage.Message);
        logger.LogDebug("Messages: {Messages}", string.Join(", ", messages));

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

        if (e.ChatMessage.Message.Contains("andibanterbot", StringComparison.CurrentCultureIgnoreCase)
            || randomValue > 50)
        {
            string completion = await aiClient.GetAwareCompletion(messages);
            client.SendMessage(e.ChatMessage.Channel, completion);
        }
    }

    private void Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
    }

    private void Client_OnLog(object? sender, OnLogArgs e)
    {
        logger.LogDebug("{LogTime}: {BotUsername} - {Message}", e.DateTime, e.BotUsername, e.Data);
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

internal class FixedQueue<T>(int capacity) : Queue<T>
{
    private readonly int _capacity = capacity;

    public new void Enqueue(T item)
    {
        if (Count == _capacity)
        {
            Dequeue();
        }
        base.Enqueue(item);
    }
}