using Microsoft.AspNetCore.DataProtection;

namespace Flicked.Api.Services;

/* Encrypting RCON passwords.

   Hashing is wrong here. A session token is only ever checked, so a one-way hash
   is enough; an RCON password has to be sent to the CS2 server, so it must come
   back out. Hash what you verify, encrypt what you replay.

   ASP.NET's Data Protection does the work: AES-256-CBC with an HMAC, and key
   rotation handled for us. What it defends against is a database-only leak: a
   stolen backup or a dump yields ciphertext and nothing else. It does not defend
   against someone who owns the machine, because the keys live there too.

   The keys must outlive the process. If they are lost, every stored password
   becomes permanently unreadable and every server has to be registered again, so
   in Docker the key folder belongs on a volume (see Program.cs). */
public class ServerSecrets(IDataProtectionProvider provider)
{
    // The purpose string scopes the key: ciphertext from here cannot be decrypted
    // by a protector created for anything else, even with the same key ring.
    private readonly IDataProtector _protector = provider.CreateProtector("flicked.rcon.v1");

    public string Encrypt(string password) => _protector.Protect(password);

    /* Returns null when the value cannot be read: wrong key ring, or a row written
       before the keys were lost. Callers treat that as "this server needs its RCON
       password setting again" rather than crashing the request. */
    public string? TryDecrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return null;
        try { return _protector.Unprotect(encrypted); }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }
}
