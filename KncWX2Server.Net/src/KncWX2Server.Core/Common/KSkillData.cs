namespace KncWX2Server.Core.Common;

/// <summary>Legacy KSkillData: 16-bit skill id and 8-bit skill level.</summary>
public readonly record struct KSkillData(
    short SkillId,
    byte SkillLevel);
