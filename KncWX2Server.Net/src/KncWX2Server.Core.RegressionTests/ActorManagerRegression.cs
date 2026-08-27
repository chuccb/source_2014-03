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

        if (!processed.SequenceEqual([1L, 2L]))
            throw new InvalidOperationException("Regression check failed: native actor insertion order");
    }
}
