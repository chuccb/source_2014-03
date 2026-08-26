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
        {
            if (!serializer.Get(out KSkillData skill))
                return false;

            value.EquippedSkills[i] = skill;
        }

        for (var i = 0; i < KUnitSkillData.EquippedSkillSlotCount; i++)
        {
            if (!serializer.Get(out KSkillData skill))
                return false;

            value.EquippedSkillSlotB[i] = skill;
        }

        if (!serializer.GetWString(out var skillSlotBEndDate)
            || !serializer.Get(out sbyte skillSlotBExpirationState)
            || !serializer.GetVector(value.PassiveSkills, static (KSerializer s, out KSkillData skill) => s.Get(out skill))
            || !serializer.GetVector(value.GuildPassiveSkills, static (KSerializer s, out KSkillData skill) => s.Get(out skill))
            || !serializer.GetVector(value.SkillNotes, static (KSerializer s, out int note) => s.Get(out note))
            || !serializer.Get(out byte activeSkillPagesNumber)
            || !serializer.Get(out byte availableSkillPagesNumber))
            return false;

        value.SkillSlotBEndDate = skillSlotBEndDate;
        value.SkillSlotBExpirationState = skillSlotBExpirationState;
        value.ActiveSkillPagesNumber = activeSkillPagesNumber;
        value.AvailableSkillPagesNumber = availableSkillPagesNumber;
        return true;
    }
}
