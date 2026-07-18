using Microsoft.Extensions.Hosting;

namespace Application.Features;

/// <summary>
/// Connects chat events to the message processor at startup. Keeps
/// ChatService free of a dependency on the processor (which itself
/// depends on IChatService).
/// </summary>
public class ChatEventWiring(IChatService chatService, IMessageProcessor messageProcessor) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        chatService.MessageReceived += (chatMessage, threadId) => messageProcessor.ProcessMessage(chatMessage, threadId);
        chatService.WhisperReceived += whisperMessage => messageProcessor.ProcessWhisper(whisperMessage);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
