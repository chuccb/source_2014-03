using KncWX2Server.Core.Common;

namespace KncWX2Server.Core.Common.Serialization;

/// <summary>Wire serialization for the legacy skill-state models.</summary>
public static class KSkillDataSerialization
{
    public static bool Put(this KSerializer serializer, in KSkillData value) =>
        serializer.Put(value.SkillId)
        && serializer.Put(value.SkillLevel);

    public static bool Get(this KSerializer serializer, out KSkillData value)
    {
        value = default;

        if (!serializer.Get(out short skillId)
            || !serializer.Get(out byte skillLevel))
            return false;

        value = new(skillId, skillLevel);
        return true;
    }
}

public static class KUnitSkillDataSerialization
{
    public static bool Put(this KSerializer serializer, KUnitSkillData value)
    {
        ArgumentNullException.ThrowIfNull(value);

        for (var i = 0; i < KUnitSkillData.EquippedSkillSlotCount; i++)
            if (!serializer.Put(value.EquippedSkills[i]))
                return false;

        for (var i = 0; i < KUnitSkillData.EquippedSkillSlotCount; i++)
            if (!serializer.Put(value.EquippedSkillSlotB[i]))
                return false;

        return serializer.PutWString(value.SkillSlotBEndDate)
            && serializer.Put(value.SkillSlotBExpirationState)
            && serializer.PutVector(value.PassiveSkills, static (KSerializer s, KSkillData skill) => s.Put(skill))
            && serializer.PutVector(value.GuildPassiveSkills, static (KSerializer s, KSkillData skill) => s.Put(skill))
            && serializer.PutVector(value.SkillNotes, static (KSerializer s, int note) => s.Put(note))
            && serializer.Put(value.ActiveSkillPagesNumber)
            && serializer.Put(value.AvailableSkillPagesNumber);
    }

    public static bool Get(this KSerializer serializer, out KUnitSkillData value)
    {
        value = new();

        for (var i = 0; i < KUnitSkillData.EquippedSkillSlotCount; i++)
            if (!serializer.Get(out value.EquippedSkills[i]))
            {
                value = default!;
                return false;
            }

        for (var i = 0; i < KUnitSkillData.EquippedSkillSlotCount; i++)
            if (!serializer.Get(out value.EquippedSkillSlotB[i]))
            {
                value = default!;
                return false;
            }

        if (!serializer.GetWString(out var endDate)
            || !serializer.Get(out value.SkillSlotBExpirationState)
            || !serializer.GetVector(value.PassiveSkills, static (KSerializer s, out KSkillData skill) => s.Get(out skill))
            || !serializer.GetVector(value.GuildPassiveSkills, static (KSerializer s, out KSkillData skill) => s.Get(out skill))
            || !serializer.GetVector(value.SkillNotes, static (KSerializer s, out int note) => s.Get(out note))
            || !serializer.Get(out value.ActiveSkillPagesNumber)
            || !serializer.Get(out value.AvailableSkillPagesNumber))
        {
            value = default!;
            return false;
        }

        value.SkillSlotBEndDate = endDate;
        return true;
    }
}
