using System.Collections.Concurrent;
using System.Net.Sockets;
using KncWX2Server.Core;
using KncWX2Server.Core.Common;
using KncWX2Server.Persistence;

namespace KncWX2Server.Host;

public sealed class KncServerHost(ServerOptions options, SqliteDatabase database)
{
    private readonly ConcurrentDictionary<long, Task> _sessions = new();
    private long _nextSessionId;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await database.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var listener = new TcpListener(options.BindAddress, options.Port);
        listener.Start(options.Backlog);

        Console.WriteLine($"[{options.Role}] listening on {options.BindAddress}:{options.Port}");
        Console.WriteLine($"SQLite: {database.DatabasePath}; workers={options.WorkerCount}; packet-auth-limit={options.PacketAuthFailLimit}; sequence-check={options.CheckSequenceNumbers}; no-delay={options.NoDelay}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Legacy accepter enables Nagle by default; only --no-delay maps to TCP_NODELAY.
                client.NoDelay = options.NoDelay;

                var sessionId = Interlocked.Increment(ref _nextSessionId);
                var session = new KncServerSession(sessionId, client, options, DispatchAsync, cancellationToken);
                var task = session.RunAsync();
                _sessions[sessionId] = task;
                _ = RemoveCompletedSessionAsync(sessionId, task);
            }
        }
        finally
        {
            listener.Stop();
            await WaitForSessionsAsync().ConfigureAwait(false);
        }
    }

    private static ValueTask DispatchAsync(KncServerSession session, KEvent @event)
    {
        Console.WriteLine($"[{session.SessionId}] event={@event.EventId} spi={session.Spi} payload={@event.Buffer.Length} bytes");

        // Role-specific opcode handlers are not guessed here. The native server
        // routes KEvent through actor/server managers; those callers are the next
        // subsystem to port once this transport contract is stable.
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
            await Task.WhenAll(pending).ConfigureAwait(false);
    }
}

public static class KncServerBootstrap
{
    private static readonly List<ServerRole> _roles =
        [ServerRole.Login, ServerRole.Center, ServerRole.Channel, ServerRole.Game];

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
        await new KncServerHost(options, database).RunAsync(shutdown.Token).ConfigureAwait(false);
    }
}
