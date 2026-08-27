namespace KncWX2Server.Core.Common;

using FsmType = KncWX2Server.Core.Common.Fsm.Fsm;

/// <summary>
/// Managed actor boundary corresponding to native KActor: a session owns the
/// connection while the actor owns FIFO event consumption and FSM state.
/// </summary>
public sealed class ServerActor
{
    private readonly ServerEventQueue _events = new();
    private FsmType? _fsm;
    private Func<ServerActor, KEvent, ValueTask>? _eventProcessor;

    public ServerActor(long id)
    {
        Id = id;
    }

    public long Id { get; }
    public long Uid { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public ServerEventQueue Events => _events;
    public FsmType? Fsm => _fsm;

    public void SetFsm(FsmType fsm, int initialState)
    {
        ArgumentNullException.ThrowIfNull(fsm);
        _fsm = fsm;
        _fsm.CurrentState = initialState;
    }

    public void SetEventProcessor(Func<ServerActor, KEvent, ValueTask> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _eventProcessor = processor;
    }

    public void QueueingEvent(KEvent @event)
    {
        _events.Enqueue(@event);
    }

    public async ValueTask TickAsync()
    {
        if (_eventProcessor is null)
            throw new InvalidOperationException("The actor event processor has not been configured.");

        while (_events.TryDequeue(out var @event))
        {
            await _eventProcessor(this, @event!).ConfigureAwait(false);
        }
    }

    public int QueueSize => _events.Count;
    public int MaxQueueSize => _events.MaxCount;
}
