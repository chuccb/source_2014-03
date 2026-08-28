using KncWX2Server.Core.Common;

namespace KncWX2Server.LoginServer;

public enum LoginServerConnectRequestResult : byte
{
    ForwardToLogDb,
    InvalidPeerIp,
}

public readonly record struct LoginServerConnectRequestOutcome(
    LoginServerConnectRequestResult Result,
    ServerConnectRequest Request,
    ServerConnectAck Response);

/// <summary>Ports the source-proven pre-LogDB portion of ELG_VERIFY_SERVER_CONNECT_REQ.</summary>
public static class LoginServerConnectRequestProcessor
{
    public static LoginServerConnectRequestOutcome Process(
        in ServerConnectRequest request,
        string peerIp)
    {
        ArgumentNullException.ThrowIfNull(peerIp);

        if (!string.Equals(request.IpAddress, peerIp, StringComparison.Ordinal))
        {
            // Native NetError_def.h sequence: NET_OK=0 through ERR_VERIFY_02=8.
            return new(
                LoginServerConnectRequestResult.InvalidPeerIp,
                request,
                new ServerConnectAck(
                    Ok: 8,
                    Uid: 0,
                    DbRegisteredServerGroupId: 0,
                    LocalServerGroupId: 0,
                    ServerUid: 0,
                    Name: string.Empty,
                    MaxNum: 0,
                    IpAddress: string.Empty,
                    Port: 0,
                    Version: 0));
        }

        return new(
            LoginServerConnectRequestResult.ForwardToLogDb,
            request,
            default);
    }
}
