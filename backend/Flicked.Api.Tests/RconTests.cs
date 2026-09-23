using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Flicked.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flicked.Api.Tests;

/* Source RCON, tested against a fake server that speaks the protocol back.

   No CS2 needed: what is being checked is the packet format and the handling of
   a refused password, and a socket on localhost proves both. */
public class RconTests
{
    [Fact]
    public async Task Authenticates_then_runs_the_command()
    {
        var received = new List<(int Type, string Body)>();
        using var fake = new FakeRconServer(received, acceptPassword: true);

        var rcon = new Rcon(NullLogger<Rcon>.Instance);
        var reply = await rcon.RunAsync("127.0.0.1", fake.Port, "secret", "matchzy_loadmatch_url http://x");

        Assert.Equal("ok", reply);
        Assert.Equal(2, received.Count);
        Assert.Equal((3, "secret"), received[0]);                                  // authenticate first
        Assert.Equal((2, "matchzy_loadmatch_url http://x"), received[1]);          // then the command
    }

    /// A wrong password must not look like a working server.
    [Fact]
    public async Task Returns_null_when_the_password_is_refused()
    {
        var received = new List<(int Type, string Body)>();
        using var fake = new FakeRconServer(received, acceptPassword: false);

        var rcon = new Rcon(NullLogger<Rcon>.Instance);
        Assert.Null(await rcon.RunAsync("127.0.0.1", fake.Port, "wrong", "echo hi"));
    }

    /// An unreachable server is an ordinary outcome, not an exception.
    [Fact]
    public async Task Returns_null_when_nothing_is_listening()
    {
        var rcon = new Rcon(NullLogger<Rcon>.Instance);
        Assert.Null(await rcon.RunAsync("127.0.0.1", 1, "x", "echo hi"));
    }

    private sealed class FakeRconServer : IDisposable
    {
        private readonly TcpListener _listener;
        public int Port { get; }

        public FakeRconServer(List<(int, string)> received, bool acceptPassword)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = Task.Run(() => ServeAsync(received, acceptPassword));
        }

        private async Task ServeAsync(List<(int, string)> received, bool acceptPassword)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync();
                await using var stream = client.GetStream();

                while (true)
                {
                    var header = new byte[4];
                    await stream.ReadExactlyAsync(header);
                    var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                    var payload = new byte[length];
                    await stream.ReadExactlyAsync(payload);

                    var id = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0));
                    var type = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4));
                    var body = Encoding.ASCII.GetString(payload, 8, length - 10);
                    received.Add((type, body));

                    // the protocol's way of refusing a password is to echo id -1
                    var replyId = type == 3 && !acceptPassword ? -1 : id;
                    await WriteAsync(stream, replyId, type == 3 ? 2 : 0, type == 3 ? "" : "ok");

                    if (type == 3 && !acceptPassword) return;
                }
            }
            catch { /* the client hanging up ends the test, not a failure */ }
        }

        private static async Task WriteAsync(NetworkStream stream, int id, int type, string body)
        {
            var text = Encoding.ASCII.GetBytes(body);
            var packet = new byte[14 + text.Length];
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0), packet.Length - 4);
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4), id);
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(8), type);
            text.CopyTo(packet.AsSpan(12));
            await stream.WriteAsync(packet);
        }

        public void Dispose() => _listener.Stop();
    }
}
