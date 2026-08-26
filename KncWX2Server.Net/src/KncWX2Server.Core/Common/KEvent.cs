using KncWX2Server.Core.Common.Serialization;

namespace KncWX2Server.Core.Common;

/// <summary>Managed representation of the legacy KEvent.</summary>
public sealed class KEvent
{
    public KPerformerInfo Destination { get; } = new();
    public long FirstTrace { get; private set; } = -1;
    public long LastTrace { get; private set; } = -1;
    public ushort EventId { get; private set; }
    public SerBuffer Buffer { get; } = new();

    public long FirstSenderUid => FirstTrace;
    public long LastSenderUid => LastTrace == -1 ? FirstTrace : LastTrace;
    public bool IsEmptyTrace => FirstTrace == -1;

    public void SetData(uint performerId, ReadOnlySpan<long> trace, ushort eventId)
    {
        Destination.PerformerId = performerId;
        EventId = eventId;
        FirstTrace = trace.Length > 0 ? trace[0] : -1;
        LastTrace = trace.Length > 1 ? trace[1] : -1;
    }

    public void PushTrace(long uid)
    {
        if (FirstTrace == -1)
            FirstTrace = uid;
        else
            LastTrace = uid;
    }

    public void PopTrace()
    {
        if (LastTrace != -1)
            LastTrace = -1;
        else
            FirstTrace = -1;
    }

    public KEvent Clone()
    {
        var clone = new KEvent();
        clone.Destination.PerformerId = Destination.PerformerId;
        clone.Destination.Uids.UnionWith(Destination.Uids);
        clone.FirstTrace = FirstTrace;
        clone.LastTrace = LastTrace;
        clone.EventId = EventId;
        clone.Buffer.CopyStateFrom(Buffer);
        return clone;
    }

    internal bool WriteFields(KSerializer serializer) =>
        serializer.Put(Destination)
        && serializer.Put(FirstTrace)
        && serializer.Put(LastTrace)
        && serializer.Put(EventId)
        && serializer.Put(Buffer);

    internal bool ReadFields(KSerializer serializer)
    {
        if (!serializer.Get(Destination)
            || !serializer.Get(out long firstTrace)
            || !serializer.Get(out long lastTrace)
            || !serializer.Get(out ushort eventId)
            || !serializer.Get(Buffer))
            return false;

        FirstTrace = firstTrace;
        LastTrace = lastTrace;
        EventId = eventId;
        return true;
    }
}
