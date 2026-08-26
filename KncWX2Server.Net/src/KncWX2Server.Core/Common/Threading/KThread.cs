namespace KncWX2Server.Core.Common.Threading;

/// <summary>Managed thread abstraction corresponding to legacy KThread.</summary>
public abstract class KThread : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private CancellationTokenSource? _shutdown;
    private Task? _task;

    public bool IsRunning => _task is { IsCompleted: false };

    public bool Begin()
    {
        lock (_gate)
        {
            if (_task is { IsCompleted: false })
                return false;

            _shutdown?.Dispose();
            _shutdown = new CancellationTokenSource();
            var cancellationToken = _shutdown.Token;
            _task = Task.Run(() => RunAsync(cancellationToken), CancellationToken.None);
            return true;
        }
    }

    /// <summary>Requests immediate cancellation, matching a direct KThread shutdown.</summary>
    public void RequestStop()
    {
        lock (_gate)
            _shutdown?.Cancel();
    }

    /// <summary>Waits for the current worker without changing its shutdown mode.</summary>
    public async ValueTask<bool> WaitAsync(TimeSpan timeout)
    {
        Task? task;
        lock (_gate)
            task = _task;

        if (task is null)
            return true;

        try
        {
            await task.WaitAsync(timeout).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async ValueTask EndAsync(TimeSpan timeout)
    {
        RequestStop();
        await WaitAsync(timeout).ConfigureAwait(false);
        CleanupCompletedTask();
    }

    protected void CleanupCompletedTask()
    {
        lock (_gate)
        {
            if (_task is { IsCompleted: true })
            {
                _task = null;
                _shutdown?.Dispose();
                _shutdown = null;
            }
        }
    }

    public ValueTask DisposeAsync() => EndAsync(TimeSpan.FromSeconds(3));

    protected abstract Task RunAsync(CancellationToken cancellationToken);
}
