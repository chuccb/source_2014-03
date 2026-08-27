namespace KncWX2Server.Core.Common;

/// <summary>Thread-safe server-list snapshot matching native KBaseServer::KServerList replacement/copy semantics.</summary>
public sealed class ServerList
{
    private readonly object _gate = new();
    private Dictionary<int, ServerInfo[]> _serversByGroup = [];

    public void Replace(IReadOnlyDictionary<int, IReadOnlyList<ServerInfo>> serversByGroup)
    {
        ArgumentNullException.ThrowIfNull(serversByGroup);

        var snapshot = new Dictionary<int, ServerInfo[]>(serversByGroup.Count);
        foreach (var group in serversByGroup)
        {
            ArgumentNullException.ThrowIfNull(group.Value);
            var servers = new ServerInfo[group.Value.Count];
            for (var i = 0; i < servers.Length; i++)
                servers[i] = group.Value[i];

            snapshot.Add(group.Key, servers);
        }

        lock (_gate)
            _serversByGroup = snapshot;
    }

    public IReadOnlyDictionary<int, IReadOnlyList<ServerInfo>> Snapshot()
    {
        lock (_gate)
        {
            var snapshot = new Dictionary<int, IReadOnlyList<ServerInfo>>(_serversByGroup.Count);
            foreach (var group in _serversByGroup)
                snapshot.Add(group.Key, group.Value);

            return snapshot;
        }
    }
}
