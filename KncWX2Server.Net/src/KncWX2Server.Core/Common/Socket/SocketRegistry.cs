namespace KncWX2Server.Core.Common.Socket;

/// <summary>Managed equivalent of the legacy IOCP completion-key to socket map.</summary>
public sealed class SocketRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<uint, TcpSocketConnection> _connections = [];

    public int Count { get { lock (_gate) return _connections.Count; } }

    public uint Register(TcpSocketConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        lock (_gate)
        {
            uint key;
            do
                key = unchecked((uint)Random.Shared.NextInt64(1, (long)uint.MaxValue + 1));
            while (_connections.ContainsKey(key));

            _connections.Add(key, connection);
            connection.SetCompletionKey(key);
            return key;
        }
    }

    public bool TryGet(uint key, out TcpSocketConnection? connection)
    {
        lock (_gate)
            return _connections.TryGetValue(key, out connection);
    }

    public bool Remove(uint key)
    {
        lock (_gate)
            return _connections.Remove(key);
    }

    public IReadOnlyList<TcpSocketConnection> Snapshot()
    {
        lock (_gate)
            return [.. _connections.Values];
    }
}
