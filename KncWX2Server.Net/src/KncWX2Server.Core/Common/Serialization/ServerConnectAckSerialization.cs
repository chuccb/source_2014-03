namespace KncWX2Server.Core.Common.Serialization;

using KncWX2Server.Core.Common;

/// <summary>Field-order-preserving serializer for ECN_VERIFY_SERVER_CONNECT_ACK.</summary>
public static class ServerConnectAckSerialization
{
    public static bool Write(KSerializer serializer, in ServerConnectAck value)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        return serializer.Put(value.Ok)
            && serializer.Put(value.Uid)
            && serializer.Put(value.DbRegisteredServerGroupId)
            && serializer.Put(value.LocalServerGroupId)
            && serializer.Put(value.ServerUid)
            && serializer.PutWString(value.Name)
            && serializer.Put(value.MaxNum)
            && serializer.PutWString(value.IpAddress)
            && serializer.Put(value.Port)
            && serializer.Put(value.Version);
    }

    public static bool Read(KSerializer serializer, out ServerConnectAck value)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        value = default;
        if (!serializer.Get(out int ok)
            || !serializer.Get(out long uid)
            || !serializer.Get(out int dbRegisteredServerGroupId)
            || !serializer.Get(out int localServerGroupId)
            || !serializer.Get(out long serverUid)
            || !serializer.GetWString(out var name)
            || !serializer.Get(out short maxNum)
            || !serializer.GetWString(out var ipAddress)
            || !serializer.Get(out ushort port)
            || !serializer.Get(out int version))
            return false;

        value = new ServerConnectAck(
            ok,
            uid,
            dbRegisteredServerGroupId,
            localServerGroupId,
            serverUid,
            name,
            maxNum,
            ipAddress,
            port,
            version);
        return true;
    }
}
