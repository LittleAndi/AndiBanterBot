using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Infrastructure.Pubg.Models;

namespace Application.Infrastructure.Pubg;

public interface IPubgApiClient
{
    Task<string> FindPlayerId(string inGameName, CancellationToken cancellationToken = default);
    Task<PlayerInfo> GetPlayerInfo(string playerId, CancellationToken cancellationToken = default);
    Task<double> GetPlayerLifetimeStats(string playerId, CancellationToken cancellationToken = default);
    Task<Models.Match> GetMatch(string matchId, CancellationToken cancellationToken = default);
}

public class PubgApiClient(IHttpClientFactory httpClientFactory, PubgClientOptions pubgClientOptions, ILogger<PubgApiClient> logger) : IPubgApiClient
{
    private readonly HttpClient httpClient = httpClientFactory.CreateClient("pubg");
    private readonly PubgClientOptions pubgClientOptions = pubgClientOptions;
    private readonly ILogger<PubgApiClient> logger = logger;

    public async Task<string> FindPlayerId(string inGameName, CancellationToken cancellationToken = default)
    {
        var request = $"shards/{pubgClientOptions.Platform}/players?filter%5BplayerNames%5D={inGameName}";
        logger.LogDebug("{Request}", request);

        var pubgListOfData = await httpClient.GetFromJsonAsync<PubgListOfData>(request, cancellationToken: cancellationToken);

        if (pubgListOfData == null || (pubgListOfData?.Data.Count ?? 0) == 0)
        {
            logger.LogError("No player found with the given in-game name: {InGameName}", inGameName);
            throw new Exception("Player not found");
        }

        return pubgListOfData.Data.First().Id;
    }

    public async Task<Models.Match> GetMatch(string matchId, CancellationToken cancellationToken = default)
    {
        var request = $"shards/{pubgClientOptions.Platform}/matches/{matchId}";
        logger.LogDebug("{Request}", request);
        var match = await httpClient.GetFromJsonAsync<Models.Match>(request, Converter.Settings, cancellationToken: cancellationToken);
        if (match == null)
        {
            logger.LogError("No match found for match: {MatchId}", matchId);
            throw new Exception("No match found");
        }
        return match;
    }

    public async Task<PlayerInfo> GetPlayerInfo(string playerId, CancellationToken cancellationToken = default)
    {
        var request = $"shards/{pubgClientOptions.Platform}/players/{playerId}";
        logger.LogDebug("{Request}", request);
        var playerInfo = await httpClient.GetFromJsonAsync<PlayerInfo>(request, cancellationToken: cancellationToken);
        if (playerInfo == null)
        {
            logger.LogError("No player info found for player: {PlayerId}", playerId);
            throw new Exception("No player info found");
        }
        return playerInfo;
    }

    public async Task<double> GetPlayerLifetimeStats(string playerId, CancellationToken cancellationToken = default)
    {
        var request = $"shards/{pubgClientOptions.Platform}/players/{playerId}/seasons/lifetime";
        logger.LogDebug("{Request}", request);
        var lifetimeStats = await httpClient.GetFromJsonAsync<LifetimeStats>(request, cancellationToken: cancellationToken);

        if (lifetimeStats == null || lifetimeStats.Data == null || lifetimeStats.Data.Attributes == null)
        {
            logger.LogError("No lifetime stats found for player: {PlayerId}", playerId);
            throw new Exception("No lifetime stats found");
        }

        return lifetimeStats.Data.Attributes.GameModeStats.SquadFpp.RideDistance;
    }
}

public record PubgListOfData
{
    [JsonPropertyName("data")]
    public List<Data> Data { get; init; } = new();
}

public record Data
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public PlayerAttributes Attributes { get; set; } = new();
}

public record PlayerAttributes
{
    /*
    {
        "name": "LittleAndi",
        "stats": null,
        "titleId": "pubg",
        "shardId": "steam",
        "patchVersion": "",
        "banType": "Innocent",
        "clanId": "clan.883b492bcd674f118c3421ac9738f1a0"
    }
    */

    public string Name { get; set; } = string.Empty;
    public string ShardId { get; set; } = string.Empty;
    public string ClanId { get; set; } = string.Empty;
    public string TitleId { get; set; } = string.Empty;
    public string BanType { get; set; } = string.Empty;
    public string PatchVersion { get; set; } = string.Empty;
}

public record LifetimeStats
{
    public LifetimeStatsData Data { get; set; } = new();
}

public record LifetimeStatsData
{
    public LifetimeStatsAttributes Attributes { get; set; } = new();
}

public record LifetimeStatsAttributes
{
    public GameModeStats GameModeStats { get; set; } = new();
}

public record GameModeStats
{
    [JsonPropertyName("squad-fpp")]
    public GameModeStatusAttributes SquadFpp { get; set; } = new();
}

public record GameModeStatusAttributes
{
    public int Assists { get; set; }
    public int Boosts { get; set; }
    public int DbNOs { get; set; }
    public int DailyKills { get; set; }
    public int DailyWins { get; set; }
    public double DamageDealt { get; set; }
    public int Days { get; set; }
    public int HeadshotKills { get; set; }
    public int Heals { get; set; }
    public int KillPoints { get; set; }
    public int Kills { get; set; }
    public double LongestKill { get; set; }
    public int LongestTimeSurvived { get; set; }
    public int Losses { get; set; }
    public int MaxKillStreaks { get; set; }
    public int MostSurvivalTime { get; set; }
    public int RankPoints { get; set; }
    public string RankPointsTitle { get; set; } = string.Empty;
    public int Revives { get; set; }
    public double RideDistance { get; set; }
    public int RoadKills { get; set; }
    public int RoundMostKills { get; set; }
    public int RoundsPlayed { get; set; }
    public int Suicides { get; set; }
    public double SwimDistance { get; set; }
    public int TeamKills { get; set; }
    public double TimeSurvived { get; set; }
    public int Top10s { get; set; }
    public int VehicleDestroys { get; set; }
    public double WalkDistance { get; set; }
    public int WeaponsAcquired { get; set; }
    public int WeeklyKills { get; set; }
    public int WeeklyWins { get; set; }
    public int WinPoints { get; set; }
    public int Wins { get; set; }
}

public record PlayerInfo
{
    public PlayerInfoData Data { get; set; } = new();
}

public record PlayerInfoData
{
    public PlayerInfoRelationships Relationships { get; set; } = new();
}

public record PlayerInfoRelationships
{
    public PlayerInfoRelationshipsDataMatches Matches { get; set; } = new();
}

public record PlayerInfoRelationshipsDataMatches
{
    public IEnumerable<PlayerInfoRelationshipsDataMatchesData> Data { get; set; } = [];
}

public record PlayerInfoRelationshipsDataMatchesData
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}