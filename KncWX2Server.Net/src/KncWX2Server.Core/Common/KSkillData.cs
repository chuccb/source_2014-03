namespace KncWX2Server.Core.Common;

/// <summary>Legacy KSkillData: 32-bit skill id and 8-bit skill level.</summary>
public readonly record struct KSkillData(
    int SkillId,
    byte SkillLevel);
