using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Database;
using KncWX2Server.Core.Common.Routing;

namespace KncWX2Server.Persistence;

/// <summary>
/// SQLite replacement for the legacy KDBLayer.
/// It preserves the logical DB-agent boundary and routes KEvents by performer class.
/// </summary>
public sealed class SqliteDbLayer : IAsyncDisposable
{
    private readonly Dictionary<DbConnectionId, SqliteDbAgent> _agents = [];
    private readonly Dictionary<DbConnectionId, SqliteDatabase> _databases = [];
    private readonly IReadOnlyDictionary<DbConnectionId, string> _databasePaths;
    private readonly int _workerCount;
    private bool _initialized;
    private bool _started;

    public SqliteDbLayer(
        IReadOnlyDictionary<DbConnectionId, string> databasePaths,
        int workerCount = 1)
    {
        ArgumentNullException.ThrowIfNull(databasePaths);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workerCount);

        if (databasePaths.Count == 0)
            throw new ArgumentException("At least one logical DB path is required.", nameof(databasePaths));

        _databasePaths = databasePaths;
        _workerCount = workerCount;
    }

    public IReadOnlyDictionary<DbConnectionId, SqliteDbAgent> Agents => _agents;

    public void RegisterHandler(DbConnectionId connectionId, ISqliteDbEventHandler handler)
    {
        GetOrCreateAgent(connectionId).RegisterHandler(handler);
    }

    public void SetUnknownEventHandler(Action<DbConnectionId, ushort> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        foreach (var agent in _agents.Values)
            agent.UnknownEvent = handler;
    }

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        foreach (var pair in _databasePaths)
        {
            var agent = GetOrCreateAgent(pair.Key);
            var database = _databases[pair.Key];
            await database.InitializeAsync(cancellationToken).ConfigureAwait(false);
            agent.Init(_workerCount);
        }

        _initialized = true;
    }

    public void Start()
    {
        if (!_initialized)
            throw new InvalidOperationException("InitializeAsync must complete before Start.");

        if (_started)
            return;

        foreach (var agent in _agents.Values)
            agent.BeginThread();

        _started = true;
    }

    public bool QueueingEvent(KEvent eventObject)
    {
        ArgumentNullException.ThrowIfNull(eventObject);

        if (!DbConnectionIdExtensions.TryMapPerformerClass(
                PerformerRouting.GetPerformerClass(eventObject.Destination.PerformerId),
                out var connectionId))
            return false;

        return _agents.TryGetValue(connectionId, out var agent) &&
               QueueToAgent(agent, eventObject);
    }

    public async ValueTask ShutdownAsync(TimeSpan? timeout = null)
    {
        if (!_started)
            return;

        var wait = timeout ?? TimeSpan.FromSeconds(10);
        foreach (var agent in _agents.Values)
            await agent.EndThreadAsync(wait).ConfigureAwait(false);

        _started = false;
    }

    private static bool QueueToAgent(SqliteDbAgent agent, KEvent eventObject)
    {
        agent.QueueingEvent(eventObject);
        return true;
    }

    private SqliteDbAgent GetOrCreateAgent(DbConnectionId connectionId)
    {
        if (_agents.TryGetValue(connectionId, out var existing))
            return existing;

        if (!_databasePaths.TryGetValue(connectionId, out var path) || string.IsNullOrWhiteSpace(path))
            throw new KeyNotFoundException($"No SQLite database path is configured for {connectionId}.");

        var database = new SqliteDatabase(path);
        var handlers = new SqliteDbEventHandlerRegistry();
        var agent = new SqliteDbAgent(connectionId, database, handlers);

        _databases.Add(connectionId, database);
        _agents.Add(connectionId, agent);
        return agent;
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);

        foreach (var database in _databases.Values)
            await database.DisposeAsync().ConfigureAwait(false);

        _agents.Clear();
        _databases.Clear();
    }
}
