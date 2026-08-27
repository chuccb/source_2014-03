using System.Collections.Concurrent;

namespace KncWX2Server.Core.Common;

/// <summary>FIFO event queue matching the ownership boundary of native KPerformer.</summary>
public sealed class ServerEventQueue
{
    private readonly ConcurrentQueue<KEvent> _events = new();
    private long _count;
    private long _maxCount;

    public int Count => checked((int)Math.Max(0, Volatile.Read(ref _count)));
    public int MaxCount => checked((int)Math.Max(0, Volatile.Read(ref _maxCount)));

    public void Enqueue(KEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _events.Enqueue(@event);

        var count = Interlocked.Increment(ref _count);
        UpdateMaximum(count);
    }

    public bool TryDequeue(out KEvent? @event)
    {
        if (!_events.TryDequeue(out @event))
            return false;

        Interlocked.Decrement(ref _count);
        return true;
    }

    public void Clear()
    {
        while (_events.TryDequeue(out _))
            Interlocked.Decrement(ref _count);
    }

    private void UpdateMaximum(long count)
    {
        while (true)
        {
            var current = Volatile.Read(ref _maxCount);
            if (count <= current || Interlocked.CompareExchange(ref _maxCount, count, current) == current)
                return;
        }
    }
}
