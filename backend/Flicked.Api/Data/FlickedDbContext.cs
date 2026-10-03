using Microsoft.EntityFrameworkCore;
using Flicked.Api.Models;
namespace Flicked.Api.Data;

public class FlickedDbContext : DbContext
{
    public FlickedDbContext(DbContextOptions<FlickedDbContext> options) : base(options) { }
    public DbSet<NewsPost> News => Set<NewsPost>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<LoginCode> LoginCodes => Set<LoginCode>();
    public DbSet<Friendship> Friendships => Set<Friendship>();

    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyMember> PartyMembers => Set<PartyMember>();
    public DbSet<PartyInvite> PartyInvites => Set<PartyInvite>();

    public DbSet<GameServer> Servers => Set<GameServer>();
    public DbSet<QueueEntry> Queue => Set<QueueEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        /* The demo posts every fresh database starts with.

           Written out field by field rather than positionally now that NewsPost
           is an editable row: HasData compares what is here with what is in the
           table, so these values have to stay constant, which is why CreatedAt
           is derived from each post's own date instead of "now". */
        modelBuilder.Entity<NewsPost>().HasData(
            new NewsPost
            {
                Id = "n6", Category = "patch", Date = new DateOnly(2026, 9, 18), Badge = "0.2",
                CreatedAt = new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero),
                Title = "Alpha 0.2: parties and map vote",
                Excerpt = "Queue with up to four friends, and pick the map together after everyone accepts.",
                Body =
                [
                    "Parties are here. Invite friends from the list on the Play screen, or share your party code so they can join directly. The leader queues for everyone.",
                    "Map veto is replaced by a map vote. After all ten players accept, everyone has 15 seconds to vote. The map with the most votes is played; a tie is settled at random.",
                    "Also in this release: the launcher starts faster, fonts ship with the app, and the window no longer flashes white on open.",
                ],
            },
            new NewsPost
            {
                Id = "n5", Category = "update", Date = new DateOnly(2026, 9, 15),
                CreatedAt = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero),
                Title = "Self-host FLICKED on a single machine",
                Excerpt = "A new guide walks through running the backend, database and one CS2 server on one box.",
                Body =
                [
                    "You do not need a cluster to run FLICKED. The new guide in the repository shows how to run the backend, PostgreSQL, Redis and a CS2 dedicated server on a single machine.",
                    "It covers ports, the match config, and how to point the launcher at your own server.",
                ],
            },
            new NewsPost
            {
                Id = "n4", Category = "event", Date = new DateOnly(2026, 9, 12),
                CreatedAt = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero),
                Title = "Community Cup #1: sign-ups open",
                Excerpt = "Five-stack tournament, single elimination, played on community servers.",
                Body =
                [
                    "Sign-ups for the first FLICKED Community Cup are open. Teams of five, single elimination, best of one until the final.",
                    "Matches run on community-hosted servers. Brackets are published the day before the first round.",
                ],
            },
            new NewsPost
            {
                Id = "n3", Category = "patch", Date = new DateOnly(2026, 9, 8), Badge = "0.1.3",
                CreatedAt = new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero),
                Title = "Alpha 0.1.3: queue fixes",
                Excerpt = "Fixes a case where a declined match kept you in queue, plus smaller stability fixes.",
                Body =
                [
                    "Declining a match now always returns you to the Play screen. Before, a declined match could leave you searching with no way to cancel.",
                    "Reconnecting to a live match is faster, and the server log keeps the full match history.",
                ],
            },
            new NewsPost
            {
                Id = "n2", Category = "update", Date = new DateOnly(2026, 9, 3),
                CreatedAt = new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero),
                Title = "Every match now records a demo",
                Excerpt = "Demos are saved on the server and can be downloaded from the match page.",
                Body =
                [
                    "Every match played on FLICKED now records a demo automatically. Demos are stored on the server that hosted the match.",
                    "Server owners can set how long demos are kept.",
                ],
            },
            new NewsPost
            {
                Id = "n1", Category = "event", Date = new DateOnly(2026, 8, 29),
                CreatedAt = new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero),
                Title = "Weekly 5v5 night, Fridays at 20:00 CET",
                Excerpt = "A standing night to find full stacks. Queue times drop, games get better.",
                Body =
                [
                    "Every Friday from 20:00 CET we play community 5v5s. More people in queue at the same time means shorter waits and closer matches.",
                ],
            }
        );
        modelBuilder.Entity<Player>().HasData(
    new Player(1, "kovac", 2614, 311, 146),
    new Player(2, "Halden", 2571, 287, 148),
    new Player(3, "Nyx", 2498, 264, 148),
    new Player(4, "sprayz", 2402, 240, 147),
    new Player(5, "quietus", 2366, 198, 127),
    new Player(6, "reload", 2291, 215, 149),
    new Player(7, "Brine", 2240, 176, 122),
    new Player(8, "m0th", 2187, 169, 122),
    new Player(9, "lowground", 2105, 151, 119),
    new Player(10, "patchnote", 2050, 143, 117)
);
        modelBuilder.Entity<MatchPlayer>().HasKey(mp => new { mp.MatchId, mp.PlayerId });

        modelBuilder.Entity<Friendship>(friendship =>
        {
            friendship.HasOne(f => f.Requester)
                .WithMany()
                .HasForeignKey(f => f.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);

            friendship.HasOne(f => f.Addressee)
                .WithMany()
                .HasForeignKey(f => f.AddresseeId)
                .OnDelete(DeleteBehavior.Cascade);

            friendship.HasIndex(f => new { f.RequesterId, f.AddresseeId }).IsUnique();
            friendship.HasIndex(f => f.AddresseeId);   // "requests sent to me"
            friendship.Property(f => f.Status).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<Party>(party =>
        {
            /* A party belongs to its leader: if their account goes, so does the
               party, and its members are freed to make or join another. */
            party.HasOne(p => p.Leader)
                .WithMany()
                .HasForeignKey(p => p.LeaderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PartyMember>(member =>
        {
            /* The index the whole design leans on: one party per player, decided
               by the database rather than by a check the API makes just before
               inserting. See PartyMember for what it prevents. */
            member.HasIndex(m => m.PlayerId).IsUnique();

            member.HasOne(m => m.Party)
                .WithMany(p => p.Members)
                .HasForeignKey(m => m.PartyId)
                .OnDelete(DeleteBehavior.Cascade);

            member.HasOne(m => m.Player)
                .WithMany()
                .HasForeignKey(m => m.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PartyInvite>(invite =>
        {
            // inviting somebody twice refreshes one row rather than making two
            invite.HasIndex(i => new { i.PartyId, i.ToPlayerId }).IsUnique();
            invite.HasIndex(i => i.ToPlayerId);   // "invites waiting for me", asked on every poll
            invite.HasIndex(i => i.ExpiresAt);    // how the janitor sweeps them

            invite.HasOne(i => i.Party)
                .WithMany()
                .HasForeignKey(i => i.PartyId)
                .OnDelete(DeleteBehavior.Cascade);

            invite.HasOne(i => i.ToPlayer)
                .WithMany()
                .HasForeignKey(i => i.ToPlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            invite.HasOne(i => i.FromPlayer)
                .WithMany()
                .HasForeignKey(i => i.FromPlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QueueEntry>(entry =>
        {
            /* One queue entry per party: joining twice, or queueing for both
               modes at once, would let a party be matched into two matches. One
               party per player is enforced a table away, so this is also still
               "one queue at a time" for a person. */
            entry.HasIndex(q => q.PartyId).IsUnique();
            entry.HasIndex(q => new { q.Mode, q.JoinedAt });   // how the matchmaker reads it
            entry.Property(q => q.Mode).HasConversion<string>().HasMaxLength(16);

            /* A disbanded party takes its queue entry with it, which is what
               makes "membership changes dequeue the party" hold even when the
               change is the party ceasing to exist. */
            entry.HasOne(q => q.Party)
                .WithMany()
                .HasForeignKey(q => q.PartyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Session>().HasIndex(s => s.TokenHash).IsUnique();
        modelBuilder.Entity<LoginCode>().HasIndex(c => c.CodeHash).IsUnique();
        modelBuilder.Entity<Player>()
            .HasIndex(p => p.SteamId)
            .IsUnique()
            .HasFilter("\"SteamId\" IS NOT NULL");

        // readable in the database, like the other enums here
        modelBuilder.Entity<Match>().Property(m => m.Status).HasConversion<string>().HasMaxLength(16);

        modelBuilder.Entity<Match>().HasData(
            new Match { Id = 48213, Map = "de_mirage",  ScoreA = 13, ScoreB = 9,  PlayedAt = new DateTimeOffset(2026, 9, 21, 18, 40, 0, TimeSpan.Zero), Status = MatchStatus.Finished },
            new Match { Id = 48190, Map = "de_inferno", ScoreA = 10, ScoreB = 13, PlayedAt = new DateTimeOffset(2026, 9, 21, 17,  5, 0, TimeSpan.Zero), Status = MatchStatus.Finished },
            new Match { Id = 48122, Map = "de_nuke",    ScoreA = 13, ScoreB = 4,  PlayedAt = new DateTimeOffset(2026, 9, 20, 21, 30, 0, TimeSpan.Zero), Status = MatchStatus.Finished },
            new Match { Id = 48077, Map = "de_ancient", ScoreA = 13, ScoreB = 11, PlayedAt = new DateTimeOffset(2026, 9, 20, 20,  0, 0, TimeSpan.Zero), Status = MatchStatus.Finished },
            new Match { Id = 47951, Map = "de_anubis",  ScoreA = 7,  ScoreB = 13, PlayedAt = new DateTimeOffset(2026, 9, 19, 22, 10, 0, TimeSpan.Zero), Status = MatchStatus.Finished },
            new Match { Id = 47903, Map = "de_dust2",   ScoreA = 13, ScoreB = 10, PlayedAt = new DateTimeOffset(2026, 9, 18, 21, 15, 0, TimeSpan.Zero), Status = MatchStatus.Finished }
        );

        modelBuilder.Entity<MatchPlayer>().HasData(
            new MatchPlayer { MatchId = 48213, PlayerId = 1, Team = 0, Kills = 21, Deaths = 12, Adr = 97, RatingDelta =  24 },
            new MatchPlayer { MatchId = 48190, PlayerId = 1, Team = 0, Kills = 15, Deaths = 17, Adr = 72, RatingDelta = -19 },
            new MatchPlayer { MatchId = 48122, PlayerId = 1, Team = 0, Kills = 18, Deaths =  8, Adr = 91, RatingDelta =  21 },
            new MatchPlayer { MatchId = 48077, PlayerId = 1, Team = 0, Kills = 17, Deaths = 15, Adr = 80, RatingDelta =  18 },
            new MatchPlayer { MatchId = 47951, PlayerId = 1, Team = 0, Kills = 11, Deaths = 16, Adr = 61, RatingDelta = -22 },
            new MatchPlayer { MatchId = 47903, PlayerId = 1, Team = 0, Kills = 19, Deaths = 14, Adr = 88, RatingDelta =  20 }
        );

        /* No seeded servers: an empty pool is the truth on a fresh install, and a
           fake row would be claimable by a real match. Admins add their own.
           Passwords would not belong in a migration anyway, since migrations are
           committed and run on every self-hoster's database. */
        modelBuilder.Entity<GameServer>(server =>
        {
            // one row per machine and port, so a server cannot be registered twice
            server.HasIndex(s => new { s.Host, s.Port }).IsUnique();
            // every authenticated call from a server looks itself up by this
            server.HasIndex(s => s.TokenHash);
            server.HasIndex(s => s.Status);   // "find me a free one"

            server.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            server.Property(s => s.Type).HasConversion<string>().HasMaxLength(16);

            server.HasOne(s => s.CurrentMatch)
                .WithMany()
                .HasForeignKey(s => s.CurrentMatchId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}