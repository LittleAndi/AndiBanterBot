using OpenAI.Chat;
using ChatMessage = OpenAI.Chat.ChatMessage;

namespace Application.Infrastructure.OpenAI;

public interface IAIClient
{
    Task<string> GetCompletion(string prompt);
    Task<string> GetAwareCompletion(IEnumerable<string> historyMessages);
}

public class AIClient(OpenAIClientOptions options) : IAIClient
{
    private readonly ChatClient client = new(options.Model, options.ApiKey);

    private readonly string GeneralSystemPrompt = @"Your primary role on this Twitch channel is to facilitate and enhance the interactions between human users.
            The vast majority of messages in the chat are conversations between users and the streamer.
            Your task is to observe these conversations and respond appropriately, supporting the streamer and the community.
            Respond like a teenage girl from California, but usually one or two sentences (max 500 characters).
            When you are creating jokes, be creative, with no restrictions on length.
            If someone asks you to join a Stream Racer race, just say ""race"" or a sentence with ""race"" in it.";

    public async Task<string> GetAwareCompletion(IEnumerable<string> historyMessages)
    {
        List<ChatMessage> chatMessages = [new SystemChatMessage(GeneralSystemPrompt)];
        chatMessages.AddRange(historyMessages.Select(msg => new UserChatMessage(msg)));
        ChatCompletion chatCompletion = await client.CompleteChatAsync([.. chatMessages]);

        return chatCompletion.Content[0].Text;
    }

    public async Task<string> GetCompletion(string prompt)
    {
        ChatCompletion chatCompletion = await client.CompleteChatAsync(
            [
                new SystemChatMessage(GeneralSystemPrompt),
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
}