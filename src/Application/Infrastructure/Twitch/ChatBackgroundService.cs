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
    private readonly ILogger<ChatBackgroundService> logger;

    public ChatBackgroundService(ILogger<ChatBackgroundService> logger, ChatOptions options)
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

    private void Client_OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
    {
        logger.LogDebug("Whisper from {Username}: {Message}", e.WhisperMessage.Username, e.WhisperMessage.Message);
    }

    private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        logger.LogDebug("{Channel} - {Username}: {Message}", e.ChatMessage.Channel, e.ChatMessage.Username, e.ChatMessage.Message);
    }

    private void Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        client.SendMessage(e.Channel, "Hey guys! I am a bot connected via TwitchLib!");
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
