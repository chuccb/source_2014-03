using KncWX2Server.Core.Common;

namespace KncWX2Server.Core.Common.Serialization;

/// <summary>Serializer bridge for the legacy five-field KStat payload.</summary>
public static class KStatSerialization
{
    public static bool Put(this KSerializer serializer, in KStat value) =>
        serializer.Put(value.BaseHp)
        && serializer.Put(value.AtkPhysic)
        && serializer.Put(value.AtkMagic)
        && serializer.Put(value.DefPhysic)
        && serializer.Put(value.DefMagic);

    public static bool Get(this KSerializer serializer, out KStat value)
    {
        value = default;

        if (!serializer.Get(out var baseHp)
            || !serializer.Get(out var atkPhysic)
            || !serializer.Get(out var atkMagic)
            || !serializer.Get(out var defPhysic)
            || !serializer.Get(out var defMagic))
            return false;

        value = new(baseHp, atkPhysic, atkMagic, defPhysic, defMagic);
        return true;
    }
}
