using Microsoft.Data.Sqlite;
using KncWX2Server.Core.Common;

namespace KncWX2Server.Persistence;

/// <summary>
/// Handles one legacy KEvent EventId for one logical SQLite database agent.
/// Event-specific decoding and SQL belong here, not in the routing layer.
/// </summary>
public interface ISqliteDbEventHandler
{
    ushort EventId { get; }

    ValueTask HandleAsync(
        SqliteConnection connection,
        KEvent eventObject,
        CancellationToken cancellationToken);
}

public sealed class SqliteDbEventHandlerRegistry
{
    private readonly Dictionary<ushort, ISqliteDbEventHandler> _handlers = [];

    public void Register(ISqliteDbEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!_handlers.TryAdd(handler.EventId, handler))
        {
            throw new InvalidOperationException(
                $"A SQLite DB event handler for EventId {handler.EventId} is already registered.");
        }
    }

    public bool TryGet(ushort eventId, out ISqliteDbEventHandler? handler) =>
        _handlers.TryGetValue(eventId, out handler);
}
