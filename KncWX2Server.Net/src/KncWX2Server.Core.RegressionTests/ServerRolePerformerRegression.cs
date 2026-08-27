using KncWX2Server.Core;
using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Routing;

static class ServerRolePerformerRegression
{
    public static void MapsRolesToNativeServerPerformers()
    {
        Check(ServerRolePerformer.GetPerformerId(ServerRole.Login) == (uint)PerformerId.LoginServer, "Login server performer");
        Check(ServerRolePerformer.GetPerformerId(ServerRole.Center) == (uint)PerformerId.CnServer, "Center server performer");
        Check(ServerRolePerformer.GetPerformerId(ServerRole.Channel) == (uint)PerformerId.ChannelServer, "Channel server performer");
        Check(ServerRolePerformer.GetPerformerId(ServerRole.Game) == (uint)PerformerId.GsServer, "Game server performer");

        Check(PerformerRouting.GetPerformerClass(ServerRolePerformer.GetPerformerId(ServerRole.Login)) == PerformerRouting.PerformerServer, "role mapping is PC_SERVER");
        Check(PerformerRouting.GetPerformerClass(ServerRolePerformer.GetPerformerId(ServerRole.Game)) == PerformerRouting.PerformerServer, "game mapping is PC_SERVER");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Regression check failed: {name}");
    }
}
