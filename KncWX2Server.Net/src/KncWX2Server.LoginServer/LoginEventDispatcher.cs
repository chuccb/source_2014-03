using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Routing;

namespace KncWX2Server.LoginServer;

public enum LoginEventDispatchResult : byte
{
    NotForLoginUser,
    TypedPayloadContractMissing,
}

/// <summary>
/// Explicit boundary for KLoginUser's role-local event dispatch.
/// The native path deserializes a concrete K&lt;event&gt; packet before invoking
/// ON_&lt;event&gt;. Until those packet contracts are ported, this boundary refuses
/// to interpret the payload rather than silently dropping or guessing it.
/// </summary>
public static class LoginEventDispatcher
{
    public static LoginEventDispatchResult Dispatch(KEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return PerformerRouting.GetServerClass(value.Destination.PerformerId) == PerformerRouting.ServerClassLogin
            && PerformerRouting.GetPerformerClass(value.Destination.PerformerId) == PerformerRouting.PerformerUser
            ? LoginEventDispatchResult.TypedPayloadContractMissing
            : LoginEventDispatchResult.NotForLoginUser;
    }
}
