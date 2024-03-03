using Application.Common;
using Azure;
using Azure.AI.OpenAI;

namespace Application.Infrastructure.OpenAI;

public interface IAIClient
{
    Task<string> GetCompletion(string prompt);
    Task<string> GetAwareCompletion(IEnumerable<string> messages);
}

public class AIClient(OpenAIClientOptions options) : IAIClient
{
    private readonly OpenAIClient client = new(options.ApiKey);

    private readonly string SystemPrompt = @"Your primary role on this Twitch channel is to facilitate and enhance the interactions between human users.
            While you may receive mentions (@andibanterbot), these should be treated as proactive requests for assistance or information.
            The vast majority of messages in the chat are conversations between users and the streamer.
            Your task is to observe these conversations and respond appropriately, supporting the streamer and the community.
            Respond like a teenage girl from California, but usually one or two sentences (max 500 characters).
            If someone asks you to join a Stream Racer race, just say ""race"" or a scentence with ""race"" in it.";

    public async Task<string> GetAwareCompletion(IEnumerable<string> messages)
    {
        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = "gpt-4-0125-preview",
            ChoiceCount = 1,
            Messages = {
                new ChatRequestSystemMessage(SystemPrompt),
                new ChatRequestUserMessage(messages.Select(msg => new ChatMessageTextContentItem(msg)))
            }
        };

        Response<ChatCompletions> response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
        ChatResponseMessage chatResponseMessage = response.Value.Choices[0].Message;
        return chatResponseMessage.Content;
    }

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