using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Fsm;

namespace KncWX2Server.LoginServer;

public enum LoginServerConnectAckResult : byte
{
    Authenticated,
    RemoteFailure,
    DuplicateUid,
    InvalidState,
    FsmNotConfigured,
    UidUpdateFailed,
}

public readonly record struct LoginServerConnectAckOutcome(
    LoginServerConnectAckResult Result,
    bool DestroyActor,
    ServerConnectAck Response);

/// <summary>Ports the source-proven DBE_VERIFY_SERVER_CONNECT_ACK state transition.</summary>
public static class LoginServerConnectAckProcessor
{
    public static LoginServerConnectAckOutcome Process(
        ServerActor actor,
        ServerActorManager actors,
        in ServerConnectAck packet)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(actors);

        // Native VERIFY_STATE requires KDefaultFSM::S_INIT before doing any work.
        if (actor.Fsm is null)
            return new(LoginServerConnectAckResult.FsmNotConfigured, true, packet);

        if (actor.Fsm.CurrentState != DefaultFsm.StateInit)
            return new(LoginServerConnectAckResult.InvalidState, true, packet);

        // Native sends ELG_VERIFY_SERVER_CONNECT_ACK before the later duplicate-UID check.
        if (packet.Ok != 0)
            return new(LoginServerConnectAckResult.RemoteFailure, true, packet);

        if (actors.Get(packet.Uid) is not null)
            return new(LoginServerConnectAckResult.DuplicateUid, true, packet);

        actor.Name = packet.Name;
        if (!actors.UpdateUid(actor.Uid, packet.Uid))
            return new(LoginServerConnectAckResult.UidUpdateFailed, true, packet);

        actor.Fsm.StateTransition(DefaultFsm.InputAuthenticated);
        return new(LoginServerConnectAckResult.Authenticated, false, packet);
    }
}
