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

        if (!serializer.Get(out int baseHp)
            || !serializer.Get(out int atkPhysic)
            || !serializer.Get(out int atkMagic)
            || !serializer.Get(out int defPhysic)
            || !serializer.Get(out int defMagic))
            return false;

        value = new(baseHp, atkPhysic, atkMagic, defPhysic, defMagic);
        return true;
    }
}
