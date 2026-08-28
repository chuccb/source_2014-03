namespace KncWX2Server.Core.Common;

/// <summary>Exact SERV_INTEGRATION layout of native ECN_VERIFY_SERVER_CONNECT_REQ.</summary>
public readonly record struct ServerConnectRequest(
    string IpAddress,
    ushort Port,
    int ServerGroupId);
