using KncWX2Server.Core.Common;

namespace KncWX2Server.Core.Common.Threading;

/// <summary>
/// Event-worker manager corresponding to the legacy KThreadManager.
/// Workers drain queued events before honoring a reserved shutdown.
/// </summary>
public abstract class KThreadManager : KPerformer, IAsyncDisposable
{
    private readonly Lock _threadGate = new();
    private readonly List<KThread> _threads = [];
    private int _terminateReserved;
    private int _terminatedCount;

    public int ThreadCount
    {
        get
        {
            lock (_threadGate)
                return _threads.Count;
        }
    }

    public int TerminatedCount => Volatile.Read(ref _terminatedCount);

    public bool TerminationReserved => Volatile.Read(ref _terminateReserved) != 0;

    public virtual void Init(int threadCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threadCount);

        lock (_threadGate)
        {
            while (_threads.Count < threadCount)
                _threads.Add(CreateThread());
        }
    }

    public virtual void BeginThread()
    {
        lock (_threadGate)
        {
            foreach (var thread in _threads)
                thread.Begin();
        }
    }

    /// <summary>
    /// KThreadManager itself does not handle events; its worker subclasses do.
    /// This matches the legacy empty KThreadManager::ProcessEvent implementation.
    /// </summary>
    protected override void ProcessEvent(KEvent eventObject)
    {
    }

    public async ValueTask EndThreadAsync(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        Interlocked.Exchange(ref _terminateReserved, 1);

        KThread[] threads;
        lock (_threadGate)
            threads = [.. _threads];

        var deadline = DateTime.UtcNow + timeout;
        var gracefullyExited = true;

        foreach (var thread in threads)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero || !await thread.WaitAsync(remaining).ConfigureAwait(false))
            {
                gracefullyExited = false;
                break;
            }
        }

        if (!gracefullyExited)
        {
            foreach (var thread in threads)
                thread.RequestStop();

            foreach (var thread in threads)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero)
                    await thread.WaitAsync(remaining).ConfigureAwait(false);
                thread.CleanupCompletedTask();
            }
        }

        lock (_threadGate)
            _threads.Clear();

        Interlocked.Exchange(ref _terminateReserved, 0);
        Interlocked.Exchange(ref _terminatedCount, 0);
    }

    public void SetThreadNum(int threadCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threadCount);

        List<KThread> removed;
        lock (_threadGate)
        {
            if (threadCount == _threads.Count)
                return;

            removed = _threads.Skip(threadCount).ToList();
            _threads.RemoveRange(threadCount, _threads.Count - threadCount);

            while (_threads.Count < threadCount)
            {
                var thread = CreateThread();
                _threads.Add(thread);
                thread.Begin();
            }
        }

        foreach (var thread in removed)
            _ = thread.EndAsync(TimeSpan.FromSeconds(3));
    }

    protected bool TryGetEventForWorker(out KEvent? eventObject, out bool terminate)
    {
        if (GetKEvent(out eventObject!))
        {
            terminate = false;
            return true;
        }

        if (TerminationReserved && QueueSize == 0)
        {
            Interlocked.Increment(ref _terminatedCount);
            eventObject = null;
            terminate = true;
            return true;
        }

        terminate = false;
        return false;
    }

    protected abstract KThread CreateThread();

    public ValueTask DisposeAsync() => EndThreadAsync(TimeSpan.FromSeconds(10));
}
