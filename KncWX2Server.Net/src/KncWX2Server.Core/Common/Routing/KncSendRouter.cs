using KncWX2Server.Core.Common;

namespace KncWX2Server.Core.Common.Routing;

/// <summary>
/// Runtime dependencies required by the legacy KncSend routing algorithm.
/// The concrete Login/Center/Channel/Game layers provide these operations;
/// the Core layer keeps no server-specific Singleton dependency.
/// </summary>
public interface IKncSendRoutingContext
{
    long LocalServerUid { get; }

    void QueueToLocalServer(KEvent value);
    void QueueToDatabase(KEvent value);

    bool TryGetLocalUser(long uid, out IKncSendActor? actor);
    bool TryFindLocalCharacter(long uid);
    void QueueToCharacter(long uid, KEvent value);

    void QueueToRoom(KEvent value);

    void QueueToActor(long uid, KEvent value);
    void QueueToAllActors(KEvent value);

    void SendThroughProxy(long fromUid, KEvent value);
}

public interface IKncSendActor
{
    void Queue(KEvent value);
}

/// <summary>
/// Direct managed translation of KncSend.cpp's server/performer routing rules.
/// </summary>
public sealed class KncSendRouter(IKncSendRoutingContext context)
{
    public const string InvalidDownstreamSourceMessage = "SendPacket should be used for a user-originated downstream send.";

    private readonly IKncSendRoutingContext _context =
        context ?? throw new ArgumentNullException(nameof(context));

    public int CompareServerLevel(uint destinationPerformerId, uint currentPerformerId) =>
        PerformerRouting.CompareServerLevel(destinationPerformerId, currentPerformerId);

    public void Send(uint fromPerformerId, long fromUid, KEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var destinationPerformerId = value.Destination.PerformerId;
        var evaluation = CompareServerLevel(destinationPerformerId, fromPerformerId);
        var destinationServerClass = PerformerRouting.GetServerClass(destinationPerformerId);

        if (evaluation == 0 || destinationServerClass == 0)
        {
            RouteAtSameLevel(fromUid, value);
            return;
        }

        if (evaluation < 0)
        {
            RouteDownstream(fromPerformerId, value);
            return;
        }

        _context.SendThroughProxy(fromUid, value);
    }

    private void RouteAtSameLevel(long fromUid, KEvent value)
    {
        switch (PerformerRouting.GetPerformerClass(value.Destination.PerformerId))
        {
            case PerformerRouting.PerformerServer:
                if (value.Destination.FindUid(_context.LocalServerUid))
                    _context.QueueToLocalServer(value);
                else
                    _context.SendThroughProxy(fromUid, value);
                break;

            case PerformerRouting.PerformerAccountDb:
            case PerformerRouting.PerformerGameDb:
            case PerformerRouting.PerformerGameDb2nd:
            case PerformerRouting.PerformerLogDb:
            case PerformerRouting.PerformerLogDb2nd:
            case PerformerRouting.PerformerSmsDb:
            case PerformerRouting.PerformerNxWebDb:
            case PerformerRouting.PerformerChatLogDb:
            case PerformerRouting.PerformerKogBillingDb:
            case PerformerRouting.PerformerPublisherBillingDb:
            case PerformerRouting.PerformerIdPublisherPcbangAuthDb:
            case PerformerRouting.PerformerJpRelayDb:
            case PerformerRouting.PerformerEventDb:
            case PerformerRouting.PerformerPublisherAuthDb:
            case PerformerRouting.PerformerScriptDb:
                _context.QueueToDatabase(value);
                break;

            case PerformerRouting.PerformerUser:
                foreach (var uid in value.Destination.Uids)
                {
                    if (_context.TryGetLocalUser(uid, out var actor) && actor is not null)
                    {
                        actor.Queue(value.Clone());
                    }
                    else if (!value.IsEmptyTrace)
                    {
                        _context.SendThroughProxy(fromUid, value);
                    }
                }
                break;

            case PerformerRouting.PerformerCharacter:
                foreach (var uid in value.Destination.Uids)
                {
                    if (_context.TryFindLocalCharacter(uid))
                    {
                        _context.QueueToCharacter(uid, value.Clone());
                    }
                    else if (!value.IsEmptyTrace)
                    {
                        _context.SendThroughProxy(fromUid, value);
                    }
                }
                break;

            case PerformerRouting.PerformerRoom:
                if (PerformerRouting.GetServerClass(value.Destination.PerformerId) == PerformerRouting.ServerClassGame)
                    _context.QueueToRoom(value);
                break;

            default:
                // The C++ implementation logs here and otherwise drops the event.
                break;
        }
    }

    private void RouteDownstream(uint fromPerformerId, KEvent value)
    {
        if (PerformerRouting.GetPerformerClass(fromPerformerId) == PerformerRouting.PerformerUser)
            return;

        if (value.IsEmptyTrace)
            return;

        var destinationUid = value.LastSenderUid;
        if (destinationUid == 0)
            _context.QueueToAllActors(value);
        else
            _context.QueueToActor(destinationUid, value);
    }
}
