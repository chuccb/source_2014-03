namespace KncWX2Server.Core.Common;

/// <summary>Legacy title/TC clear state embedded in KUnitInfo.</summary>
public readonly record struct KTCClearInfo(
    int TcId,
    string ClearTime,
    bool IsNew);
