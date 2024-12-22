using OpenAI.Chat;
using ChatMessage = OpenAI.Chat.ChatMessage;

namespace Application.Infrastructure.OpenAI;

public interface IAIClient
{
    Task<string> GetCompletion(string prompt);
    Task<string> GetAwareCompletion(IEnumerable<string> historyMessages);
}

public class AIClient(IOptionsMonitor<OpenAIClientOptions> optionsMonitor) : IAIClient
{
    private readonly ChatClient client = new(optionsMonitor.CurrentValue.Model, optionsMonitor.CurrentValue.ApiKey);

    public async Task<string> GetAwareCompletion(IEnumerable<string> historyMessages)
    {
        var options = optionsMonitor.CurrentValue;
        List<ChatMessage> chatMessages = [new SystemChatMessage(options.GeneralSystemPrompt)];
        chatMessages.AddRange(historyMessages.Select(msg => new UserChatMessage(msg)));
        ChatCompletion chatCompletion = await client.CompleteChatAsync([.. chatMessages]);

        return chatCompletion.Content[0].Text;
    }

    public async Task<string> GetCompletion(string prompt)
    {
        var options = optionsMonitor.CurrentValue;
        ChatCompletion chatCompletion = await client.CompleteChatAsync(
            [
                new SystemChatMessage(options.GeneralSystemPrompt),
                new UserChatMessage(prompt),
            ]
        );

        return chatCompletion.Content[0].Text;
    }
}

public class OpenAIClientOptions : IConfigurationOptions
{
    public static string SectionName => "OpenAI";

    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModerationModel { get; set; } = string.Empty;
    public string AudioModel { get; set; } = string.Empty;
    public Guid SoundOutDeviceGuid { get; set; } = Guid.Empty;
    public string AudioOutputPath { get; set; } = string.Empty;
    public string GeneralSystemPrompt { get; set; } = string.Empty;
    public string Assistant { get; set; } = string.Empty;
}