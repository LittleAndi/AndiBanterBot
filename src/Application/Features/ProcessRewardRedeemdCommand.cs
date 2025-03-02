namespace Application.Features;

public record ProcessRewardRedeemCommand(RedemptionReward RedemptionReward, string UserInput) : IRequest;

public class ProcessRewardRedeemHandler(IAudioClient audioClient) : IRequestHandler<ProcessRewardRedeemCommand>
{
    private readonly IAudioClient audioClient = audioClient;

    public async Task Handle(ProcessRewardRedeemCommand request, CancellationToken cancellationToken)
    {
        // TTS = be354cd0-f485-4c3a-87c0-eed2a354c6b9
        if (request.RedemptionReward.Id.Equals("be354cd0-f485-4c3a-87c0-eed2a354c6b9"))
        {
            //await chatService.SendMessage(request.Reward.ChannelId, request.Reward.Prompt, cancellationToken);
            var userInput = request.UserInput.Replace("little2926", "");
            await audioClient.PlayTTS(userInput, GeneratedSpeechVoice.Nova, cancellationToken);
        }
    }
}