namespace Flicked.Api.Models;

/* The two tables behind signing in.

   A Session is a signed-in launcher. A LoginCode is the short-lived ticket the
   browser carries back to the launcher after Steam says who you are.

   Neither table stores the real secret. Both store a SHA-256 hash of it, for the
   same reason passwords are hashed: if somebody ever reads a database backup,
   they must not come away with a stack of working sessions. Hashes are checked by
   hashing what arrived and looking for that value, so the original never has to
   be written down anywhere. */

public class Session
{
    public Guid Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}

/* Steam sends the player back to the browser, but the token belongs to the launcher.
   Rather than putting the token in that redirect URL (URLs end up in browser history
   and logs), the backend puts a one-time code there. The launcher swaps the code for
   the real token in a request it makes itself. This is the same idea as OAuth's
   authorization-code step. */
public class LoginCode
{
    public Guid Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public required string CodeHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}
