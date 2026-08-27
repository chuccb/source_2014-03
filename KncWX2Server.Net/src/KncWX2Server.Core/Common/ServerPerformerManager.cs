namespace KncWX2Server.Core.Common;

public sealed class ServerPerformerManager
{
    private readonly Dictionary<uint, ServerPerformer> _performers = [];

    public int Count => _performers.Count;

    public bool Register(ServerPerformer performer)
    {
        ArgumentNullException.ThrowIfNull(performer);
        return _performers.TryAdd(performer.PerformerId, performer);
    }

    public bool Remove(uint performerId) => _performers.Remove(performerId);

    public ServerPerformer? Get(uint performerId) =>
        _performers.GetValueOrDefault(performerId);

    public bool QueueingTo(uint performerId, KEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (!_performers.TryGetValue(performerId, out var performer))
            return false;

        performer.QueueingEvent(@event);
        return true;
    }

    public async ValueTask TickAsync()
    {
        foreach (var performer in _performers.Values)
            await performer.TickAsync().ConfigureAwait(false);
    }
}
