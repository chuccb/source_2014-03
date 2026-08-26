namespace KncWX2Server.Core.Common;

/// <summary>Legacy unit skill-state container used by KUnitInfo.</summary>
public sealed class KUnitSkillData
{
    public const int EquippedSkillSlotCount = 4;

    public KSkillData[] EquippedSkills { get; } = new KSkillData[EquippedSkillSlotCount];
    public KSkillData[] EquippedSkillSlotB { get; } = new KSkillData[EquippedSkillSlotCount];

    public string SkillSlotBEndDate { get; set; } = string.Empty;
    public byte SkillSlotBExpirationState { get; set; }

    public List<KSkillData> PassiveSkills { get; } = [];
    public List<KSkillData> GuildPassiveSkills { get; } = [];
    public List<int> SkillNotes { get; } = [];

    public byte ActiveSkillPagesNumber { get; set; } = 1;
    public byte AvailableSkillPagesNumber { get; set; } = 1;
}
