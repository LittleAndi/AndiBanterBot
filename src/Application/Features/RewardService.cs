namespace Application.Features;

public interface IRewardService
{
    public Task ProcessRewardRedeem(RedemptionReward redemptionReward, string userInput, CancellationToken cancellationToken = default);
}

public class RewardService(IAudioClient audioClient) : IRewardService
{
    private readonly IAudioClient audioClient = audioClient;

    public async Task ProcessRewardRedeem(RedemptionReward redemptionReward, string userInput, CancellationToken cancellationToken = default)
    {
        // TTS = be354cd0-f485-4c3a-87c0-eed2a354c6b9
        if (redemptionReward.Id.Equals("be354cd0-f485-4c3a-87c0-eed2a354c6b9"))
        {
            var input = userInput.Replace("little2926", "");
            await audioClient.PlayTTS(input, GeneratedSpeechVoice.Nova, cancellationToken);
        }
    }
}
