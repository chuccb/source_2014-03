using System.Buffers;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace KncWX2Server.Core.Common;

/// <summary>Deferred actor registry matching native KActorManager ordering and lifecycle.</summary>
public sealed class ServerActorManager
{
    private readonly object _gate = new();
    private readonly List<ServerActor> _actors = [with(capacity: 5000)];
    private readonly Dictionary<long, ServerActor> _actorsByUid = [];
    private readonly ConcurrentQueue<ServerActor> _pendingAdd = new();
    private readonly ConcurrentQueue<ServerActor> _pendingDelete = new();
    private readonly Dictionary<long, ServerActor> _pendingAddById = [];
    private readonly HashSet<long> _cancelledBeforeAdd = [];

    public int Count
    {
        get
        {
            lock (_gate)
                return _actors.Count;
        }
    }

    public ServerActor Create(long id, Func<ServerActor, KEvent, ValueTask> eventProcessor)
    {
        ArgumentNullException.ThrowIfNull(eventProcessor);

        var actor = new ServerActor(id);
        actor.SetEventProcessor(eventProcessor);

        lock (_gate)
            _pendingAddById[id] = actor;

        _pendingAdd.Enqueue(actor);
        return actor;
    }

    public ServerActor? Get(long uid)
    {
        lock (_gate)
            return _actorsByUid.GetValueOrDefault(uid);
    }

    public void ReserveDelete(ServerActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        _pendingDelete.Enqueue(actor);
    }

    public bool QueueingTo(long uid, KEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (Get(uid) is not { } actor)
            return false;

        actor.QueueingEvent(@event);
        return true;
    }

    public void QueueingToAll(KEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        lock (_gate)
        {
            foreach (var actor in _actors)
                actor.QueueingEvent(@event.Clone());
        }
    }

    public async ValueTask TickAsync()
    {
        ServerActor[]? rentedActors = null;
        var actorCount = 0;

        lock (_gate)
        {
            actorCount = _actors.Count;
            if (actorCount != 0)
            {
                rentedActors = ArrayPool<ServerActor>.Shared.Rent(actorCount);
                _actors.CopyTo(rentedActors, 0);
            }
        }

        try
        {
            // Native KActorManager::Tick(): process the existing actor vector first.
            if (rentedActors is not null)
            {
                for (var i = 0; i < actorCount; i++)
                    await rentedActors[i].TickAsync().ConfigureAwait(false);
            }

            // Native order: deferred deletion, then deferred addition.
            while (_pendingDelete.TryDequeue(out var actor))
            {
                lock (_gate)
                {
                    if (actor.Uid == 0)
                    {
                        _cancelledBeforeAdd.Add(actor.Id);
                        _pendingAddById.Remove(actor.Id);
                        continue;
                    }

                    if (_actorsByUid.Remove(actor.Uid, out var registered))
                        _actors.Remove(registered);
                }
            }

            while (_pendingAdd.TryDequeue(out var actor))
            {
                lock (_gate)
                {
                    _pendingAddById.Remove(actor.Id);
                    if (_cancelledBeforeAdd.Remove(actor.Id))
                        continue;

                    actor.Uid = GenerateTemporaryUid();
                    if (_actorsByUid.TryAdd(actor.Uid, actor))
                        _actors.Add(actor);
                }
            }
        }
        finally
        {
            if (rentedActors is not null)
                ArrayPool<ServerActor>.Shared.Return(rentedActors, clearArray: true);
        }
    }

    public bool UpdateUid(long oldUid, long newUid)
    {
        lock (_gate)
        {
            if (!_actorsByUid.TryGetValue(oldUid, out var actor))
                return false;

            _actorsByUid.Remove(oldUid);
            actor.Uid = newUid;

            if (_actorsByUid.TryAdd(newUid, actor))
                return true;

            return false;
        }
    }

    public int GetMaxQueueSize(out long actorUid)
    {
        lock (_gate)
        {
            actorUid = 0;
            var max = 0;

            foreach (var actor in _actors)
            {
                if (actor.MaxQueueSize <= max)
                    continue;

                max = actor.MaxQueueSize;
                actorUid = actor.Uid;
            }

            return max;
        }
    }

    private long GenerateTemporaryUid()
    {
        Span<byte> bytes = stackalloc byte[8];
        while (true)
        {
            RandomNumberGenerator.Fill(bytes);
            var pureUid = BitConverter.ToUInt64(bytes) & 0x000000ffffffffffUL;
            if (pureUid == 0)
                continue;

            var uid = unchecked((long)(pureUid | 0x4000000000000000UL));
            if (!_actorsByUid.ContainsKey(uid))
                return uid;
        }
    }
}
