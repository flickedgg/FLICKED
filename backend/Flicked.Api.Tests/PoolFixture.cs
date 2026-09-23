using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.EntityFrameworkCore;
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

    public ServerPool NewPool(FlickedDbContext db) => new(db, NullLogger<ServerPool>.Instance);

    private static DbContextOptions<FlickedDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<FlickedDbContext>().UseNpgsql(connectionString).Options;

    /// Wipes the pool between tests, so one test's leftovers cannot pass another.
    public async Task ResetServersAsync()
    {
        await using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Servers\"");
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
