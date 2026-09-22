using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Controllers;

/* Managing the server pool, from the dashboard.

     GET    /api/admin/servers        every server and what it is doing
     POST   /api/admin/servers        register one; the server token is shown once
     PATCH  /api/admin/servers/{id}   edit, unless it is in a match
     DELETE /api/admin/servers/{id}   remove, unless it is in a match
     POST   /api/admin/servers/{id}/token   issue a new server token

   Admins only. Every route checks, rather than trusting the dashboard to hide
   buttons: the dashboard is a convenience, the API is the rule. */
[ApiController]
[Route("api/admin/servers")]
public class AdminServersController(
    FlickedDbContext db,
    CurrentPlayer current,
    ServerSecrets secrets,
    ILogger<AdminServersController> log) : ControllerBase
{
    /* Everything the dashboard shows. No RCON password and no game password: the
       API never hands a secret back, so a bug in the dashboard cannot leak one.
       HasRcon says whether one is stored, which is all the interface needs. */
    public record ServerView(
        int Id, string Name, string Region, string Type, string Host, int Port,
        string Status, int? CurrentMatchId, DateTimeOffset? LastSeenAt,
        bool IsEnabled, bool HasRcon, bool HasGamePassword, DateTimeOffset CreatedAt);

    public record ServerForm(
        string? Name, string? Region, string? Type, string? Host, int? Port,
        string? GamePassword, string? RconPassword, bool? IsEnabled);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (await Denied(ct) is { } denial) return denial;

        var servers = await db.Servers
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(servers.Select(View));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] ServerForm form, CancellationToken ct)
    {
        if (await Denied(ct) is { } denial) return denial;

        if (string.IsNullOrWhiteSpace(form.Name)) return BadRequest("A name is required.");
        if (string.IsNullOrWhiteSpace(form.Host)) return BadRequest("A host is required.");
        if (string.IsNullOrWhiteSpace(form.RconPassword)) return BadRequest("An RCON password is required.");
        if (!TryType(form.Type, out var type)) return BadRequest("Type must be Competitive or Wingman.");

        var port = form.Port ?? 27015;
        if (port is < 1 or > 65535) return BadRequest("Port must be between 1 and 65535.");

        var host = form.Host.Trim();
        if (await db.Servers.AnyAsync(s => s.Host == host && s.Port == port, ct))
            return Conflict("That host and port is already registered.");

        /* The server's own credential, for the calls it makes to us. Shown once,
           here, and never again: only its hash is kept, exactly like a session
           token. If it is lost, issue a new one. */
        var token = Secrets.New();

        var server = new GameServer
        {
            Name = form.Name.Trim(),
            Region = form.Region?.Trim() ?? "",
            Type = type,
            Host = host,
            Port = port,
            GamePassword = string.IsNullOrWhiteSpace(form.GamePassword) ? null : form.GamePassword,
            RconPasswordEncrypted = secrets.Encrypt(form.RconPassword),
            TokenHash = Secrets.Hash(token),
            Status = ServerStatus.Offline,   // until it says hello
            IsEnabled = form.IsEnabled ?? true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Servers.Add(server);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Server {Name} ({Host}:{Port}) registered", server.Name, server.Host, server.Port);

        return Ok(new { server = View(server), token });
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Edit(int id, [FromBody] ServerForm form, CancellationToken ct)
    {
        if (await Denied(ct) is { } denial) return denial;

        var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (server is null) return NotFound("No such server.");
        if (InMatch(server)) return Conflict("That server is in a match. Wait for it to finish.");

        if (form.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(form.Name)) return BadRequest("A name is required.");
            server.Name = form.Name.Trim();
        }
        if (form.Region is not null) server.Region = form.Region.Trim();

        if (form.Type is not null)
        {
            if (!TryType(form.Type, out var type)) return BadRequest("Type must be Competitive or Wingman.");
            server.Type = type;
        }

        // Host and port move together: the unique pair has to stay unique.
        if (form.Host is not null || form.Port is not null)
        {
            var host = (form.Host ?? server.Host).Trim();
            var port = form.Port ?? server.Port;
            if (host.Length == 0) return BadRequest("A host is required.");
            if (port is < 1 or > 65535) return BadRequest("Port must be between 1 and 65535.");
            if (await db.Servers.AnyAsync(s => s.Id != id && s.Host == host && s.Port == port, ct))
                return Conflict("That host and port is already registered.");

            server.Host = host;
            server.Port = port;
        }

        // "" clears the game password, null leaves it alone
        if (form.GamePassword is not null)
            server.GamePassword = form.GamePassword.Length == 0 ? null : form.GamePassword;

        // Replaced, never read back: the old one cannot be shown to compare.
        if (!string.IsNullOrWhiteSpace(form.RconPassword))
            server.RconPasswordEncrypted = secrets.Encrypt(form.RconPassword);

        if (form.IsEnabled is not null) server.IsEnabled = form.IsEnabled.Value;

        await db.SaveChangesAsync(ct);
        return Ok(View(server));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        if (await Denied(ct) is { } denial) return denial;

        var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (server is null) return NotFound("No such server.");

        /* Deleting a server mid-match would leave ten players on a machine nobody
           is tracking, and the result would have nowhere to be reported. Disable
           it instead: it stops being handed out, and goes when the match ends. */
        if (InMatch(server)) return Conflict("That server is in a match. Disable it instead, or wait.");

        db.Servers.Remove(server);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Server {Name} removed", server.Name);
        return NoContent();
    }

    /// A new server token, when the old one is lost or should stop working.
    [HttpPost("{id:int}/token")]
    public async Task<IActionResult> RotateToken(int id, CancellationToken ct)
    {
        if (await Denied(ct) is { } denial) return denial;

        var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (server is null) return NotFound("No such server.");

        var token = Secrets.New();
        server.TokenHash = Secrets.Hash(token);
        await db.SaveChangesAsync(ct);

        log.LogInformation("Server {Name} token rotated", server.Name);
        return Ok(new { token });
    }

    // Reserved or hosting: something is depending on this server right now.
    private static bool InMatch(GameServer s) =>
        s.Status is ServerStatus.Reserved or ServerStatus.Hosting || s.CurrentMatchId is not null;

    private static bool TryType(string? value, out ServerType type)
    {
        if (string.IsNullOrWhiteSpace(value)) { type = ServerType.Competitive; return true; }
        return Enum.TryParse(value, ignoreCase: true, out type);
    }

    private static ServerView View(GameServer s) => new(
        s.Id, s.Name, s.Region, s.Type.ToString(), s.Host, s.Port,
        s.Status.ToString(), s.CurrentMatchId, s.LastSeenAt,
        s.IsEnabled, s.RconPasswordEncrypted.Length > 0, s.GamePassword is not null, s.CreatedAt);

    private async Task<IActionResult?> Denied(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();
        return me.IsAdmin ? null : StatusCode(StatusCodes.Status403Forbidden, "Admins only.");
    }
}
