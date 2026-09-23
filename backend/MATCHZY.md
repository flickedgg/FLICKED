# Talking to MatchZy

What FLICKED needs from the CS2 side, checked against MatchZy's source and docs
rather than from memory. Every claim here has a link; if something disagrees with
the plugin in practice, the source wins and this file is wrong.

Read on 2026-09-23 against the `dev` branch of
[shobhit-pathak/MatchZy](https://github.com/shobhit-pathak/MatchZy).

---

## How a match starts

MatchZy loads a match from JSON, either from a local file or over HTTP:

```
matchzy_loadmatch <filepath>                      # relative to csgo/
matchzy_loadmatch_url <url> [header name] [header value]
```

`matchzy_loadmatch_url` does a **GET** and accepts one optional custom header, so
FLICKED can require a token on the config endpoint rather than leaving it open.

**So the flow for us is:** claim a server from the pool → over RCON, tell it
`matchzy_loadmatch_url https://api.example/api/matches/{id}/config <header> <token>`.

### The config we have to serve

Fields, from [Match Setup](https://shobhit-pathak.github.io/MatchZy/match_setup/):

```json
{
  "matchid": 27,
  "team1": {
    "name": "TeamName",
    "players": { "76561198xxxxxxxxxx": "PlayerNickname" }
  },
  "team2": { "name": "...", "players": { } },
  "num_maps": 1,
  "maplist": ["de_mirage"],
  "map_sides": ["knife"],
  "players_per_team": 5,
  "cvars": { "hostname": "FLICKED #27" }
}
```

Notes that matter for us:

- **Players are keyed by Steam64 ID**, which is the only identifier MatchZy
  accepts. FLICKED already stores `Player.SteamId`, so nothing else is needed.
- `matchid` is optional and auto-generated if left out. We always send ours, so
  events can be matched to a FLICKED match.
- One map per match for now: `num_maps: 1`, `maplist` holding the voted map.
- `map_sides` decides starting sides. `"knife"` gives a knife round.
- Coaches cannot be set in the config; they join as players and use `.coach`.

---

## How a match reports back

Set on the server:

```
matchzy_remote_log_url <url>
matchzy_remote_log_header_key <name>
matchzy_remote_log_header_value <token>
```

([RemoteLogConfig.cs](https://github.com/shobhit-pathak/MatchZy/blob/dev/RemoteLogConfig.cs);
the `get5_` prefixes are accepted as aliases.)

Events are **POSTed as `application/json`**, with the custom header attached when
configured ([PublishEvents.cs](https://github.com/shobhit-pathak/MatchZy/blob/dev/PublishEvents.cs)).

**There is no retry and no deduplication.** If FLICKED is down or answers slowly,
that event is gone. So:

- the endpoint must be quick and must not depend on anything flaky,
- it must tolerate the same event arriving twice (write results idempotently),
- and **the match lease stays the safety net**: a missed `series_end` must not
  strand a server, which is what `ServerPool.SweepAsync` already covers.

### The events

Every payload has an `event` field, and every match-scoped one has `matchid`
([Events.cs](https://github.com/shobhit-pathak/MatchZy/blob/dev/Events.cs)):

| `event` | When | Payload beyond `event` + `matchid` |
|---|---|---|
| `series_start` | match config loaded | `team1`, `team2`, `num_maps` |
| `going_live` | knife done, match starts | `map_number` |
| `round_end` | every round | `map_number`, `round_number`, `round_time`, `reason`, `winner`, `team1`, `team2` |
| `map_result` | map finished | `map_number`, `winner`, `team1`, `team2` |
| `series_end` | match over | `winner`, `team1_series_score`, `team2_series_score`, `time_until_restore` |
| `map_picked` / `map_vetoed` / `side_picked` | veto, if used | `team`, `map_name`, `map_number`, `side` |
| `player_disconnect` | a player leaves | `player` |
| `demo_upload_ended` | demo uploaded | `map_number`, `filename`, `success` |

`winner` is `{ "side": "...", "team": "..." }`
([MatchData.cs](https://github.com/shobhit-pathak/MatchZy/blob/dev/MatchData.cs)).

`team1` / `team2` in round and map events carry the score and every player's
stats: `series_score`, `score`, `score_ct`, `score_t`, and `players` where each
player has `steamid`, `name` and a `stats` object containing `kills`, `deaths`,
`assists`, `damage`, `headshot_kills`, `rounds_played`, `bomb_plants`,
`bomb_defuses`, `utility_damage`, `enemies_flashed`, multi-kills (`1k`…`5k`),
clutches (`1v1`…`1v4`) and more.

**Everything FLICKED shows already maps onto this**: K/D from `kills` and
`deaths`, ADR from `damage ÷ rounds_played`, the score from `score`.

---

## What FLICKED still has to build

1. **`GET /api/matches/{id}/config`** — serves the JSON above, authenticated by
   the header MatchZy is told to send. Public would leak the match and its
   players' Steam IDs.
2. **`POST /api/matches/events`** — receives the events, authenticated by the
   server token we already issue. Should handle repeats without duplicating.
3. **RCON** — one command per match to point a claimed server at its config.
   Needs an RCON client; nothing in FLICKED speaks that protocol yet.
4. **`going_live` → `MarkHostingAsync`**, and **`series_end` → save the result,
   then `ReleaseAsync`.** That closes the loop the pool is waiting for.

## Decisions still open

- **Which events to store.** `round_end` every round is a lot of rows for little
  benefit early on; `map_result` and `series_end` carry the final figures.
- **Whether to keep MatchZy long term** or write our own CounterStrikeSharp
  plugin. The roadmap says MatchZy first, and the match handling it gives us
  (knife rounds, pauses, reconnects, demos, overtime) is the part least worth
  rebuilding.
