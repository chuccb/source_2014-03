using KncWX2Server.Core;
using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Routing;

static class ServerRolePerformerRegression
{
    public static async Task MapsRolesToNativeServerPerformers()
    {
        Check(ServerRolePerformer.GetPerformerId(ServerRole.Login) == (uint)PerformerId.LoginServer, "Login server performer");
        Check(ServerRolePerformer.GetPerformerId(ServerRole.Center) == (uint)PerformerId.CnServer, "Center server performer");
        Check(ServerRolePerformer.GetPerformerId(ServerRole.Channel) == (uint)PerformerId.ChannelServer, "Channel server performer");
        Check(ServerRolePerformer.GetPerformerId(ServerRole.Game) == (uint)PerformerId.GsServer, "Game server performer");

        Check(PerformerRouting.GetPerformerClass(ServerRolePerformer.GetPerformerId(ServerRole.Login)) == PerformerRouting.PerformerServer, "Login mapping is PC_SERVER");
        Check(PerformerRouting.GetPerformerClass(ServerRolePerformer.GetPerformerId(ServerRole.Game)) == PerformerRouting.PerformerServer, "Game mapping is PC_SERVER");

        var manager = new ServerPerformerManager();
        var received = 0;
        var performer = manager.RegisterRole(
            ServerRole.Login,
            (_, _) =>
            {
                received++;
                return ValueTask.CompletedTask;
            });

        if (performer is not { } registered)
            throw new InvalidOperationException("Regression check failed: register Login server role");

        Check(registered.PerformerId == (uint)PerformerId.LoginServer, "registered Login performer id");

        var @event = new KEvent();
        @event.SetData(registered.PerformerId, ReadOnlySpan<long>.Empty, 301);
        Check(manager.QueueingTo(registered.PerformerId, @event), "queue Login server event");
        await manager.TickAsync();
        Check(received == 1, "registered Login server event processed");

        Check(manager.RegisterRole(ServerRole.Login, static (_, _) => ValueTask.CompletedTask) is null, "duplicate Login role rejected");
        Check(manager.Remove(registered.PerformerId), "remove registered Login performer");
        Check(manager.Get(registered.PerformerId) is null, "removed Login performer unavailable");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Regression check failed: {name}");
    }
}
