using Microsoft.AspNetCore.DataProtection;
using Flicked.Api.Config;
using Flicked.Api.Data;
using Flicked.Api.Services;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// Reads backend/.env, if there is one, into configuration (see Config/DotEnv.cs).
DotEnv.Load(builder.Configuration, builder.Environment.ContentRootPath);

// Database
builder.Services.AddDbContext<FlickedDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Flicked") ?? throw new InvalidOperationException("Connection string 'Flicked' not found.")));

builder.Services.AddHttpClient<SteamOpenId>();
builder.Services.AddHttpClient<SteamProfile>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentPlayer>();
builder.Services.AddSingleton<Admins>();

/* Keys for encrypting RCON passwords (Services/ServerSecrets.cs).

   Persisted to a folder on purpose. The default location is per-process on some
   hosts and thrown away with the container on others, and losing these keys means
   every stored RCON password becomes permanently unreadable. In Docker this path
   must be a mounted volume. */
var keyPath = builder.Configuration["DataProtection:KeyPath"]
    ?? builder.Configuration["FLICKED_KEY_PATH"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    .SetApplicationName("flicked");
builder.Services.AddSingleton<ServerSecrets>();

// The pool: claiming and releasing servers, plus the timer that tidies up after
// matches that never reported finishing.
builder.Services.AddScoped<ServerAuth>();
builder.Services.AddScoped<ServerPool>();
builder.Services.AddSingleton<Rcon>();
builder.Services.AddHostedService<PoolJanitor>();

// Matchmaking: the queue becomes matches, and matches become servers.
builder.Services.AddScoped<Matchmaker>();
builder.Services.AddScoped<MatchStarter>();
builder.Services.AddHostedService<MatchmakerJanitor>();


builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Where the dashboard runs. Same default as dashboard/lib/api.ts expects.
var dashboard = (builder.Configuration["Dashboard:Url"]
    ?? builder.Configuration["FLICKED_DASHBOARD_URL"]
    ?? "http://localhost:3000").TrimEnd('/');

// The launcher's webview is a different origin from the API, so it has to be allowed by name.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(
            "http://localhost:1420",    // tauri dev (vite)
            "http://tauri.localhost",   // the built app on Windows
            "tauri://localhost",        // the built app on macOS and Linux
            dashboard)                  // the admin dashboard, in a browser
        .AllowAnyHeader()
        .AllowAnyMethod()
        /* The dashboard signs in with a cookie, and a browser only sends one
           cross-origin when the server allows credentials. That also rules out a
           wildcard origin: with credentials, origins must be named. */
        .AllowCredentials());
});

/* Match sizes, so a smaller group can try the whole thing (see Matchmaker). */
Matchmaker.CompetitivePlayers = builder.Configuration.GetValue("Matchmaking:CompetitivePlayers", 10);
Matchmaker.WingmanPlayers = builder.Configuration.GetValue("Matchmaking:WingmanPlayers", 4);

var app = builder.Build();

/* Bring the database up to date on startup.

   FLICKED is meant to be self-hosted, and asking someone to install the EF
   tooling and run migrations by hand before their friends can play is a poor
   welcome. There is one instance of this API, so there is no second copy to
   race with. Set Database:AutoMigrate to false to take this over yourself. */
if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var migrations = app.Services.CreateScope();
    await migrations.ServiceProvider.GetRequiredService<FlickedDbContext>()
        .Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No HTTPS redirect while the launcher talks to http://localhost:5165: the redirect
// would send it to the self-signed dev certificate, which the webview refuses.
// Put it back (behind an IsDevelopment check) once this is deployed for real.
app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
