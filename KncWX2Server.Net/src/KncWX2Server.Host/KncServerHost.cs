using System.Collections.Concurrent;
using System.Net.Sockets;
using KncWX2Server.Core;
using KncWX2Server.Persistence;

namespace KncWX2Server.Host;

public sealed class KncServerHost(ServerOptions options, SqliteDatabase database)
{
    private readonly ConcurrentDictionary<long, Task> _sessions = new();
    private long _nextSessionId;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await database.InitializeAsync(cancellationToken);

        var listener = new TcpListener(options.BindAddress, options.Port);
        listener.Start(options.Backlog);

        Console.WriteLine($"[{options.Role}] listening on {options.BindAddress}:{options.Port}");
        Console.WriteLine($"SQLite: {database.DatabasePath}; workers={options.WorkerCount}; packet-auth-limit={options.PacketAuthFailLimit}; sequence-check={options.CheckSequenceNumbers}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                client.NoDelay = true;
                var sessionId = Interlocked.Increment(ref _nextSessionId);
                var task = HandleClientAsync(sessionId, client, cancellationToken);
                _sessions[sessionId] = task;
                _ = RemoveCompletedSessionAsync(sessionId, task);
            }
        }
        finally
        {
            listener.Stop();
            await WaitForSessionsAsync();
        }
    }

    private async Task HandleClientAsync(long sessionId, TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            Console.WriteLine($"[{options.Role}] client #{sessionId} connected from {client.Client.RemoteEndPoint}");

            try
            {
                await using var stream = client.GetStream();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var packet = await KncProtocol.ReadAsync(stream, options.MaxPayloadBytes, cancellationToken);
                    await DispatchAsync(sessionId, packet, stream, cancellationToken);
                }
            }
            catch (EndOfStreamException)
            {
                Console.WriteLine($"[{options.Role}] client #{sessionId} disconnected");
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                Console.WriteLine($"[{options.Role}] client #{sessionId} socket closed: {ex.Message}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{options.Role}] client #{sessionId} failed: {ex}");
            }
        }
    }

    private static ValueTask DispatchAsync(long sessionId, Packet packet, NetworkStream stream, CancellationToken cancellationToken)
    {
        Console.WriteLine($"packet session={sessionId} opcode=0x{packet.Opcode:X4} flags=0x{packet.Flags:X4} payload={packet.Payload.Length} bytes");

        // Legacy opcode routing is intentionally not guessed here. The old server's
        // exact wire contract is distributed across the native event/protocol code.
        // This transport layer keeps framing lossless until each opcode is ported.
        return ValueTask.CompletedTask;
    }

    private async Task RemoveCompletedSessionAsync(long sessionId, Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
        }
    }

    private async Task WaitForSessionsAsync()
    {
        var pending = _sessions.Values.ToArray();
        if (pending.Length != 0)
            await Task.WhenAll(pending);
    }
}

public static class KncServerBootstrap
{
    private static readonly List<ServerRole> _roles =
        [with(capacity: 4), ServerRole.Login, ServerRole.Center, ServerRole.Channel, ServerRole.Game];

    public static IReadOnlyList<ServerRole> Roles => _roles;

    public static async Task RunAsync(string[] args, ServerRole role, int defaultPort)
    {
        var options = ServerOptions.Parse(args, role, defaultPort);
        using var shutdown = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        await using var database = new SqliteDatabase(options.DatabasePath);
        await new KncServerHost(options, database).RunAsync(shutdown.Token);
    }
}
