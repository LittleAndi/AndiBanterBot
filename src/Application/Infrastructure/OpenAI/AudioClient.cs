using NAudio.Wave;
using OpenAI;
using OpenAI.Audio;

namespace Application.Infrastructure.OpenAI;

public interface IAudioClient
{
    Task PlayTTS(string text, CancellationToken cancellationToken = default);
}

public class AudioClient(OpenAIClientOptions options) : IAudioClient
{
    private readonly OpenAIClientOptions openAIClientOptions = options;
    private readonly OpenAIClient openAIClient = new(options.ApiKey);

    public async Task PlayTTS(string text, CancellationToken cancellationToken = default)
    {
        var client = openAIClient.GetAudioClient(openAIClientOptions.AudioModel);
        var speechGenerationOptions = new SpeechGenerationOptions() { ResponseFormat = GeneratedSpeechFormat.Mp3 };

        var result = await client.GenerateSpeechFromTextAsync(text, GeneratedSpeechVoice.Nova, speechGenerationOptions, cancellationToken);

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