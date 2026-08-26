namespace KncWX2Server.Core.Common;

/// <summary>Legacy KSkillData: skill id and skill level.</summary>
public readonly record struct KSkillData(
    short SkillId,
    byte SkillLevel);
