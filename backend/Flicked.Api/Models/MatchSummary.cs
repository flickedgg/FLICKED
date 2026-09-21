namespace Flicked.Api.Models;

/* One player's view of one match: what the history screen shows.
   Result, score and K/D are worked out per player, not stored. */
public record MatchSummary(
    int Id,
    string Map,
    string Result,          // "W" or "L", from this player's side
    string Score,           // this player's team first, e.g. "13-9"
    string Kd,              // "21/12"
    int Adr,
    int Delta,              // rating change
    DateTimeOffset PlayedAt);
