namespace Application.Infrastructure.Twitch;

public interface IClipService
{
    Task<CreateClipResult> CreateClipAsync(string channel, CancellationToken cancellationToken = default);
}

public record CreateClipResult(bool ClipCreated, string ClipId, string EditUrl, string Url);
