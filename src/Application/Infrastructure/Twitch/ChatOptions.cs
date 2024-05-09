using Application.Common;

namespace Application.Infrastructure.Twitch;

public class ChatOptions : IConfigurationOptions
{
    static string IConfigurationOptions.SectionName => "TwitchChat";

    public string Username { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string[] AcceptWhispersFrom { get; set; } = [];
    public string[] IgnoreChatMessagesFrom { get; set; } = [];
    public double RandomResponseChance { get; set; } = 0.0;
}