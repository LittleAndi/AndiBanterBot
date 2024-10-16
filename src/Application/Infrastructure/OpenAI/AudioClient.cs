using NAudio.Wave;

namespace Application.Infrastructure.OpenAI;

public interface IAudioClient
{
    Task PlayTTS(string text, GeneratedSpeechVoice generatedSpeechVoice, CancellationToken cancellationToken = default);
}

public class AudioClient(OpenAIClientOptions options, IModerationClient moderationClient) : IAudioClient
{
    private readonly OpenAIClientOptions openAIClientOptions = options;
    private readonly IModerationClient moderationClient = moderationClient;
    private readonly OpenAIClient openAIClient = new(options.ApiKey);

    public async Task PlayTTS(string text, GeneratedSpeechVoice generatedSpeechVoice, CancellationToken cancellationToken = default)
    {
        // Moderate the input text
        var classificationResult = await moderationClient.Classify(text, cancellationToken);
        if (classificationResult.Flagged)
        {
            return;
        }

        var client = openAIClient.GetAudioClient(openAIClientOptions.AudioModel);
        var speechGenerationOptions = new SpeechGenerationOptions() { ResponseFormat = GeneratedSpeechFormat.Mp3 };

        var result = await client.GenerateSpeechAsync(text, generatedSpeechVoice, speechGenerationOptions, cancellationToken);

        // Save a copy of the speech to disk
        var filename = $"{DateTime.Now:yyyy-MM-dd-HHmm}_{Guid.NewGuid()}.mp3";
        await File.WriteAllBytesAsync(Path.Combine(openAIClientOptions.AudioOutputPath, filename), result.Value.ToArray(), cancellationToken);

        using var stream = new MemoryStream(result.Value.ToArray());
        using var mp3 = new Mp3FileReader(stream);

        using var outputDevice = new DirectSoundOut(openAIClientOptions.SoundOutDeviceGuid);

        outputDevice.Init(mp3);
        outputDevice.Play();

        while (outputDevice.PlaybackState == PlaybackState.Playing)
        {
            Thread.Sleep(500);
        }
    }
}