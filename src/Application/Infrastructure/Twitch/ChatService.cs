using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace Application.Infrastructure.Twitch;

#pragma warning disable OPENAI001

public interface IChatService
{
    public Task StartAsync(string accessToken, CancellationToken cancellationToken);
    public Task SendMessage(string channel, string message, CancellationToken cancellationToken = default);
    public Task SendReply(string channel, string chatMessage, string reply, CancellationToken cancellationToken = default);
    public Task JoinChannel(string channel, CancellationToken cancellationToken = default);
    public IReadOnlyList<JoinedChannel> JoinedChannels { get; }
}

public partial class ChatService(ILoggerFactory loggerFactory, ILogger<ChatService> logger, ChatOptions options, IMediator mediator, IAssistantClient assistantClient) : IChatService
{
    readonly TwitchClient client = new(loggerFactory: loggerFactory);
    private readonly ILogger<ChatService> logger = logger;
    private readonly ChatOptions options = options;
    private readonly IMediator mediator = mediator;
    private readonly IAssistantClient assistantClient = assistantClient;
    private string threadId = string.Empty;


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

    private async Task Client_OnWhisperReceived(object? sender, OnWhisperReceivedArgs e)
    {
        var processWhisperCommand = new ProcessWhisperCommand(e.WhisperMessage);
        await mediator.Send(processWhisperCommand);
    }

    private async Task Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var processMessageCommand = new ProcessMessageCommand(e.ChatMessage, threadId);
        await mediator.Send(processMessageCommand);
    }

    private Task Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        logger.LogDebug("{BotUsername} joined channel {Channel}", e.BotUsername, e.Channel);
        return Task.CompletedTask;
    }

    public async Task StartAsync(string accessToken, CancellationToken cancellationToken)
    {
        threadId = await assistantClient.NewThread(new ThreadCreationOptions());

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

    public async Task SendMessage(string channel, string message, CancellationToken cancellationToken = default)
    {
        if (!client.IsConnected)
        {
            logger.LogWarning("Client is not connected");
            return;
        }
        await client.SendMessageAsync(channel, message);
    }

    public async Task SendReply(string channel, string chatMessage, string reply, CancellationToken cancellationToken = default)
    {
        await client.SendReplyAsync(channel, chatMessage, reply);
    }

    public async Task JoinChannel(string channel, CancellationToken cancellationToken = default)
    {
        await client.JoinChannelAsync(channel);
    }

    public IReadOnlyList<JoinedChannel> JoinedChannels => client.JoinedChannels;
}

#pragma warning restore OPENAI001