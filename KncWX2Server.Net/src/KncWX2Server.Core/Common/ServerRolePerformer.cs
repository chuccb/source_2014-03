using KncWX2Server.Core.Common.Routing;

namespace KncWX2Server.Core.Common;

/// <summary>Maps each migrated server role to its native PC_SERVER performer identity.</summary>
public static class ServerRolePerformer
{
    public static uint GetPerformerId(ServerRole role) =>
        role switch
        {
            ServerRole.Login => (uint)PerformerId.LoginServer,
            ServerRole.Center => (uint)PerformerId.CnServer,
            ServerRole.Channel => (uint)PerformerId.ChannelServer,
            ServerRole.Game => (uint)PerformerId.GsServer,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported server role."),
        };
}
