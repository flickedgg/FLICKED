using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace Flicked.Api.Services;

/* Source RCON: running one command on a CS2 server.

   Written out rather than taken from a library, because the protocol is tiny and
   this is the one place holding decrypted RCON passwords. We need exactly two
   messages (authenticate, then run a command) and none of the awkward parts a
   library exists for, like responses split across packets.

   A packet is:

       int32  length of everything after this field
       int32  id          echoed back, so a reply can be matched to a request
       int32  type        3 = authenticate, 2 = run a command, 0 = response
       bytes  body        ASCII, null-terminated
       byte   0           a second terminator the protocol insists on

   Authentication is confirmed by the reply's id: the server echoes our id when
   the password was right, and sends -1 when it was wrong. */
public class Rcon(ILogger<Rcon> log)
{
    private const int Authenticate = 3;
    private const int RunCommand = 2;
    private const int AuthFailed = -1;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// Runs one command. Returns whatever the server printed, or null if it failed.
    public async Task<string?> RunAsync(string host, int port, string password, string command,
                                        CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeout.Token);
            await using var stream = client.GetStream();

            // id 1 for the password, 2 for the command: any value works, they only
            // have to be distinguishable in the replies.
            await WriteAsync(stream, 1, Authenticate, password, timeout.Token);

            var (authId, _) = await ReadAsync(stream, timeout.Token);
            if (authId == AuthFailed)
            {
                // Never log the password, and never say which server it was in a way
                // that pairs the two up in one line.
                log.LogWarning("RCON on {Host}:{Port} refused the password", host, port);
                return null;
            }

            await WriteAsync(stream, 2, RunCommand, command, timeout.Token);
            var (_, body) = await ReadAsync(stream, timeout.Token);
            return body;
        }
        catch (Exception e) when (e is SocketException or IOException or OperationCanceledException)
        {
            // A server being unreachable is an ordinary outcome here, not a crash:
            // the caller decides what to do about a match that cannot be started.
            log.LogWarning(e, "RCON to {Host}:{Port} failed", host, port);
            return null;
        }
    }

    private static async Task WriteAsync(NetworkStream stream, int id, int type, string body,
                                         CancellationToken ct)
    {
        var text = Encoding.ASCII.GetBytes(body);
        var packet = new byte[14 + text.Length];   // 4 length + 4 id + 4 type + body + 2 nulls

        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0), packet.Length - 4);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4), id);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(8), type);
        text.CopyTo(packet.AsSpan(12));
        // the last two bytes stay 0: one ends the body, one ends the packet

        await stream.WriteAsync(packet, ct);
    }

    private static async Task<(int Id, string Body)> ReadAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);

        // Sanity bounds: this number comes off the wire, and a hostile or broken
        // server should not be able to make us allocate whatever it likes.
        if (length is < 10 or > 8192) throw new IOException($"RCON packet length {length} is out of range.");

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, ct);

        var id = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0));
        // body starts after id and type, and ends before the two null terminators
        var body = Encoding.ASCII.GetString(payload, 8, Math.Max(0, length - 10));
        return (id, body);
    }
}
