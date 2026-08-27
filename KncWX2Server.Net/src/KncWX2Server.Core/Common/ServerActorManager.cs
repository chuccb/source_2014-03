using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace KncWX2Server.Core.Common;

/// <summary>
/// Deferred actor registry corresponding to native KActorManager.
/// Tick order is intentionally actor-processing, delete, then add.
/// </summary>
public sealed class ServerActorManager
{
    private readonly ConcurrentDictionary<long, ServerActor> _actors = new();
    private readonly ConcurrentQueue<ServerActor> _pendingAdd = new();
    private readonly ConcurrentQueue<long> _pendingDelete = new();

    public int Count => _actors.Count;

    public ServerActor Create(long id, Func<ServerActor, KEvent, ValueTask> eventProcessor)
    {
        ArgumentNullException.ThrowIfNull(eventProcessor);

        var actor = new ServerActor(id);
        actor.SetEventProcessor(eventProcessor);
        _pendingAdd.Enqueue(actor);
        return actor;
    }

    public ServerActor? Get(long uid) =>
        _actors.TryGetValue(uid, out var actor) ? actor : null;

    public void ReserveDelete(long uid) => _pendingDelete.Enqueue(uid);

    public void QueueingTo(long uid, KEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Get(uid)?.QueueingEvent(@event);
    }

    public void QueueingToAll(KEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        foreach (var actor in _actors.Values)
            actor.QueueingEvent(@event.Clone());
    }

    public async ValueTask TickAsync()
    {
        // Native KActorManager::Tick() processes the current actor set first.
        foreach (var actor in _actors.Values)
            await actor.TickAsync().ConfigureAwait(false);

        // Deletions are deliberately deferred until all current actor queues ran.
        while (_pendingDelete.TryDequeue(out var uid))
            _actors.TryRemove(uid, out _);

        // Additions are deliberately deferred until the end of the tick.
        while (_pendingAdd.TryDequeue(out var actor))
        {
            actor.Uid = GenerateTemporaryUid();
            _actors.TryAdd(actor.Uid, actor);
        }
    }

    public bool UpdateUid(long oldUid, long newUid)
    {
        if (oldUid == newUid || !_actors.TryGetValue(oldUid, out var actor))
            return false;

        if (!_actors.TryAdd(newUid, actor))
            return false;

        if (_actors.TryRemove(oldUid, out _))
        {
            actor.Uid = newUid;
            return true;
        }

        _actors.TryRemove(newUid, out _);
        return false;
    }

    public int GetMaxQueueSize(out long actorUid)
    {
        actorUid = 0;
        var max = 0;

        foreach (var actor in _actors.Values)
        {
            if (actor.MaxQueueSize <= max)
                continue;

            max = actor.MaxQueueSize;
            actorUid = actor.Uid;
        }

        return max;
    }

    private long GenerateTemporaryUid()
    {
        Span<byte> bytes = stackalloc byte[8];
        while (true)
        {
            RandomNumberGenerator.Fill(bytes);
            var value = BitConverter.ToUInt64(bytes) & 0x000000ffffffffffUL;
            if (value == 0)
                continue;

            var uid = unchecked((long)(value | 0x4000000000000000UL));
            if (!_actors.ContainsKey(uid))
                return uid;
        }
    }
}
