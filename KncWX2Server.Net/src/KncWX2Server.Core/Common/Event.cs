using KncWX2Server.Core.Common.Serialization;

namespace KncWX2Server.Core.Common;

public sealed class KPerformerInfo
{
    public const int MaxUidCount = 100;

    public uint PerformerId { get; set; }
    public SortedSet<long> Uids { get; } = [];

    public bool FindUid(long uid) => Uids.Contains(uid);

    public bool AddUid(long uid)
    {
        if (Uids.Count >= MaxUidCount)
            return false;
        return Uids.Add(uid) || Uids.Contains(uid);
    }

    public int UidListSize => Uids.Count;

    public long GetFirstUid() => Uids.Count == 0 ? -1 : Uids.Min;

    internal bool WriteFields(KSerializer serializer)
    {
        return serializer.Put(PerformerId)
            && serializer.Put(Uids, static (ser, uid) => ser.Put(uid));
    }

    internal bool ReadFields(KSerializer serializer)
    {
        if (!serializer.Get(out uint performerId))
            return false;
        if (!serializer.Get(Uids, static (ser, out long uid) => ser.Get(out uid)))
            return false;

        PerformerId = performerId;
        return true;
    }
}

/// <summary>Managed representation of the legacy KEvent.</summary>
public sealed class KEvent
{
    public const ushort EventFromNone = 0;
    public const ushort EventFromServer = 1;
    public const ushort EventFromClient = 2;

    public KPerformerInfo Destination { get; } = new();
    public long FirstTrace { get; private set; } = -1;
    public long LastTrace { get; private set; } = -1;
    public ushort EventId { get; private set; }
    public ushort FromType { get; private set; } = EventFromNone;
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

    public void SetFromType(ushort fromType) => FromType = fromType;

    public KEvent Clone()
    {
        var clone = new KEvent();
        clone.Destination.PerformerId = Destination.PerformerId;
        clone.Destination.Uids.UnionWith(Destination.Uids);
        clone.FirstTrace = FirstTrace;
        clone.LastTrace = LastTrace;
        clone.EventId = EventId;
        clone.FromType = FromType;
        clone.Buffer.CopyStateFrom(Buffer);
        return clone;
    }

    internal bool WriteFields(KSerializer serializer)
    {
        // KSerializer.Put(KEvent) contributes the outer user-class tag.
        // KPerformerInfo is itself a user class, so it must go through Put().
        if (!serializer.Put(Destination) ||
            !serializer.Put(FirstTrace) ||
            !serializer.Put(LastTrace) ||
            !serializer.Put(EventId) ||
            !serializer.Put(Buffer))
            return false;

        // FromType intentionally is not serialized: the 2014 Event.cpp did not include it.
        return true;
    }

    internal bool ReadFields(KSerializer serializer)
    {
        if (!serializer.Get(Destination) ||
            !serializer.Get(out long firstTrace) ||
            !serializer.Get(out long lastTrace) ||
            !serializer.Get(out ushort eventId) ||
            !serializer.Get(Buffer))
            return false;

        FirstTrace = firstTrace;
        LastTrace = lastTrace;
        EventId = eventId;
        return true;
    }
}
