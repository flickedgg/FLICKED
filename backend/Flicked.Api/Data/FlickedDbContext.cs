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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewsPost>().HasData(
                 new("n6", "patch", new DateOnly(2026, 9, 18), "0.2",
              "Alpha 0.2: parties and map vote",
              "Queue with up to four friends, and pick the map together after everyone accepts.",
              [
                  "Parties are here. Invite friends from the list on the Play screen, or share your party code so they can join directly. The leader queues for everyone.",
                  "Map veto is replaced by a map vote. After all ten players accept, everyone has 15 seconds to vote. The map with the most votes is played; a tie is settled at random.",
                  "Also in this release: the launcher starts faster, fonts ship with the app, and the window no longer flashes white on open.",
              ]),
          new("n5", "update", new DateOnly(2026, 9, 15), null,
              "Self-host FLICKED on a single machine",
              "A new guide walks through running the backend, database and one CS2 server on one box.",
              [
                  "You do not need a cluster to run FLICKED. The new guide in the repository shows how to run the backend, PostgreSQL, Redis and a CS2 dedicated server on a single machine.",
                  "It covers ports, the match config, and how to point the launcher at your own server.",
              ]),
          new("n4", "event", new DateOnly(2026, 9, 12), null,
              "Community Cup #1: sign-ups open",
              "Five-stack tournament, single elimination, played on community servers.",
              [
                  "Sign-ups for the first FLICKED Community Cup are open. Teams of five, single elimination, best of one until the final.",
                  "Matches run on community-hosted servers. Brackets are published the day before the first round.",
              ]),
          new("n3", "patch", new DateOnly(2026, 9, 8), "0.1.3",
              "Alpha 0.1.3: queue fixes",
              "Fixes a case where a declined match kept you in queue, plus smaller stability fixes.",
              [
                  "Declining a match now always returns you to the Play screen. Before, a declined match could leave you searching with no way to cancel.",
                  "Reconnecting to a live match is faster, and the server log keeps the full match history.",
              ]),
          new("n2", "update", new DateOnly(2026, 9, 3), null,
              "Every match now records a demo",
              "Demos are saved on the server and can be downloaded from the match page.",
              [
                  "Every match played on FLICKED now records a demo automatically. Demos are stored on the server that hosted the match.",
                  "Server owners can set how long demos are kept.",
              ]),
          new("n1", "event", new DateOnly(2026, 8, 29), null,
              "Weekly 5v5 night, Fridays at 20:00 CET",
              "A standing night to find full stacks. Queue times drop, games get better.",
              [
                  "Every Friday from 20:00 CET we play community 5v5s. More people in queue at the same time means shorter waits and closer matches.",
              ])
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

        modelBuilder.Entity<Session>().HasIndex(s => s.TokenHash).IsUnique();
        modelBuilder.Entity<LoginCode>().HasIndex(c => c.CodeHash).IsUnique();
        modelBuilder.Entity<Player>()
            .HasIndex(p => p.SteamId)
            .IsUnique()
            .HasFilter("\"SteamId\" IS NOT NULL");

        modelBuilder.Entity<Match>().HasData(
            new Match { Id = 48213, Map = "de_mirage",  ScoreA = 13, ScoreB = 9,  PlayedAt = new DateTimeOffset(2026, 9, 21, 18, 40, 0, TimeSpan.Zero) },
            new Match { Id = 48190, Map = "de_inferno", ScoreA = 10, ScoreB = 13, PlayedAt = new DateTimeOffset(2026, 9, 21, 17,  5, 0, TimeSpan.Zero) },
            new Match { Id = 48122, Map = "de_nuke",    ScoreA = 13, ScoreB = 4,  PlayedAt = new DateTimeOffset(2026, 9, 20, 21, 30, 0, TimeSpan.Zero) },
            new Match { Id = 48077, Map = "de_ancient", ScoreA = 13, ScoreB = 11, PlayedAt = new DateTimeOffset(2026, 9, 20, 20,  0, 0, TimeSpan.Zero) },
            new Match { Id = 47951, Map = "de_anubis",  ScoreA = 7,  ScoreB = 13, PlayedAt = new DateTimeOffset(2026, 9, 19, 22, 10, 0, TimeSpan.Zero) },
            new Match { Id = 47903, Map = "de_dust2",   ScoreA = 13, ScoreB = 10, PlayedAt = new DateTimeOffset(2026, 9, 18, 21, 15, 0, TimeSpan.Zero) }
        );

        modelBuilder.Entity<MatchPlayer>().HasData(
            new MatchPlayer { MatchId = 48213, PlayerId = 1, Team = 0, Kills = 21, Deaths = 12, Adr = 97, RatingDelta =  24 },
            new MatchPlayer { MatchId = 48190, PlayerId = 1, Team = 0, Kills = 15, Deaths = 17, Adr = 72, RatingDelta = -19 },
            new MatchPlayer { MatchId = 48122, PlayerId = 1, Team = 0, Kills = 18, Deaths =  8, Adr = 91, RatingDelta =  21 },
            new MatchPlayer { MatchId = 48077, PlayerId = 1, Team = 0, Kills = 17, Deaths = 15, Adr = 80, RatingDelta =  18 },
            new MatchPlayer { MatchId = 47951, PlayerId = 1, Team = 0, Kills = 11, Deaths = 16, Adr = 61, RatingDelta = -22 },
            new MatchPlayer { MatchId = 47903, PlayerId = 1, Team = 0, Kills = 19, Deaths = 14, Adr = 88, RatingDelta =  20 }
        );
    }
}