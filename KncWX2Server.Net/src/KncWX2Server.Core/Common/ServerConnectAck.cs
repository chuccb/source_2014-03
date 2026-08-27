namespace KncWX2Server.Core.Common;

/// <summary>Effective SERV_INTEGRATION layout of native ECN_VERIFY_SERVER_CONNECT_ACK.</summary>
public readonly record struct ServerConnectAck(
    int Ok,
    long Uid,
    int DbRegisteredServerGroupId,
    int LocalServerGroupId,
    long ServerUid,
    string Name,
    short MaxNum,
    string IpAddress,
    ushort Port,
    int Version);
