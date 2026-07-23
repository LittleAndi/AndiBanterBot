using Application.Infrastructure.Pubg.Models;

namespace Application.Features;

public interface IPubgMatchFeedService
{
    IReadOnlyList<PubgMatchSummary> GetRecent();
    void Add(string matchId, Match match, string playerName);
}

public record PubgMatchSummary(
    string MatchId,
    DateTimeOffset CreatedAt,
    string MapName,
    string GameMode,
    string MatchType,
    long DurationSeconds,
    bool IsCustomMatch,
    PubgPlayerStats? PlayerStats);

public record PubgPlayerStats(
    long? Kills,
    long? Assists,
    long? DbnOs,
    double? DamageDealt,
    long? HeadshotKills,
    long? Heals,
    long? KillPlace,
    long? WinPlace,
    double? LongestKill,
    long? Revives,
    long? TimeSurvived,
    long? Boosts);

/// <summary>
/// Buffers recently processed matches in memory so overlay/dashboard consumers have something to
/// poll without reading through to blob storage on every request - same pattern as
/// TwitchActivityFeedService in TwitchService. The buffer is process-lifetime only: a PubgService
/// restart clears history, but the full match data remains durably stored in blob storage via
/// IPubgStorageClient.
/// </summary>
public class PubgMatchFeedService : IPubgMatchFeedService
{
    private const int MaxEntries = 20;
    private readonly Lock gate = new();
    private readonly LinkedList<PubgMatchSummary> entries = new();

    public IReadOnlyList<PubgMatchSummary> GetRecent()
    {
        lock (gate)
        {
            return [.. entries];
        }
    }

    public void Add(string matchId, Match match, string playerName)
    {
        var stats = match.Included
            .Select(included => included.Attributes.Stats)
            .FirstOrDefault(stats => stats?.Name == playerName);

        var summary = new PubgMatchSummary(
            matchId,
            match.Data.Attributes.CreatedAt,
            match.Data.Attributes.MapName,
            match.Data.Attributes.GameMode,
            match.Data.Attributes.MatchType,
            match.Data.Attributes.Duration,
            match.Data.Attributes.IsCustomMatch,
            stats is null ? null : new PubgPlayerStats(
                stats.Kills,
                stats.Assists,
                stats.DbnOs,
                stats.DamageDealt,
                stats.HeadshotKills,
                stats.Heals,
                stats.KillPlace,
                stats.WinPlace,
                stats.LongestKill,
                stats.Revives,
                stats.TimeSurvived,
                stats.Boosts));

        lock (gate)
        {
            entries.AddFirst(summary);
            while (entries.Count > MaxEntries)
            {
                entries.RemoveLast();
            }
        }
    }
}
