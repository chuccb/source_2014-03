namespace KncWX2Server.Core.Common;

public sealed class ServerPerformerManager
{
    private readonly List<ServerPerformer> _performers = [];
    private readonly Dictionary<uint, ServerPerformer> _performersById = [];

    public int Count => _performers.Count;

    public bool Register(ServerPerformer performer)
    {
        ArgumentNullException.ThrowIfNull(performer);
        if (!_performersById.TryAdd(performer.PerformerId, performer))
            return false;

        _performers.Add(performer);
        return true;
    }

    public ServerPerformer? RegisterRole(
        ServerRole role,
        Func<ServerPerformer, KEvent, ValueTask> eventProcessor)
    {
        var performer = new ServerPerformer(
            ServerRolePerformer.GetPerformerId(role),
            eventProcessor);

        return Register(performer) ? performer : null;
    }

    public bool Remove(uint performerId)
    {
        if (!_performersById.Remove(performerId, out var performer))
            return false;

        _performers.Remove(performer);
        return true;
    }

    public ServerPerformer? Get(uint performerId) =>
        _performersById.GetValueOrDefault(performerId);

    public bool QueueingTo(uint performerId, KEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (!_performersById.TryGetValue(performerId, out var performer))
            return false;

        performer.QueueingEvent(@event);
        return true;
    }

    public async ValueTask TickAsync()
    {
        foreach (var performer in _performers)
            await performer.TickAsync().ConfigureAwait(false);
    }
}
