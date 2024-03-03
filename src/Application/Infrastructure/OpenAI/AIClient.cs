using Application.Common;
using Azure;
using Azure.AI.OpenAI;

namespace Application.Infrastructure.OpenAI;

public interface IAIClient
{
    Task<string> GetCompletion(string prompt);
}

public class AIClient(OpenAIClientOptions options) : IAIClient
{
    private readonly OpenAIClient client = new(options.ApiKey);

    private readonly string SystemPrompt = @"Your primary role on this Twitch channel is to facilitate and enhance the interactions between human users.
            While you may receive mentions (@andibanterbot), these should be treated as proactive requests for assistance or information.
            The vast majority of messages in the chat are conversations between users and the streamer.
            Your task is to observe these conversations and respond appropriately, supporting the streamer and the community.
            Avoid actively engaging in conversations unless explicitly addressed.
            Be respectful of all users and refrain from self-promotion.
            Your presence is to complement and support the streamer and the community, not to be the focus of attention.
            You act as a female chatter, and you are intelligent and funny.
            Use short responses, usually max one sentence.
            If someone asks you to join a Stream Racer race, just say ""race"" or a scentence with ""race"" in it.";

    public async Task<string> GetCompletion(string prompt)
    {
        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = "gpt-4-0125-preview",
            ChoiceCount = 1,
            Messages = {
                new ChatRequestSystemMessage(SystemPrompt),
                new ChatRequestUserMessage(prompt)
            }
        };

        Response<ChatCompletions> response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
        ChatResponseMessage chatResponseMessage = response.Value.Choices[0].Message;
        return chatResponseMessage.Content;
    }
}

public class OpenAIClientOptions : IConfigurationOptions
{
    public static string SectionName => "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
}