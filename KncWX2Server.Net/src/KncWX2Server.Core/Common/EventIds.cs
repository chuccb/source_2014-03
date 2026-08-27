namespace KncWX2Server.Core.Common;

/// <summary>System event ids verified from the legacy EventID_System.h prefix.</summary>
public static class SystemEventIds
{
    public const ushort HeartBeat = 0;
    public const ushort AcceptConnectionNot = 1;
    public const ushort ConnectionLostNot = 2;
    public const ushort UdpPortNot = 3;
    public const ushort DisableHeartbeatCheckReq = 4;
    public const ushort LogNot = 5;
    public const ushort ReserveDestroy = 6;
    public const ushort ToolGetCcuInfoReq = 7;
    public const ushort ToolGetCcuInfoAck = 8;
    public const ushort ToolCheckLoginReq = 9;
    public const ushort ToolCheckLoginAck = 10;
    public const ushort ToolServerListReq = 11;
    public const ushort ToolServerListAck = 12;
    public const ushort CheckSequenceCountNot = 13;
    public const ushort UdpRelayServerCheckPacketNot = 14;
    public const ushort ConnectRelayAck = 15;
    public const ushort CheckDdosGuardReq = 16;
    public const ushort CheckDdosGuardAck = 17;
    public const ushort ChConnectionLostForDdosGuardNot = 18;
    public const ushort GsConnectionLostForDdosGuardNot = 19;
}
