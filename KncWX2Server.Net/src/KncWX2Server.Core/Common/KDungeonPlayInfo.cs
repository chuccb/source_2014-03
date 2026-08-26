namespace KncWX2Server.Core.Common;

/// <summary>Legacy limited-dungeon play state embedded in KUnitInfo.</summary>
public readonly record struct KDungeonPlayInfo(
    int DungeonId,
    int PlayTimes,
    int ClearTimes,
    bool IsNew);
