namespace KncWX2Server.Core.Common;

/// <summary>Legacy battle-field last-position state embedded in KUnitInfo.</summary>
public readonly record struct KLastPositionInfo(
    int MapId,
    byte LastTouchLineIndex,
    ushort LastPosValue,
    bool IgnoreLastTouch);
