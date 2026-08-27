namespace KncWX2Server.Core.Common;

public enum ServerClassId : int
{
    None = 0,
    Center = 1,
    Login = 2,
    Game = 3,
    Channel = 4,
    Simulation = 5,
    Global = 6,
}

/// <summary>Managed domain representation of native KServerInfo.</summary>
public readonly record struct ServerInfo(
    int Uid,
    string Name,
    int ServerGroupId,
    ServerClassId ServerClass,
    string IpAddress,
    ushort MasterPort,
    ushort NcUdpPort,
    int MaxUser,
    int CurrentUser,
    bool IsOn);
