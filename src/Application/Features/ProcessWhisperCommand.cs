using TwitchLib.Client.Models;

namespace Application.Features;
public record ProcessWhisperCommand(WhisperMessage WhisperMessage) : IRequest;

public partial class ProcessWhisperCommandHandler(
    IChatService chatService,
    IAIClient aiClient,
    ChatOptions options,
    ILogger<ProcessWhisperCommandHandler> logger
    ) : IRequestHandler<ProcessWhisperCommand>
{
    private readonly IChatService chatService = chatService;
    private readonly IAIClient aiClient = aiClient;
    private readonly ChatOptions options = options;

    [GeneratedRegex(@"^join channel (?<channel>[\S\d]*)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex WhisperChannelRegex();

    public async Task Handle(ProcessWhisperCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Whisper from {Username}: {Message}", request.WhisperMessage.Username, request.WhisperMessage.Message);

        // Only accept whispers from the specified users
        if (!options.AcceptWhispersFrom.Contains(request.WhisperMessage.Username, StringComparer.CurrentCultureIgnoreCase)) return;

        var prompt = request.WhisperMessage.Message;

        // If the whisper is a command to join a channel, join the channel
        if (WhisperChannelRegex().IsMatch(prompt))
        {
            var match = WhisperChannelRegex().Match(prompt);
            var channel = match.Groups["channel"].Value;
            await chatService.JoinChannel(channel, cancellationToken);
            return;
        }

        // Otherwise, generate a completion and send it to the main channel
        var completion = await aiClient.GetCompletion(prompt);
        await chatService.SendMessage(options.Channel, completion, cancellationToken);
    }
}