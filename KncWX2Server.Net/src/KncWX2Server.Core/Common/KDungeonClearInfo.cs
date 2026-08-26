namespace KncWX2Server.Core.Common;

/// <summary>Legacy dungeon-clear state embedded in KUnitInfo.</summary>
public readonly record struct KDungeonClearInfo(
    int DungeonId,
    int MaxScore,
    sbyte MaxTotalRank,
    string ClearTime,
    bool IsNew);
