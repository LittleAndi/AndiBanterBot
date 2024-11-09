using System.Text.Json;
using OpenAI.Chat;

namespace Application.Infrastructure.OpenAI;

public interface IPubgAIClient
{
    Task<string> GetPubgCompletion(string prompt, Pubg.Models.Match match);
}

public class PubgAIClient(IOptionsMonitor<PubgOpenAIClientOptions> optionsMonitor) : IPubgAIClient
{
    private readonly ChatClient client = new(optionsMonitor.CurrentValue.Model, optionsMonitor.CurrentValue.ApiKey);

    public async Task<string> GetPubgCompletion(string prompt, Pubg.Models.Match match)
    {
        var options = optionsMonitor.CurrentValue;
        ChatCompletion chatCompletion = await client.CompleteChatAsync(
            [
                new SystemChatMessage(options.PubgGameSystemPrompt),
                new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart(prompt),
                    ChatMessageContentPart.CreateTextPart(JsonSerializer.Serialize(match, Infrastructure.Pubg.Models.Converter.Settings))
                ),
            ]
        );

        return new string(chatCompletion.Content[0].Text);
    }
}

public class PubgOpenAIClientOptions : IConfigurationOptions
{
    public static string SectionName => "PubgOpenAI";

    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string PubgGameSystemPrompt { get; set; } = string.Empty;
}