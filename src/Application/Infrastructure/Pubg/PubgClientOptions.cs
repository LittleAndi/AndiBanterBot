namespace Application.Infrastructure.Pubg;

public class PubgClientOptions : IConfigurationOptions
{
    public static string SectionName => "Pubg";

    public string BaseAddress { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Storage { get; set; } = string.Empty;
}