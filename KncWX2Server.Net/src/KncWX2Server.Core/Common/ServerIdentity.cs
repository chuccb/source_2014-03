namespace KncWX2Server.Core.Common;

/// <summary>Mutable local server identity corresponding to native KBaseServer state.</summary>
public sealed class ServerIdentity
{
    public int Uid { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int ServerGroupId { get; private set; }
    public ServerClassId ServerClass { get; private set; }
    public string IpAddress { get; private set; } = string.Empty;
    public ushort MasterPort { get; private set; }
    public ushort NcUdpPort { get; private set; }
    public int MaxUser { get; private set; }
    public int CurrentUser { get; private set; }
    public bool IsOn { get; private set; }

    public void Apply(ServerInfo serverInfo)
    {
        Uid = serverInfo.Uid;
        Name = serverInfo.Name;
        ServerGroupId = serverInfo.ServerGroupId;
        ServerClass = serverInfo.ServerClass;
        IpAddress = serverInfo.IpAddress;
        MasterPort = serverInfo.MasterPort;
        NcUdpPort = serverInfo.NcUdpPort;
        MaxUser = serverInfo.MaxUser;
        CurrentUser = serverInfo.CurrentUser;
        IsOn = serverInfo.IsOn;
    }
}
