namespace KncWX2Server.Core.Common;

public enum ServerEventRouteResult : byte
{
    Routed = 0,
    LocalTargetMissing = 1,
    RemoteRouteRequired = 2,
    UnsupportedPerformer = 3,
}

public sealed class ServerEventRouter
{
    private readonly uint _currentPerformerId;
    private readonly ServerActorManager _actors;
    private readonly ServerPerformerManager _performers;

    public ServerEventRouter(
        uint currentPerformerId,
        ServerActorManager actors,
        ServerPerformerManager performers)
    {
        _currentPerformerId = currentPerformerId;
        _actors = actors;
        _performers = performers;
    }

    public ServerEventRouteResult Route(KEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var destination = @event.Destination.PerformerId;
        var comparison = PerformerIds.CompareServerLevel(destination, _currentPerformerId);

        if (comparison == 0 || PerformerIds.GetServerClass(destination) == 0)
            return RouteSameLevel(destination, @event);

        return comparison < 0
            ? RouteToLowerServer(@event)
            : ServerEventRouteResult.RemoteRouteRequired;
    }

    private ServerEventRouteResult RouteSameLevel(uint destination, KEvent @event)
    {
        return PerformerIds.GetPerformerClass(destination) switch
        {
            PerformerIds.PcUser => RouteToUsers(@event),
            PerformerIds.PcAccountDb or
            PerformerIds.PcGameDb or
            PerformerIds.PcGameDb2 or
            PerformerIds.PcLogDb or
            PerformerIds.PcLogDb2 => RouteToInternalPerformer(destination, @event),
            _ => ServerEventRouteResult.UnsupportedPerformer,
        };
    }

    private ServerEventRouteResult RouteToUsers(KEvent @event)
    {
        var routed = false;
        foreach (var uid in @event.Destination.Uids)
        {
            var actor = _actors.Get(uid);
            if (actor is null)
                continue;

            actor.QueueingEvent(@event.Clone());
            routed = true;
        }

        return routed
            ? ServerEventRouteResult.Routed
            : @event.IsEmptyTrace
                ? ServerEventRouteResult.LocalTargetMissing
                : ServerEventRouteResult.RemoteRouteRequired;
    }

    private ServerEventRouteResult RouteToInternalPerformer(uint destination, KEvent @event) =>
        _performers.QueueingTo(destination, @event)
            ? ServerEventRouteResult.Routed
            : ServerEventRouteResult.LocalTargetMissing;

    private ServerEventRouteResult RouteToLowerServer(KEvent @event)
    {
        if (@event.IsEmptyTrace)
            return ServerEventRouteResult.LocalTargetMissing;

        var uid = @event.LastSenderUid;
        return _actors.QueueingTo(uid, @event)
            ? ServerEventRouteResult.Routed
            : ServerEventRouteResult.LocalTargetMissing;
    }
}
