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
        return Uids.Add(uid);
    }

    public int UidListSize => Uids.Count;

    public long GetFirstUid() => Uids.Count == 0 ? -1 : Uids.Min;

    internal bool WriteFields(KSerializer serializer) =>
        serializer.Put(PerformerId)
        && serializer.Put(Uids, static (KSerializer ser, long uid) => ser.Put(uid));

    internal bool ReadFields(KSerializer serializer)
    {
        if (!serializer.Get(out uint performerId)
            || !serializer.Get(Uids, static (KSerializer ser, out long uid) => ser.Get(out uid)))
            return false;

        PerformerId = performerId;
        return true;
    }
}
