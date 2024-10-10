using OpenAI;
using OpenAI.Moderations;

namespace Application.Infrastructure.OpenAI;

public interface IModerationClient
{
    Task<ModerationResult> Classify(string message, CancellationToken cancellationToken = default);
}

public class ModerationClient(OpenAIClientOptions options) : IModerationClient
{
    private readonly OpenAIClientOptions openAIClientOptions = options;
    private readonly OpenAIClient openAIClient = new(options.ApiKey);

    public async Task<ModerationResult> Classify(string message, CancellationToken cancellationToken = default)
    {
        var client = openAIClient.GetModerationClient(openAIClientOptions.ModerationModel);
        var result = await client.ClassifyTextAsync(message, cancellationToken);
        return result.Value;
    }
}