using TwitchLib.Api;

namespace Application.Infrastructure.Twitch;

public interface IClipService
{
    public void Start(string accessToken);
    public Task<CreateClipResult> CreateClipAsync(string channel, CancellationToken cancellationToken = default);
}

public record CreateClipResult(bool ClipCreated, string ClipId, string EditUrl, string Url);

public class ClipService(ILogger<ClipService> logger, ChatOptions options, IMediator mediator) : IClipService
{
    private readonly TwitchAPI twitchApi = new();
    private readonly ILogger<ClipService> logger = logger;
    private readonly ChatOptions options = options;
    private readonly IMediator mediator = mediator;

    public void Start(string accessToken)
    {
        twitchApi.Settings.ClientId = options.ClientId;
        twitchApi.Settings.AccessToken = accessToken;
    }

    public async Task<CreateClipResult> CreateClipAsync(string channel, CancellationToken cancellationToken)
    {
        try
        {
            // Convert channel name to broadcaster ID
            var users = await twitchApi.Helix.Users.GetUsersAsync(logins: [channel]);
            var broadcasterId = users.Users.First().Id;

            // Create clip
            var createdClipResponse = await twitchApi.Helix.Clips.CreateClipAsync(broadcasterId);
            var createdClip = createdClipResponse.CreatedClips.First();
            logger.LogInformation("Created clip {ClipId} for channel {Channel}, {EditUrl}", createdClip.Id, options.Channel, createdClip.EditUrl);

            // Send message to chat
            var url = createdClip.EditUrl[..^5];
            await mediator.Publish(new SendMessageNotification(options.Channel, $"Clip created! {url}"), cancellationToken);

            return new CreateClipResult(true, createdClip.Id, createdClip.EditUrl, url);
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Error creating clip for channel {Channel}", options.Channel);
            await mediator.Publish(new SendMessageNotification(options.Channel, "Couldn't create clip at the moment, try again later."), cancellationToken);
            return new CreateClipResult(false, string.Empty, string.Empty, string.Empty);
        }
    }
}