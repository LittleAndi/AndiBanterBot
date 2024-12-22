namespace Application.Infrastructure.OpenAI;

public record PubgAIMatchModel(MatchData MatchData, TeamData TeamData, IEnumerable<Participant> TeamParticipants)
{
    public static PubgAIMatchModel FromMatch(Pubg.Models.Match match, string mainParticipantName)
    {
        // Find the participants of the team (roster)
        var mainParticipant = match.Included.First(i => i.Type == Pubg.Models.TypeEnum.Participant && i.Attributes.Stats.Name == mainParticipantName);
        var roster = match.Included.First(i => i.Type == Pubg.Models.TypeEnum.Roster && i.Relationships.Participants.Data.Any(p => p.Id == mainParticipant.Id));
        List<Participant> rosterParticipants = [];

        foreach (var participantData in roster.Relationships.Participants.Data)
        {
            var participant = match.Included.First(i => i.Type == Pubg.Models.TypeEnum.Participant && i.Id == participantData.Id);
            rosterParticipants.Add(Participant.FromParticipant(participant.Attributes.Stats));
        }

        return new PubgAIMatchModel(MatchData.FromMatch(match.Data), TeamData.FromRoster(roster), rosterParticipants);
    }
}

public record MatchData(
    string MatchType,
    long Duration,
    string GameMode,
    string MapName,
    bool IsCustomMatch,
    string TitleId
)
{
    public static MatchData FromMatch(Pubg.Models.Data matchData)
    {
        return new MatchData(
            MatchType: matchData.Attributes.MatchType,
            Duration: matchData.Attributes.Duration,
            GameMode: matchData.Attributes.GameMode,
            MapName: matchData.Attributes.MapName,
            IsCustomMatch: matchData.Attributes.IsCustomMatch,
            TitleId: matchData.Attributes.TitleId
        );
    }
}

public record TeamData(
    long? TeamId,
    long? Rank,
    bool? Won
)
{
    public static TeamData FromRoster(Pubg.Models.Included roster)
    {
        return new TeamData(
            TeamId: roster.Attributes.Stats.TeamId,
            Rank: roster.Attributes.Stats.Rank,
            Won: roster.Attributes.Won
        );
    }
}

public record Participant(
    string Name,
    string PlayerId,
    long? KillPlace,
    long? WinPlace,
    long? Kills,
    long? Assists,
    double? DamageDealt,
    long? Heals,
    long? Revives,
    long? Boosts,
    long? WeaponsAcquired,
    double? WalkDistance,
    double? RideDistance,
    double? SwimDistance,
    long? TimeSurvived,
    long? RoadKills,
    long? TeamKills,
    long? HeadshotKills,
    double? LongestKill,
    long? VehicleDestroys,
    long? DbnOs,
    long? KillStreaks,
    string? DeathType
)
{
    public static Participant FromParticipant(Pubg.Models.Stats participant)
    {
        return new Participant(
            Name: participant.Name,
            PlayerId: participant.PlayerId,
            KillPlace: participant.KillPlace,
            WinPlace: participant.WinPlace,
            Kills: participant.Kills,
            Assists: participant.Assists,
            DamageDealt: participant.DamageDealt,
            Heals: participant.Heals,
            Revives: participant.Revives,
            Boosts: participant.Boosts,
            WeaponsAcquired: participant.WeaponsAcquired,
            WalkDistance: participant.WalkDistance,
            RideDistance: participant.RideDistance,
            SwimDistance: participant.SwimDistance,
            TimeSurvived: participant.TimeSurvived,
            RoadKills: participant.RoadKills,
            TeamKills: participant.TeamKills,
            HeadshotKills: participant.HeadshotKills,
            LongestKill: participant.LongestKill,
            VehicleDestroys: participant.VehicleDestroys,
            DbnOs: participant.DbnOs,
            KillStreaks: participant.KillStreaks,
            DeathType: participant.DeathType.ToString()
        );
    }
}