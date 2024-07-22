using NAudio.Wave;
using OpenAI;
using OpenAI.Audio;

namespace Application.Infrastructure.OpenAI;

public interface IAudioClient
{
    Task PlayTTS(string text);
}

public class AudioClient(OpenAIClientOptions options) : IAudioClient
{
    private readonly OpenAIClientOptions options = options;
    private OpenAIClient openAIClient = new(options.ApiKey);

    public async Task PlayTTS(string text)
    {
        var client = openAIClient.GetAudioClient("tts-1");
        var options = new SpeechGenerationOptions() { ResponseFormat = GeneratedSpeechFormat.Mp3 };

        var result = await client.GenerateSpeechFromTextAsync(text, GeneratedSpeechVoice.Nova, options);

        using var stream = new MemoryStream(result.Value.ToArray());
        using var mp3 = new Mp3FileReader(stream);

        using var outputDevice = new DirectSoundOut(Guid.Parse("b9a45f56-0ce6-4aa8-a187-8e606f73a860"));

        outputDevice.Init(mp3);
        outputDevice.Play();

        while (outputDevice.PlaybackState == PlaybackState.Playing)
        {
            Thread.Sleep(500);
        }
    }
}