namespace Application.Features;

public record ProcessInstructionCommand(string Instruction, string Channel) : IRequest;

public class ProcessInstructionHandler(
    IChatService chatService,
    IAIClient aIClient
) : IRequestHandler<ProcessInstructionCommand>
{
    private readonly IChatService chatService = chatService;
    private readonly IAIClient aIClient = aIClient;

    public async Task Handle(ProcessInstructionCommand request, CancellationToken cancellationToken)
    {
        if (!chatService.JoinedChannels.Any(c => c.Channel.Equals(request.Channel)))
        {
            // Try to join
            await chatService.JoinChannel(request.Channel, cancellationToken);
        }

        string completion = await aIClient.GetCompletion(request.Instruction);
        await chatService.SendMessage(request.Channel, completion, cancellationToken);
    }
}