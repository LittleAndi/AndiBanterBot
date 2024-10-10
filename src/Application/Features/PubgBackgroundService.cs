using System.Text.Json;
using Application.Infrastructure.Pubg;

namespace Application.Features;

public class PubgBackgroundService(IPubgApiClient pubgApiClient, IChatService chatService, ChatOptions options, IAIClient aiClient, ILogger<PubgBackgroundService> logger) : BackgroundService
{
    private readonly IPubgApiClient pubgApiClient = pubgApiClient;
    private readonly IChatService chatService = chatService;
    private readonly ChatOptions options = options;
    private readonly IAIClient aiClient = aiClient;
    private readonly ILogger<PubgBackgroundService> logger = logger;
    private HashSet<string> MatchIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var playerId = await pubgApiClient.FindPlayerId("LittleAndi", stoppingToken);
        logger.LogDebug("PlayerId: {PlayerId}", playerId);

        // Get player info and check for new matches
        var playerInfo = await pubgApiClient.GetPlayerInfo(playerId, stoppingToken);
        logger.LogInformation("PlayerInfo: {PlayerInfo}", playerInfo);

        // Get all the match ids into MatchIds
        MatchIds = playerInfo.Data.Relationships.Matches.Data.Select(data => data.Id).ToHashSet();


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
                    var response = await aiClient.GetCompletion(
                        @"Look at this JSON match statistics from a PUBG game.
                        Find who won the game, use the name attribute from the players that won.
                        Indicate if was a solo, duo or squad game.
                        Also find out how LittleAndi did in the game. " + JsonSerializer.Serialize(match, Infrastructure.Pubg.Models.Converter.Settings));
                    await chatService.SendMessage(options.Channel, $"New game found! {response} ({matchData.Id})", stoppingToken);

                    // Process the new match
                    // await pubgApiClient.GetMatchDetails(matchData.Id, stoppingToken);
                    // await pubgApiClient.GetMatchPlayerStats(matchData.Id, playerId, stoppingToken);
                    // await pubgApiClient.GetMatchTelemetry(matchData.Id, stoppingToken);
                }
            }
        }
    }
}
