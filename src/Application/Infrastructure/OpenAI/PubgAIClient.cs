using System.Text.Json;
using OpenAI.Chat;

namespace Application.Infrastructure.OpenAI;

public interface IPubgAIClient
{
    Task<string> GetPubgCompletion(string prompt, Pubg.Models.Match match);
}

public class PubgAIClient(PubgOpenAIClientOptions options) : IPubgAIClient
{
    private readonly ChatClient client = new(options.Model, options.ApiKey);

    private readonly string PubgGameSystemPrompt = @"
            Analyze LittleAndi performance. Account for the game type; solo, duo or squad.
            If it is a solo game, analyze LittleAndi's performance and highlight any unique aspects of the gameplay.
            If it is a squad or duo game, also analyze LittleAndi's team performance and highlight LittleAndi's contributions.
            LittleAndi's team is the ones that is in the same roster as LittleAndi.
            The 'relationships' and 'rosters' tells you about the groups of players playing together.
            Each 'roster' got 'participants' which are the members of the groups (can also be solos).
            If LittleAndi is not in the match as a participant, just create a general analysis of the game.
            Keep your reponse under 450 chars at all costs.
            You may use gamers lingo and shorten common words to keep response short.
            Replace words with emojis where possible.
    ";

    public async Task<string> GetPubgCompletion(string prompt, Pubg.Models.Match match)
    {
        ChatCompletion chatCompletion = await client.CompleteChatAsync(
            [
                new SystemChatMessage(PubgGameSystemPrompt),
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
}