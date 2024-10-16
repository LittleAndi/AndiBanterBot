namespace Application.Features;

public record ProcessRewardRedeemCommand(Redemption Redemption) : IRequest;

public class ProcessRewardRedeemHandler(IAudioClient audioClient, IChatService chatService) : IRequestHandler<ProcessRewardRedeemCommand>
{
    private readonly IAudioClient audioClient = audioClient;
    private readonly IChatService chatService = chatService;

    public async Task Handle(ProcessRewardRedeemCommand request, CancellationToken cancellationToken)
    {
        // TTS = be354cd0-f485-4c3a-87c0-eed2a354c6b9
        if (request.Redemption.Reward.Id.Equals("be354cd0-f485-4c3a-87c0-eed2a354c6b9"))
        {
            //await chatService.SendMessage(request.Reward.ChannelId, request.Reward.Prompt, cancellationToken);
            var userInput = request.Redemption.UserInput.Replace("little2926", "");
            await audioClient.PlayTTS(userInput, GeneratedSpeechVoice.Nova, cancellationToken);
        }
    }
}