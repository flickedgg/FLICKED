using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flicked.Api.Tests;

/* These tests run against a real PostgreSQL, not an in-memory fake.

   That is deliberate: what is being tested here is FOR UPDATE SKIP LOCKED, which
   is a Postgres feature. EF's in-memory provider has no locking at all, so it
   would pass whatever we wrote, including code with the exact race this is meant
   to prevent. A test that cannot fail is worse than no test.

   The database is `flicked_test` on the same server as development, created and
   migrated on first use, so `docker compose up -d` is the only setup. */
public class PoolFixture : IAsyncLifetime
{
    private const string Server = "Host=localhost;Port=5432;Username=flicked;Password=flicked_dev";
    public const string ConnectionString = $"{Server};Database=flicked_test";

    public async Task InitializeAsync()
    {
        // CREATE DATABASE cannot run inside the database being created, so this
        // connects to the default one first.
        await using (var admin = new FlickedDbContext(Options($"{Server};Database=postgres")))
        {
            await admin.Database.ExecuteSqlRawAsync(
                "SELECT 'CREATE DATABASE flicked_test' WHERE NOT EXISTS "
              + "(SELECT FROM pg_database WHERE datname = 'flicked_test')");

            // the statement above only builds the text; run what it produced
            var exists = await admin.Database
                .SqlQueryRaw<bool>("SELECT EXISTS (SELECT FROM pg_database WHERE datname = 'flicked_test') AS \"Value\"")
                .SingleAsync();
            if (!exists) await admin.Database.ExecuteSqlRawAsync("CREATE DATABASE flicked_test");
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /* A fresh context per call, because concurrency is the point: two claims
       racing must be two connections. Sharing one DbContext would serialise them
       and the test would pass without proving anything. */
    public FlickedDbContext NewContext() => new(Options(ConnectionString));

    /* The pool now talks RCON when checking liveness, so the tests build it with
       real collaborators; nothing they exercise here makes an RCON call. */
    public ServerPool NewPool(FlickedDbContext db) => new(
        db,
        new Rcon(NullLogger<Rcon>.Instance),
        new ServerSecrets(DataProtectionProvider.Create("flicked-tests")),
        NullLogger<ServerPool>.Instance);

    /// Closing a match and rating everyone in it (see RATING.md).
    public MatchResults NewResults(FlickedDbContext db) => new(db, NullLogger<MatchResults>.Instance);

    private static DbContextOptions<FlickedDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<FlickedDbContext>().UseNpgsql(connectionString).Options;

    /// Wipes the pool between tests, so one test's leftovers cannot pass another.
    public async Task ResetServersAsync()
    {
        await using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Servers\"");
    }

    /* Matchmaking helpers. Players made here are throwaway rows with no Steam
       account, which is all the matchmaker cares about. */

    public async Task ResetMatchmakingAsync()
    {
        await using var db = NewContext();
        // order matters: match rows reference players, queue rows reference parties
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Queue\"");
        /* Every party, not only the test players', because a party of one is
           made the first time somebody queues and the seeded players are used by
           other tests. Nothing outside a test ever owns a row here. */
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"PartyInvites\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"PartyMembers\"");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Parties\"");
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"MatchPlayers\" WHERE \"PlayerId\" IN (SELECT \"Id\" FROM \"Players\" WHERE \"Name\" LIKE 'test-%')");
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"Matches\" WHERE \"Id\" NOT IN (48213, 48190, 48122, 48077, 47951, 47903)");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Players\" WHERE \"Name\" LIKE 'test-%'");
    }

    public async Task<List<Player>> AddPlayersAsync(int count, int rating)
    {
        await using var db = NewContext();
        var players = Enumerable.Range(0, count)
            .Select(_ => new Player(0, $"test-{Guid.NewGuid():N}"[..12], rating, 0, 0))
            .ToList();

        db.Players.AddRange(players);
        await db.SaveChangesAsync();
        return players;
    }

    /* One player in a match that is about to be rated: where they started, which
       side they were on, and the figures round_end would have left on their row.

       Wins and Losses are how the rating system knows whether somebody is still
       provisional, so a test that wants an established player says so here. */
    public record Contestant(int Rating, int Team, int Kills = 0, int Deaths = 0, int Adr = 0,
                             int Wins = 20, int Losses = 20);

    /* A Live match with that lineup on it, ready to be finished.

       Players are made fresh for each one, so a test can move ratings around
       without any other test noticing. */
    public async Task<Match> AddLiveMatchAsync(params Contestant[] lineup)
    {
        await using var db = NewContext();

        var match = new Match
        {
            Map = "de_mirage",
            Status = MatchStatus.Live,
            PlayedAt = DateTimeOffset.UtcNow,
        };

        foreach (var entry in lineup)
        {
            var player = new Player(0, $"test-{Guid.NewGuid():N}"[..12], entry.Rating, entry.Wins, entry.Losses);
            db.Players.Add(player);
            await db.SaveChangesAsync();

            match.Players.Add(new MatchPlayer
            {
                PlayerId = player.Id,
                Team = entry.Team,
                Kills = entry.Kills,
                Deaths = entry.Deaths,
                Adr = entry.Adr,
            });
        }

        db.Matches.Add(match);
        await db.SaveChangesAsync();
        return match;
    }

    /// Everyone in a match, their rating and what the match paid them.
    public async Task<List<(int Rating, int Wins, int Losses, int Delta)>> RatedAsync(int matchId)
    {
        await using var db = NewContext();
        return await db.MatchPlayers
            .Where(mp => mp.MatchId == matchId)
            .OrderBy(mp => mp.PlayerId)
            .Select(mp => new ValueTuple<int, int, int, int>(
                mp.Player!.Rating, mp.Player.Wins, mp.Player.Losses, mp.RatingDelta))
            .ToListAsync();
    }

    /* Everyone queues as a party, so these players each get a party of one.
       Written the long way rather than through the API, because what is being
       tested is the matchmaker, and a party of one is what it will see. */
    public async Task QueueAsync(IEnumerable<Player> players, DateTimeOffset? joinedAt = null)
    {
        await using var db = NewContext();
        foreach (var player in players) db.Queue.Add(Waiting([player], joinedAt));
        await db.SaveChangesAsync();
    }

    /// One party of several, waiting together.
    public async Task<Party> QueuePartyAsync(IReadOnlyList<Player> members, DateTimeOffset? joinedAt = null)
    {
        await using var db = NewContext();
        var entry = Waiting(members, joinedAt);
        db.Queue.Add(entry);
        await db.SaveChangesAsync();
        return entry.Party!;
    }

    /// Friends already, without going through the request and the accept.
    public async Task BefriendAsync(Player a, Player b)
    {
        await using var db = NewContext();
        db.Friendships.Add(new Friendship
        {
            RequesterId = a.Id,
            AddresseeId = b.Id,
            Status = FriendshipStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
            RespondedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// A party that is not queueing: for the rules about joining and leaving one.
    public async Task<Party> AddPartyAsync(IReadOnlyList<Player> members)
    {
        await using var db = NewContext();
        var party = NewParty(members);
        db.Parties.Add(party);
        await db.SaveChangesAsync();
        return party;
    }

    private static QueueEntry Waiting(IReadOnlyList<Player> members, DateTimeOffset? joinedAt) => new()
    {
        Party = NewParty(members),
        Mode = ServerType.Competitive,
        JoinedAt = joinedAt ?? DateTimeOffset.UtcNow,
    };

    /// The first member leads, and they join in the order they are given.
    private static Party NewParty(IReadOnlyList<Player> members)
    {
        var now = DateTimeOffset.UtcNow;
        return new Party
        {
            LeaderId = members[0].Id,
            CreatedAt = now,
            Members = members
                .Select((player, i) => new PartyMember { PlayerId = player.Id, JoinedAt = now.AddSeconds(i) })
                .ToList(),
        };
    }

    /// Marks everyone in the match as having accepted, optionally leaving some out.
    public async Task AcceptAllAsync(int matchId, int except = 0)
    {
        await using var db = NewContext();
        var rows = await db.MatchPlayers.Where(mp => mp.MatchId == matchId).ToListAsync();
        foreach (var row in rows.Skip(except)) row.AcceptedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    /// Marks exactly these players as having accepted, and nobody else.
    public async Task AcceptAsync(int matchId, IEnumerable<Player> players)
    {
        var ids = players.Select(p => p.Id).ToList();

        await using var db = NewContext();
        var rows = await db.MatchPlayers
            .Where(mp => mp.MatchId == matchId && ids.Contains(mp.PlayerId))
            .ToListAsync();

        foreach (var row in rows) row.AcceptedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    /// Pushes a match's clock back, so a window that lasts 20 seconds can be
    /// tested without waiting 20 seconds.
    public async Task AgeMatchAsync(int matchId, TimeSpan by)
    {
        await using var db = NewContext();
        var match = await db.Matches.SingleAsync(m => m.Id == matchId);
        match.PlayedAt -= by;
        await db.SaveChangesAsync();
    }

    public async Task<GameServer> AddServerAsync(string name, ServerStatus status = ServerStatus.Idle,
                                                 DateTimeOffset? lastSeen = null, bool enabled = true)
    {
        await using var db = NewContext();
        var server = new GameServer
        {
            Name = name,
            Host = $"10.0.0.{Random.Shared.Next(1, 250)}",
            Port = Random.Shared.Next(27000, 28000),
            Type = ServerType.Competitive,
            Status = status,
            IsEnabled = enabled,
            LastSeenAt = lastSeen ?? DateTimeOffset.UtcNow,
            RconPasswordEncrypted = "not-used-here",
            TokenHash = Secrets.Hash(Guid.NewGuid().ToString()),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Servers.Add(server);
        await db.SaveChangesAsync();
        return server;
    }
}

[CollectionDefinition("pool")]
public class PoolCollection : ICollectionFixture<PoolFixture>;
