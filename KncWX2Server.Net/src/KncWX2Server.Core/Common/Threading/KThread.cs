namespace KncWX2Server.Core.Common.Threading;

/// <summary>Managed thread abstraction corresponding to legacy KThread.</summary>
public abstract class KThread : IAsyncDisposable
{
    private readonly object _gate = new();
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
            _task = Task.Run(() => RunAsync(_shutdown.Token), CancellationToken.None);
            return true;
        }
    }

    public async ValueTask EndAsync(TimeSpan timeout)
    {
        Task? task;
        CancellationTokenSource? shutdown;

        lock (_gate)
        {
            task = _task;
            shutdown = _shutdown;
        }

        if (task is null)
            return;

        shutdown?.Cancel();

        try
        {
            await task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Legacy End() also returns after its timeout if the worker did not exit.
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(task, _task) && task.IsCompleted)
                {
                    _task = null;
                    _shutdown?.Dispose();
                    _shutdown = null;
                }
            }
        }
    }

    public ValueTask DisposeAsync() => EndAsync(TimeSpan.FromSeconds(3));

    protected abstract Task RunAsync(CancellationToken cancellationToken);
}
