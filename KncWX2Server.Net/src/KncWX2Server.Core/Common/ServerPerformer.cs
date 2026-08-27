namespace KncWX2Server.Core.Common;

public sealed class ServerPerformer
{
    private readonly ServerEventQueue _events = new();
    private readonly Func<ServerPerformer, KEvent, ValueTask> _eventProcessor;

    public ServerPerformer(uint performerId, Func<ServerPerformer, KEvent, ValueTask> eventProcessor)
    {
        ArgumentNullException.ThrowIfNull(eventProcessor);
        PerformerId = performerId;
        _eventProcessor = eventProcessor;
    }

    public uint PerformerId { get; }
    public int QueueSize => _events.Count;
    public int MaxQueueSize => _events.MaxCount;

    public void QueueingEvent(KEvent @event)
    {
        _events.Enqueue(@event);
    }

    public async ValueTask TickAsync()
    {
        while (_events.TryDequeue(out var @event))
            await _eventProcessor(this, @event!).ConfigureAwait(false);
    }
}
