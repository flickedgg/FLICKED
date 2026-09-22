using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Controllers;

/*
     GET  /auth/steam/login?port=1421   the launcher opens this in the system browser
     GET  /auth/steam/callback          Steam sends the browser here when it is done
     POST /auth/exchange                the launcher itself, swapping a code for a token
     POST /auth/logout                  the launcher, ending a session
*/

[ApiController]
[Route("auth")]
public class AuthController(
    FlickedDbContext db,
    SteamOpenId steam,
    SteamProfile steamProfile,
    CurrentPlayer current,
    IConfiguration config,
    ILogger<AuthController> log) : ControllerBase
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(2);

    [HttpGet("steam/login")]
    public IActionResult Login([FromQuery] int port)
    {
        if (port is < 1024 or > 65535) return BadRequest("Bad port.");

        var callback = $"{PublicUrl()}/auth/steam/callback?port={port}";

        return Redirect(SteamOpenId.BuildLoginUrl(callback, PublicUrl()));
    }

    [HttpGet("steam/callback")]
    public async Task<IActionResult> Callback([FromQuery] int port, CancellationToken ct)
    {
        if (port is < 1024 or > 65535) return BadRequest("Bad port.");

        var steamId = await steam.VerifyAsync(Request.Query, ct);
        if (steamId is null) return Unauthorized("Steam could not confirm that sign-in.");

        var player = await FindOrCreatePlayerAsync(steamId, ct);

        var code = Secrets.New();
        db.LoginCodes.Add(new LoginCode
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            CodeHash = Secrets.Hash(code),
            ExpiresAt = DateTimeOffset.UtcNow + CodeLifetime,
        });
        await db.SaveChangesAsync(ct);

        log.LogInformation("Steam sign-in for player {PlayerId}", player.Id);

        return Redirect($"http://127.0.0.1:{port}/callback?code={code}");
    }

    public record ExchangeRequest(string Code);
    public record SessionResponse(string Token, DateTimeOffset ExpiresAt, int PlayerId, string Name, string? SteamId, string? AvatarUrl);

    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange([FromBody] ExchangeRequest body, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var hash = Secrets.Hash(body.Code);

        var code = await db.LoginCodes
            .Include(c => c.Player)
            .FirstOrDefaultAsync(c => c.CodeHash == hash, ct);

        if (code is null || code.UsedAt is not null || code.ExpiresAt <= now)
            return Unauthorized("That sign-in link is no longer valid.");

        code.UsedAt = now;

        var token = Secrets.New();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            PlayerId = code.PlayerId,
            TokenHash = Secrets.Hash(token),
            CreatedAt = now,
            ExpiresAt = now + SessionLifetime,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);

        return Ok(new SessionResponse(token, session.ExpiresAt, code.PlayerId,
                                      code.Player!.Name, code.Player.SteamId, code.Player.AvatarUrl));
    }

    public record PlayerStats(int Matches, int WinRate, double Kd, int Adr);
    public record MeResponse(int Id, string Name, string? SteamId, string? AvatarUrl,
                             int Rating, string Division, PlayerStats Stats);

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var player = await current.GetAsync(ct);
        if (player is null) return Unauthorized();

        /* Stats are counted from the match rows rather than stored on the player.
           A stored "kd" column would be another number that can drift from the
           matches it claims to summarise. Only the columns needed are fetched. */
        var rows = await db.MatchPlayers
            .Where(mp => mp.PlayerId == player.Id)
            .Select(mp => new
            {
                mp.Kills,
                mp.Deaths,
                mp.Adr,
                Won = mp.Team == 0 ? mp.Match!.ScoreA > mp.Match.ScoreB
                                   : mp.Match!.ScoreB > mp.Match.ScoreA,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var stats = new PlayerStats(
            Matches: rows.Count,
            // nobody has played yet: zeros, not a division by zero
            WinRate: rows.Count == 0 ? 0 : (int)Math.Round(rows.Count(r => r.Won) * 100.0 / rows.Count),
            // deaths can be 0 in a single match, so divide by at least one
            Kd: rows.Count == 0 ? 0 : Math.Round(rows.Sum(r => r.Kills) / (double)Math.Max(1, rows.Sum(r => r.Deaths)), 2),
            Adr: rows.Count == 0 ? 0 : (int)Math.Round(rows.Average(r => r.Adr)));

        return Ok(new MeResponse(player.Id, player.Name, player.SteamId, player.AvatarUrl,
                                 player.Rating, Divisions.For(player.Rating), stats));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return NoContent();

        var hash = Secrets.Hash(header["Bearer ".Length..].Trim());
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.TokenHash == hash, ct);
        if (session is not null && session.RevokedAt is null)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    private async Task<Player> FindOrCreatePlayerAsync(string steamId, CancellationToken ct)
    {
        var profile = await steamProfile.FetchAsync(steamId, ct);
        var existing = await db.Players.FirstOrDefaultAsync(p => p.SteamId == steamId, ct);

        if (existing is not null)
        {
            if (profile is not null)
            {
                existing.Name = profile.PersonaName;
                existing.AvatarUrl = profile.AvatarUrl;
                await db.SaveChangesAsync(ct);
            }
            return existing;
        }

        var created = new Player(
            id: 0,                                          
            name: profile?.PersonaName ?? $"Player {steamId[^4..]}",
            rating: 1000,
            wins: 0,
            losses: 0,
            steamId: steamId,
            avatarUrl: profile?.AvatarUrl);

        db.Players.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    private string PublicUrl() =>
        config["Steam:PublicUrl"]?.TrimEnd('/')
        ?? throw new InvalidOperationException("Steam:PublicUrl is not configured.");
}
