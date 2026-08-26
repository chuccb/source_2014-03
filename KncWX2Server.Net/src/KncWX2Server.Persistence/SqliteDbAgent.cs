using Microsoft.Data.Sqlite;
using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Database;

namespace KncWX2Server.Persistence;

/// <summary>
/// SQLite implementation of one logical legacy DB agent.
/// Each worker keeps one SQLite connection for its lifetime.
/// </summary>
public sealed class SqliteDbAgent(
    DbConnectionId connectionId,
    SqliteDatabase database,
    SqliteDbEventHandlerRegistry handlers) : KDbEventAgent(connectionId)
{
    private readonly SqliteDatabase _database =
        database ?? throw new ArgumentNullException(nameof(database));
    private readonly SqliteDbEventHandlerRegistry _handlers =
        handlers ?? throw new ArgumentNullException(nameof(handlers));
    private readonly AsyncLocal<SqliteConnection?> _workerConnection = new();

    public int RegisteredHandlerCount => _handlers.Count;

    public void RegisterHandler(ISqliteDbEventHandler handler) => _handlers.Register(handler);

    /// <summary>Called instead of throwing when no legacy EventId handler exists.</summary>
    public Action<DbConnectionId, ushort>? UnknownEvent { get; set; }

    protected override async ValueTask ProcessDbEventAsync(
        KEvent eventObject,
        CancellationToken cancellationToken)
    {
        if (!_handlers.TryGet(eventObject.EventId, out var handler) || handler is null)
        {
            UnknownEvent?.Invoke(ConnectionId, eventObject.EventId);
            return;
        }

        var connection = _workerConnection.Value;
        if (connection is null)
        {
            connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
            _workerConnection.Value = connection;
        }

        await handler.HandleAsync(connection, eventObject, cancellationToken).ConfigureAwait(false);
    }

    protected override async ValueTask OnWorkerStoppedAsync(CancellationToken cancellationToken)
    {
        var connection = _workerConnection.Value;
        _workerConnection.Value = null;

        if (connection is not null)
            await connection.DisposeAsync().ConfigureAwait(false);
    }
}
