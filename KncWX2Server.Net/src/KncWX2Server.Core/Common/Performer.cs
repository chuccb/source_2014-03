using System.Collections.Concurrent;

namespace KncWX2Server.Core.Common;

public abstract class KPerformer : SimObject
{
    private readonly ConcurrentQueue<KEvent> _eventQueue = [];
    private int _queueCount;
    private long _maxQueueSize;
    private DateTimeOffset _maxQueueRegisteredAt;

    public int QueueSize => Volatile.Read(ref _queueCount);

    public QueueSizeSnapshot GetMaxQueueSize() =>
        new((nuint)Math.Max(0, Volatile.Read(ref _maxQueueSize)), _maxQueueRegisteredAt);

    public QueueSizeSnapshot GetMaxQueueSizeAndClear()
    {
        var snapshot = GetMaxQueueSize();
        Interlocked.Exchange(ref _maxQueueSize, 0);
        _maxQueueRegisteredAt = default;
        return snapshot;
    }

    public void QueueingEvent(KEvent eventObject)
    {
        ArgumentNullException.ThrowIfNull(eventObject);

        _eventQueue.Enqueue(eventObject);
        var queueSize = Interlocked.Increment(ref _queueCount);
        UpdateMaxQueueSize(queueSize);
    }

    public virtual void Tick()
    {
        while (GetKEvent(out var eventObject))
            ProcessEvent(eventObject);
    }

    protected bool GetKEvent(out KEvent eventObject)
    {
        if (!_eventQueue.TryDequeue(out eventObject!))
            return false;

        Interlocked.Decrement(ref _queueCount);
        return true;
    }

    protected abstract void ProcessEvent(KEvent eventObject);

    public abstract int GetPfId();

    protected virtual bool RoutePacket(KEvent eventObject) => false;

    private void UpdateMaxQueueSize(int queueSize)
    {
        while (true)
        {
            var current = Volatile.Read(ref _maxQueueSize);
            if (queueSize <= current)
                return;
            if (Interlocked.CompareExchange(ref _maxQueueSize, queueSize, current) == current)
            {
                _maxQueueRegisteredAt = DateTimeOffset.Now;
                return;
            }
        }
    }
}

public readonly record struct QueueSizeSnapshot(nuint Size, DateTimeOffset RegisteredAt);
