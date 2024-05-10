using Application.Infrastructure.Twitch;

namespace Application.Features;

public record CreateClipCommand(string Channel) : IRequest<string>;

public class CreateClipCommandHandler(IClipService clipService) : IRequestHandler<CreateClipCommand, string>
{
    private readonly IClipService clipService = clipService;

    public async Task<string> Handle(CreateClipCommand request, CancellationToken cancellationToken)
    {
        var result = await clipService.CreateClipAsync(request.Channel, cancellationToken);
        return result.ClipId;
    }
}