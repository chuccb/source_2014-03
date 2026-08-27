using KncWX2Server.Core.Common;

static class ServerIdentityRegression
{
    public static void AppliesNativeKServerInfoFields()
    {
        var identity = new ServerIdentity();
        var info = new ServerInfo(
            Uid: 12,
            Name: "Login",
            ServerGroupId: 34,
            ServerClass: ServerClassId.Login,
            IpAddress: "127.0.0.1",
            MasterPort: 5000,
            NcUdpPort: 5001,
            MaxUser: 2000,
            CurrentUser: 42,
            IsOn: true);

        identity.Apply(info);

        Check(identity.Uid == 12, "server UID uses native KServerInfo int width");
        Check(identity.Name == "Login", "server name");
        Check(identity.ServerGroupId == 34, "server group");
        Check(identity.ServerClass == ServerClassId.Login, "server class");
        Check(identity.IpAddress == "127.0.0.1", "server IP");
        Check(identity.MasterPort == 5000 && identity.NcUdpPort == 5001, "server ports");
        Check(identity.MaxUser == 2000 && identity.CurrentUser == 42, "server user counters");
        Check(identity.IsOn, "server online flag");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Regression check failed: {name}");
    }
}
