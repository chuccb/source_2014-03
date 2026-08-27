using KncWX2Server.Core.Common;

static class ActorManagerRegression
{
    public static async Task PreservesNativeInsertionOrder()
    {
        var processed = new List<long>(2);
        var manager = new ServerActorManager();
        var processor = (ServerActor actor, KEvent _) =>
        {
            processed.Add(actor.Id);
            return ValueTask.CompletedTask;
        };

        var first = manager.Create(1, processor);
        var second = manager.Create(2, processor);
        await manager.TickAsync();

        first.QueueingEvent(new KEvent());
        second.QueueingEvent(new KEvent());
        await manager.TickAsync();

        if (processed.Count != 2 || processed[0] != 1L || processed[1] != 2L)
            throw new InvalidOperationException("Regression check failed: native actor insertion order");
    }

    public static async Task TickUsesStableSnapshot()
    {
        var processed = new List<long>(3);
        var manager = new ServerActorManager();
        var created = false;

        ValueTask ProcessAsync(ServerActor actor, KEvent _)
        {
            processed.Add(actor.Id);
            if (actor.Id == 1 && !created)
            {
                created = true;
                manager.Create(3, ProcessAsync);
            }

            return ValueTask.CompletedTask;
        }

        var first = manager.Create(1, ProcessAsync);
        manager.Create(2, ProcessAsync);
        await manager.TickAsync();

        first.QueueingEvent(new KEvent());
        await manager.TickAsync();

        if (processed.Count != 1 || processed[0] != 1L || manager.Count != 3)
            throw new InvalidOperationException("Regression check failed: actor added during tick must wait for the next tick");
    }

    public static async Task UpdateUidMatchesNativeDuplicateFailure()
    {
        var manager = new ServerActorManager();
        var first = manager.Create(1, static (_, _) => ValueTask.CompletedTask);
        var second = manager.Create(2, static (_, _) => ValueTask.CompletedTask);
        await manager.TickAsync();

        var firstUid = first.Uid;
        var secondUid = second.Uid;
        Check(firstUid != 0 && secondUid != 0 && firstUid != secondUid, "distinct initial actor UIDs");

        Check(!manager.UpdateUid(firstUid, secondUid), "duplicate target UID reports failure");
        Check(first.Uid == secondUid, "actor UID mutates before duplicate insertion failure");
        Check(manager.Get(firstUid) is null, "old UID mapping is removed on duplicate failure");
        Check(ReferenceEquals(manager.Get(secondUid), second), "existing duplicate target mapping remains authoritative");
    }

    public static async Task GetsMinimumUidLikeNativeMapBegin()
    {
        var manager = new ServerActorManager();
        var first = manager.Create(1, static (_, _) => ValueTask.CompletedTask);
        var second = manager.Create(2, static (_, _) => ValueTask.CompletedTask);
        await manager.TickAsync();

        var minimum = Math.Min(first.Uid, second.Uid);
        Check(manager.GetFirstActorKey() == minimum, "first actor key is minimum UID, not insertion order");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Regression check failed: {name}");
    }
}
