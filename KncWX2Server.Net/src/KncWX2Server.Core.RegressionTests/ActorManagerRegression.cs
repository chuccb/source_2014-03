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
        Func<ServerActor, KEvent, ValueTask>? processor = null;
        processor = (actor, _) =>
        {
            processed.Add(actor.Id);
            if (actor.Id == 1 && !created)
            {
                created = true;
                manager.Create(3, processor);
            }

            return ValueTask.CompletedTask;
        };

        var first = manager.Create(1, processor);
        manager.Create(2, processor);
        await manager.TickAsync();

        first.QueueingEvent(new KEvent());
        await manager.TickAsync();

        if (processed.Count != 1 || processed[0] != 1L || manager.Count != 3)
            throw new InvalidOperationException("Regression check failed: actor added during tick must wait for the next tick");
    }
}
