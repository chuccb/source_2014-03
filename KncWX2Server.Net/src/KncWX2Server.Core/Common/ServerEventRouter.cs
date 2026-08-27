using KncWX2Server.Core.Common.Routing;

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
        var comparison = PerformerRouting.CompareServerLevel(destination, _currentPerformerId);

        if (comparison == 0 || PerformerRouting.GetServerClass(destination) == 0)
            return RouteSameLevel(destination, @event);

        return comparison < 0
            ? RouteToLowerServer(@event)
            : ServerEventRouteResult.RemoteRouteRequired;
    }

    private ServerEventRouteResult RouteSameLevel(uint destination, KEvent @event) =>
        PerformerRouting.GetPerformerClass(destination) switch
        {
            PerformerRouting.PerformerUser => RouteToUsers(@event),
            PerformerRouting.PerformerServer or
            PerformerRouting.PerformerAccountDb or
            PerformerRouting.PerformerGameDb or
            PerformerRouting.PerformerGameDb2nd or
            PerformerRouting.PerformerLogDb or
            PerformerRouting.PerformerLogDb2nd => RouteToPerformer(destination, @event),
            _ => ServerEventRouteResult.UnsupportedPerformer,
        };

    private ServerEventRouteResult RouteToUsers(KEvent @event)
    {
        var remoteRequired = false;
        var routed = false;

        foreach (var uid in @event.Destination.Uids)
        {
            if (_actors.Get(uid) is { } actor)
            {
                actor.QueueingEvent(@event.Clone());
                routed = true;
            }
            else if (!@event.IsEmptyTrace)
            {
                remoteRequired = true;
            }
        }

        if (remoteRequired)
            return ServerEventRouteResult.RemoteRouteRequired;

        return routed
            ? ServerEventRouteResult.Routed
            : ServerEventRouteResult.LocalTargetMissing;
    }

    private ServerEventRouteResult RouteToPerformer(uint destination, KEvent @event) =>
        _performers.QueueingTo(destination, @event)
            ? ServerEventRouteResult.Routed
            : ServerEventRouteResult.LocalTargetMissing;

    private ServerEventRouteResult RouteToLowerServer(KEvent @event)
    {
        if (@event.IsEmptyTrace)
            return ServerEventRouteResult.LocalTargetMissing;

        var senderUid = @event.LastSenderUid;
        if (senderUid == 0)
        {
            _actors.QueueingToAll(@event);
            return ServerEventRouteResult.Routed;
        }

        return _actors.QueueingTo(senderUid, @event)
            ? ServerEventRouteResult.Routed
            : ServerEventRouteResult.LocalTargetMissing;
    }
}
