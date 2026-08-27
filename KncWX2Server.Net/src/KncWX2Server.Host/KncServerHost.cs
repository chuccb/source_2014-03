using System.Collections.Concurrent;
using System.Net.Sockets;
using KncWX2Server.Core;
using KncWX2Server.Core.Common;
using KncWX2Server.Persistence;

namespace KncWX2Server.Host;

public sealed class KncServerHost(ServerOptions options, SqliteDatabase database)
{
    private readonly ConcurrentDictionary<long, Task> _sessions = new();
    private readonly ConcurrentDictionary<long, ServerActor> _sessionActors = new();
    private readonly ServerActorManager _actors = new();
    private long _nextSessionId;

    public int ActorCount => _actors.Count;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await database.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var listener = new TcpListener(options.BindAddress, options.Port);
        listener.Start(options.Backlog);

        using var tickCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tickTask = RunActorTicksAsync(tickCancellation.Token);

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

                client.NoDelay = options.NoDelay;

                var sessionId = Interlocked.Increment(ref _nextSessionId);
                var actor = _actors.Create(sessionId, ProcessActorEventAsync);
                _sessionActors[sessionId] = actor;

                var session = new KncServerSession(
                    sessionId,
                    client,
                    options,
                    QueueSessionEventAsync,
                    cancellationToken);

                var task = RunSessionAsync(sessionId, session, actor);
                _sessions[sessionId] = task;
                _ = RemoveCompletedSessionAsync(sessionId, task);
            }
        }
        finally
        {
            listener.Stop();
            tickCancellation.Cancel();

            try
            {
                await tickTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (tickCancellation.IsCancellationRequested)
            {
            }

            await WaitForSessionsAsync().ConfigureAwait(false);
        }
    }

    private ValueTask QueueSessionEventAsync(KncServerSession session, KEvent @event)
    {
        if (_sessionActors.TryGetValue(session.SessionId, out var actor))
            actor.QueueingEvent(@event);
        return ValueTask.CompletedTask;
    }

    private static ValueTask ProcessActorEventAsync(ServerActor actor, KEvent @event)
    {
        Console.WriteLine($"actor={actor.Id} uid={actor.Uid} event={@event.EventId} payload={@event.Buffer.Length} bytes");

        // The shared layer owns only queue/FSM delivery. Individual role opcode
        // handlers are attached in the corresponding service migration stage.
        return ValueTask.CompletedTask;
    }

    private async Task RunActorTicksAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            await _actors.TickAsync().ConfigureAwait(false);
    }

    private async Task RunSessionAsync(long sessionId, KncServerSession session, ServerActor actor)
    {
        try
        {
            await session.RunAsync().ConfigureAwait(false);
        }
        finally
        {
            _actors.ReserveDelete(actor);
        }
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
            _sessionActors.TryRemove(sessionId, out _);
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
    private static readonly ServerRole[] RolesValue =
        [ServerRole.Login, ServerRole.Center, ServerRole.Channel, ServerRole.Game];

    public static IReadOnlyList<ServerRole> Roles => RolesValue;

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
