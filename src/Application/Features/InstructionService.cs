namespace Application.Features;

public interface IInstructionService
{
    public Task ProcessInstruction(string instruction, string channel, CancellationToken cancellationToken = default);
}

public class InstructionService(
    IChatService chatService,
    IAIClient aIClient
) : IInstructionService
{
    private readonly IChatService chatService = chatService;
    private readonly IAIClient aIClient = aIClient;

    public async Task ProcessInstruction(string instruction, string channel, CancellationToken cancellationToken = default)
    {
        if (!chatService.JoinedChannels.Any(c => c.Channel.Equals(channel)))
        {
            // Try to join
            await chatService.JoinChannel(channel, cancellationToken);
        }

        string completion = await aIClient.GetCompletion(instruction);
        await chatService.SendMessage(channel, completion, cancellationToken);
    }
}
