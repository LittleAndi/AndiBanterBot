namespace Application.Features;

public class PubgBackgroundService(IPubgApiClient pubgApiClient, IPubgStorageClient pubgStorageClient, IChatService chatService, ChatOptions options, IPubgAIClient pubgAiClient, IAudioClient audioClient, ILogger<PubgBackgroundService> logger) : BackgroundService
{
    private readonly IPubgApiClient pubgApiClient = pubgApiClient;
    private readonly IPubgStorageClient pubgStorageClient = pubgStorageClient;
    private readonly IChatService chatService = chatService;
    private readonly ChatOptions options = options;
    private readonly IPubgAIClient pubgAiClient = pubgAiClient;
    private readonly IAudioClient audioClient = audioClient;
    private readonly ILogger<PubgBackgroundService> logger = logger;
    private HashSet<string> MatchIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var playerId = await pubgApiClient.FindPlayerId("LittleAndi", stoppingToken);
        if (string.IsNullOrWhiteSpace(playerId)) return;

        logger.LogDebug("PlayerId: {PlayerId}", playerId);

        // Get player info and check for new matches
        var playerInfo = await pubgApiClient.GetPlayerInfo(playerId, stoppingToken);
        logger.LogInformation("PlayerInfo: {PlayerInfo}", playerInfo);

        // Get all the match ids into MatchIds
        MatchIds = playerInfo.Data.Relationships.Matches.Data.Select(data => data.Id).ToHashSet();

        // Save the current matches
        foreach (var matchData in playerInfo.Data.Relationships.Matches.Data)
        {
            var match = await pubgApiClient.GetMatch(matchData.Id, stoppingToken);
            await pubgStorageClient.SaveMatch(matchData.Id, match, stoppingToken);
        }

        using PeriodicTimer timer = new(TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            // var playerStats = await pubgApiClient.GetPlayerLifetimeStats(playerId, stoppingToken);
            // logger.LogInformation("PlayerStats: {PlayerStats}", playerStats);

            // Get player info and check for new matches
            var timerPlayInfo = await pubgApiClient.GetPlayerInfo(playerId, stoppingToken);

            foreach (var matchData in timerPlayInfo.Data.Relationships.Matches.Data)
            {
                if (MatchIds.Add(matchData.Id))
                {
                    logger.LogInformation("New match found: {MatchId}", matchData.Id);

                    var match = await pubgApiClient.GetMatch(matchData.Id, stoppingToken);
                    await pubgStorageClient.SaveMatch(matchData.Id, match, stoppingToken);
                    var response = await pubgAiClient.GetPubgCompletion(@"", match, "LittleAndi");
                    await chatService.SendMessage(options.Channel, response, stoppingToken);

                    // Play audio
                    await audioClient.PlayTTS(response, GeneratedSpeechVoice.Echo, stoppingToken);
                }
            }
        }
    }
}
