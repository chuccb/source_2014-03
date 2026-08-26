namespace KncWX2Server.Core.Common.Database;

using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Threading;

/// <summary>
/// Logical database-agent contract corresponding to the legacy KDBThreadManager.
/// One agent owns one logical DB queue and a configurable number of workers.
/// Concrete persistence implementations decide how an event is executed.
/// </summary>
public abstract class KDbEventAgent(DbConnectionId connectionId) : KThreadManager
{
    private readonly DbConnectionId _connectionId = connectionId;

    public DbConnectionId ConnectionId => _connectionId;

    protected override KThread CreateThread() => new Worker(this);

    protected abstract ValueTask ProcessDbEventAsync(
        KEvent eventObject,
        CancellationToken cancellationToken);

    private sealed class Worker(KDbEventAgent owner) : KThread
    {
        private readonly KDbEventAgent _owner =
            owner ?? throw new ArgumentNullException(nameof(owner));

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (_owner.TryGetEventForWorker(out var eventObject, out var terminate))
                {
                    if (terminate)
                        return;

                    if (eventObject is not null)
                        await _owner.ProcessDbEventAsync(eventObject, cancellationToken).ConfigureAwait(false);

                    continue;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}
