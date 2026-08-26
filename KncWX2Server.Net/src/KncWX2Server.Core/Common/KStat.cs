namespace KncWX2Server.Core.Common;

/// <summary>
/// Legacy KStat wire value: base HP, physical/magic attack, physical/magic defense.
/// </summary>
public readonly record struct KStat(
    int BaseHp,
    int AtkPhysic,
    int AtkMagic,
    int DefPhysic,
    int DefMagic);
