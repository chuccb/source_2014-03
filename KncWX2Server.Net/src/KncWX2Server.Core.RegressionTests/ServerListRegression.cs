using KncWX2Server.Core.Common;

static class ServerListRegression
{
    public static void ReplacesAndSnapshotsServerGroups()
    {
        var list = new ServerList();
        var original = new Dictionary<int, IReadOnlyList<ServerInfo>>
        {
            [7] =
            [
                new ServerInfo(11, "Game1", 7, ServerClassId.Game, "127.0.0.1", 9400, 9401, 100, 3, true),
                new ServerInfo(12, "Game2", 7, ServerClassId.Game, "127.0.0.2", 9410, 9411, 200, 4, true),
            ],
            [8] =
            [
                new ServerInfo(21, "Channel1", 8, ServerClassId.Channel, "127.0.0.3", 9500, 9501, 50, 1, true),
            ],
        };

        list.Replace(original);
        original[7] = [new ServerInfo(99, "Changed", 7, ServerClassId.Game, "127.0.0.9", 9990, 9991, 1, 0, false)];

        var snapshot = list.Snapshot();
        Check(snapshot.Count == 2, "server group count");
        Check(snapshot[7].Count == 2, "server group replacement is copied");
        Check(snapshot[7][0].Uid == 11 && snapshot[7][1].Uid == 12, "server order is preserved");
        Check(snapshot[8][0].ServerClass == ServerClassId.Channel, "server class preserved");

        var replacement = new Dictionary<int, IReadOnlyList<ServerInfo>>
        {
            [7] = [new ServerInfo(31, "Game3", 7, ServerClassId.Game, "127.0.0.4", 9600, 9601, 300, 5, true)],
        };
        list.Replace(replacement);

        snapshot = list.Snapshot();
        Check(snapshot.Count == 1, "replacement discards previous groups");
        Check(snapshot[7][0].Uid == 31, "replacement value is visible");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Regression check failed: {name}");
    }
}
