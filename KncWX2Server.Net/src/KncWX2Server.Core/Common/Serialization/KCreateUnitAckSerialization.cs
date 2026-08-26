using KncWX2Server.Core.Common;

namespace KncWX2Server.Core.Common.Serialization;

/// <summary>Explicit wire serializer for the legacy EGS_CREATE_UNIT_ACK payload.</summary>
public static class KCreateUnitAckSerialization
{
    public static bool Put(this KSerializer serializer, KCreateUnitAck value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return serializer.Put(value.Result)
            && serializer.Put(value.UnitInfo)
            && serializer.PutVector(value.SkillSlots, static (KSerializer s, int slot) => s.Put(slot))
            && serializer.PutWString(value.EnableDate);
    }

    public static bool Get(this KSerializer serializer, out KCreateUnitAck value)
    {
        value = new();

        if (!serializer.Get(out int result)
            || !serializer.Get(out KUnitInfo unitInfo)
            || !serializer.GetVector(value.SkillSlots, static (KSerializer s, out int slot) => s.Get(out slot))
            || !serializer.GetWString(out string enableDate))
            return false;

        value.Result = result;
        value.UnitInfo = unitInfo;
        value.EnableDate = enableDate;
        return true;
    }
}
