using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flicked.Api.Models;

/* The shapes MatchZy posts to our remote log URL.

   Transcribed from the plugin's own Events.cs and MatchData.cs, not invented;
   see docs/MATCHZY.md for the links. Only the parts FLICKED uses are here:
   unknown fields are ignored by System.Text.Json, so this does not have to keep
   pace with everything the plugin sends.

   Everything is nullable because this arrives over the network from software we
   do not control. A missing field should be a 400, never an exception. */

/* MatchZy sends matchid as a number in its events, and accepts it as a string in
   a match config. Rather than guess which arrives, read whichever is there. */
public class LenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var number)
                ? number.ToString()
                : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => null,
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

public record MatchZyEvent
{
    [JsonPropertyName("event")] public string? Event { get; init; }

    [JsonPropertyName("matchid")]
    [JsonConverter(typeof(LenientStringConverter))]
    public string? MatchId { get; init; }

    /* map_result only: the score and every player's stats.
       series_end has neither, which is why it is handled separately. */
    [JsonPropertyName("team1")] public MatchZyTeam? Team1 { get; init; }
    [JsonPropertyName("team2")] public MatchZyTeam? Team2 { get; init; }
    [JsonPropertyName("winner")] public MatchZyWinner? Winner { get; init; }

    // series_end only
    [JsonPropertyName("team1_series_score")] public int? Team1SeriesScore { get; init; }
    [JsonPropertyName("team2_series_score")] public int? Team2SeriesScore { get; init; }
}

public record MatchZyWinner
{
    [JsonPropertyName("side")] public string? Side { get; init; }
    [JsonPropertyName("team")] public string? Team { get; init; }
}

public record MatchZyTeam
{
    [JsonPropertyName("score")] public int Score { get; init; }
    [JsonPropertyName("players")] public List<MatchZyPlayer>? Players { get; init; }
}

public record MatchZyPlayer
{
    // Steam64, the only identifier MatchZy works in, and the one we key players on.
    // Sent as a string today, but read leniently: a 17-digit number would overflow
    // an int and the same mismatch would be a much quieter bug.
    [JsonPropertyName("steamid")]
    [JsonConverter(typeof(LenientStringConverter))]
    public string? SteamId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("stats")] public MatchZyPlayerStats? Stats { get; init; }
}

public record MatchZyPlayerStats
{
    [JsonPropertyName("kills")] public int Kills { get; init; }
    [JsonPropertyName("deaths")] public int Deaths { get; init; }
    [JsonPropertyName("assists")] public int Assists { get; init; }

    /* MatchZy reports total damage and rounds played; FLICKED shows damage per
       round, so ADR is worked out here rather than stored twice. */
    [JsonPropertyName("damage")] public int Damage { get; init; }
    [JsonPropertyName("rounds_played")] public int RoundsPlayed { get; init; }

    public int Adr => RoundsPlayed == 0 ? 0 : (int)Math.Round((double)Damage / RoundsPlayed);
}
