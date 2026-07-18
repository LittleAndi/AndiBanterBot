namespace Application.Infrastructure.Pubg.Models;

using System;
using System.Collections.Generic;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using J = System.Text.Json.Serialization.JsonPropertyNameAttribute;
using N = System.Text.Json.Serialization.JsonIgnoreCondition;

public partial class Match
{
    [J("data")] public required Data Data { get; set; }
    [J("included")] public required List<Included> Included { get; set; }
    [J("links")] public required MatchLinks Links { get; set; }
    [J("meta")] public required Meta Meta { get; set; }
}

public partial class Data
{
    [J("type")] public required string Type { get; set; }
    [J("id")] public required Guid Id { get; set; }
    [J("attributes")] public required DataAttributes Attributes { get; set; }
    [J("relationships")] public required DataRelationships Relationships { get; set; }
    [J("links")] public required DataLinks Links { get; set; }
}

public partial class DataAttributes
{
    [J("titleId")] public required string TitleId { get; set; }
    [J("shardId")] public ShardId ShardId { get; set; }
    [J("tags")] public required object Tags { get; set; }
    [J("mapName")] public required string MapName { get; set; }
    [J("isCustomMatch")] public bool IsCustomMatch { get; set; }
    [J("duration")] public long Duration { get; set; }
    [J("stats")] public required object Stats { get; set; }
    [J("gameMode")] public required string GameMode { get; set; }
    [J("seasonState")] public required string SeasonState { get; set; }
    [J("createdAt")] public DateTimeOffset CreatedAt { get; set; }
    [J("matchType")] public required string MatchType { get; set; }
}

public partial class DataLinks
{
    [J("self")] public required Uri Self { get; set; }
    [J("schema")] public required string Schema { get; set; }
}

public partial class DataRelationships
{
    [J("assets")] public required Assets Assets { get; set; }
    [J("rosters")] public required Assets Rosters { get; set; }
}

public partial class Assets
{
    [J("data")] public required List<Datum> Data { get; set; }
}

public partial class Datum
{
    [J("type")] public TypeEnum Type { get; set; }
    [J("id")] public Guid Id { get; set; }
}

public partial class Included
{
    [J("type")] public TypeEnum Type { get; set; }
    [J("id")] public Guid Id { get; set; }
    [J("attributes")] public required IncludedAttributes Attributes { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("relationships")] public IncludedRelationships? Relationships { get; set; }
}

public partial class IncludedAttributes
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("actor")] public string? Actor { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("shardId")] public ShardId? ShardId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("stats")] public Stats? Stats { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("won")][JsonConverter(typeof(ParseStringConverter))] public bool? Won { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("createdAt")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("URL")] public Uri? Url { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("name")] public string? Name { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("description")] public string? Description { get; set; }
}

public partial class Stats
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("DBNOs")] public long? DbnOs { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("assists")] public long? Assists { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("boosts")] public long? Boosts { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("damageDealt")] public double? DamageDealt { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("deathType")] public DeathType? DeathType { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("headshotKills")] public long? HeadshotKills { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("heals")] public long? Heals { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("killPlace")] public long? KillPlace { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("killStreaks")] public long? KillStreaks { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("kills")] public long? Kills { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("longestKill")] public double? LongestKill { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("name")] public string? Name { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("playerId")] public string? PlayerId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("revives")] public long? Revives { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("rideDistance")] public double? RideDistance { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("roadKills")] public long? RoadKills { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("swimDistance")] public double? SwimDistance { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("teamKills")] public long? TeamKills { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("timeSurvived")] public long? TimeSurvived { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("vehicleDestroys")] public long? VehicleDestroys { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("walkDistance")] public double? WalkDistance { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("weaponsAcquired")] public long? WeaponsAcquired { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("winPlace")] public long? WinPlace { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("rank")] public long? Rank { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][J("teamId")] public long? TeamId { get; set; }
}

public partial class IncludedRelationships
{
    [J("team")] public Assets? Team { get; set; }
    [J("participants")] public required Assets Participants { get; set; }
}

public partial class MatchLinks
{
    [J("self")] public required Uri Self { get; set; }
}

public partial class Meta
{
}

public enum ShardId { Steam };

public enum TypeEnum { Asset, Participant, Roster };

public enum DeathType { Alive, Byplayer, ByZone, Logout, Suicide };

internal static class Converter
{
    public static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.General)
    {
        Converters =
            {
                ShardIdConverter.Singleton,
                TypeEnumConverter.Singleton,
                DeathTypeConverter.Singleton,
                new DateOnlyConverter(),
                new TimeOnlyConverter(),
                IsoDateTimeOffsetConverter.Singleton
            },
    };
}

internal class ShardIdConverter : JsonConverter<ShardId>
{
    public override bool CanConvert(Type t) => t == typeof(ShardId);

    public override ShardId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value == "steam")
        {
            return ShardId.Steam;
        }
        throw new Exception("Cannot unmarshal type ShardId");
    }

    public override void Write(Utf8JsonWriter writer, ShardId value, JsonSerializerOptions options)
    {
        if (value == ShardId.Steam)
        {
            JsonSerializer.Serialize(writer, "steam", options);
            return;
        }
        throw new Exception("Cannot marshal type ShardId");
    }

    public static readonly ShardIdConverter Singleton = new ShardIdConverter();
}

internal class TypeEnumConverter : JsonConverter<TypeEnum>
{
    public override bool CanConvert(Type t) => t == typeof(TypeEnum);

    public override TypeEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        switch (value)
        {
            case "asset":
                return TypeEnum.Asset;
            case "participant":
                return TypeEnum.Participant;
            case "roster":
                return TypeEnum.Roster;
        }
        throw new Exception("Cannot unmarshal type TypeEnum");
    }

    public override void Write(Utf8JsonWriter writer, TypeEnum value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case TypeEnum.Asset:
                JsonSerializer.Serialize(writer, "asset", options);
                return;
            case TypeEnum.Participant:
                JsonSerializer.Serialize(writer, "participant", options);
                return;
            case TypeEnum.Roster:
                JsonSerializer.Serialize(writer, "roster", options);
                return;
        }
        throw new Exception("Cannot marshal type TypeEnum");
    }

    public static readonly TypeEnumConverter Singleton = new TypeEnumConverter();
}

internal class DeathTypeConverter : JsonConverter<DeathType>
{
    public override bool CanConvert(Type t) => t == typeof(DeathType);

    public override DeathType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        switch (value)
        {
            case "alive":
                return DeathType.Alive;
            case "byplayer":
                return DeathType.Byplayer;
            case "byzone":
                return DeathType.ByZone;
            case "logout":
                return DeathType.Logout;
            case "suicide":
                return DeathType.Suicide;
        }
        throw new Exception($"Cannot unmarshal type DeathType: {value}");
    }

    public override void Write(Utf8JsonWriter writer, DeathType value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case DeathType.Alive:
                JsonSerializer.Serialize(writer, "alive", options);
                return;
            case DeathType.Byplayer:
                JsonSerializer.Serialize(writer, "byplayer", options);
                return;
            case DeathType.ByZone:
                JsonSerializer.Serialize(writer, "byzone", options);
                return;
            case DeathType.Logout:
                JsonSerializer.Serialize(writer, "logout", options);
                return;
            case DeathType.Suicide:
                JsonSerializer.Serialize(writer, "suicide", options);
                return;
        }
        throw new Exception($"Cannot marshal type DeathType: {Enum.GetName(typeof(DeathType), value)}");
    }

    public static readonly DeathTypeConverter Singleton = new DeathTypeConverter();
}

internal class ParseStringConverter : JsonConverter<bool>
{
    public override bool CanConvert(Type t) => t == typeof(bool);

    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        bool b;
        if (Boolean.TryParse(value, out b))
        {
            return b;
        }
        throw new Exception("Cannot unmarshal type bool");
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        var boolString = value ? "true" : "false";
        JsonSerializer.Serialize(writer, boolString, options);
        return;
    }

    public static readonly ParseStringConverter Singleton = new ParseStringConverter();
}

public class DateOnlyConverter : JsonConverter<DateOnly>
{
    private readonly string serializationFormat;
    public DateOnlyConverter() : this(null) { }

    public DateOnlyConverter(string? serializationFormat)
    {
        this.serializationFormat = serializationFormat ?? "yyyy-MM-dd";
    }

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return DateOnly.Parse(value!);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString(serializationFormat));
}

public class TimeOnlyConverter : JsonConverter<TimeOnly>
{
    private readonly string serializationFormat;

    public TimeOnlyConverter() : this(null) { }

    public TimeOnlyConverter(string? serializationFormat)
    {
        this.serializationFormat = serializationFormat ?? "HH:mm:ss.fff";
    }

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return TimeOnly.Parse(value!);
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString(serializationFormat));
}

internal class IsoDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override bool CanConvert(Type t) => t == typeof(DateTimeOffset);

    private const string DefaultDateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK";

    private DateTimeStyles _dateTimeStyles = DateTimeStyles.RoundtripKind;
    private string? _dateTimeFormat;
    private CultureInfo? _culture;

    public DateTimeStyles DateTimeStyles
    {
        get => _dateTimeStyles;
        set => _dateTimeStyles = value;
    }

    public string? DateTimeFormat
    {
        get => _dateTimeFormat ?? string.Empty;
        set => _dateTimeFormat = (string.IsNullOrEmpty(value)) ? null : value;
    }

    public CultureInfo Culture
    {
        get => _culture ?? CultureInfo.CurrentCulture;
        set => _culture = value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        string text;


        if ((_dateTimeStyles & DateTimeStyles.AdjustToUniversal) == DateTimeStyles.AdjustToUniversal
                || (_dateTimeStyles & DateTimeStyles.AssumeUniversal) == DateTimeStyles.AssumeUniversal)
        {
            value = value.ToUniversalTime();
        }

        text = value.ToString(_dateTimeFormat ?? DefaultDateTimeFormat, Culture);

        writer.WriteStringValue(text);
    }

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? dateText = reader.GetString();

        if (string.IsNullOrEmpty(dateText) == false)
        {
            if (!string.IsNullOrEmpty(_dateTimeFormat))
            {
                return DateTimeOffset.ParseExact(dateText, _dateTimeFormat, Culture, _dateTimeStyles);
            }
            else
            {
                return DateTimeOffset.Parse(dateText, Culture, _dateTimeStyles);
            }
        }
        else
        {
            return default(DateTimeOffset);
        }
    }


    public static readonly IsoDateTimeOffsetConverter Singleton = new IsoDateTimeOffsetConverter();
}
