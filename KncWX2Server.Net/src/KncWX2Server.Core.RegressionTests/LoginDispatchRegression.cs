using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Routing;
using KncWX2Server.LoginServer;

static class LoginDispatchRegression
{
    public static void RejectsUnprovenLoginPayloads()
    {
        var loginUserEvent = new KEvent();
        loginUserEvent.SetData(PerformerRouting.ServerClassLogin | PerformerRouting.PerformerUser, [], 0xFFFF);

        var result = LoginEventDispatcher.Dispatch(loginUserEvent);
        Check(result == LoginEventDispatchResult.TypedPayloadContractMissing,
            "Login dispatch must remain blocked until a typed payload contract is proven");

        var nonLoginEvent = new KEvent();
        nonLoginEvent.SetData(PerformerRouting.ServerClassGame | PerformerRouting.PerformerUser, [], 0xFFFF);

        result = LoginEventDispatcher.Dispatch(nonLoginEvent);
        Check(result == LoginEventDispatchResult.NotForLoginUser,
            "Non-Login destination must not enter Login role dispatch");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
