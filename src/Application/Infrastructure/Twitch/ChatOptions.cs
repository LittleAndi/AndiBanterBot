using Application.Common;
using Microsoft.Extensions.Configuration;

namespace Application.Infrastructure.Twitch;

public class ChatOptions : IConfigurationOptions
{
    static string IConfigurationOptions.SectionName => "TwitchChat";

    public string Username { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
}