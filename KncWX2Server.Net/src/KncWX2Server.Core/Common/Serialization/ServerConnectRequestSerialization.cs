namespace KncWX2Server.Core.Common.Serialization;

using KncWX2Server.Core.Common;

/// <summary>Serializer for the exact native KECN_VERIFY_SERVER_CONNECT_REQ field order.</summary>
public static class ServerConnectRequestSerialization
{
    public static bool Write(KSerializer serializer, in ServerConnectRequest value) =>
        serializer.PutWString(value.IpAddress)
        && serializer.Put(value.Port)
        && serializer.Put(value.ServerGroupId);

    public static bool Read(KSerializer serializer, out ServerConnectRequest value)
    {
        value = default;
        if (!serializer.GetWString(out var ipAddress)
            || !serializer.Get(out ushort port)
            || !serializer.Get(out int serverGroupId))
            return false;

        value = new ServerConnectRequest(ipAddress, port, serverGroupId);
        return true;
    }
}
