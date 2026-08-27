namespace KncWX2Server.Core.Common.Fsm;

/// <summary>Exact managed equivalent of the native KDefaultFSM transition table.</summary>
public sealed class DefaultFsm : Fsm
{
    public const int StateZeroNoUse = 0;
    public const int StateInit = 1;
    public const int StateLogined = 2;
    public const int StateExit = 3;
    public const int StateSentinel = 4;

    public const int InputAuthenticated = 0;
    public const int InputExit = 1;

    public DefaultFsm() : base(StateInit)
    {
        var init = new FsmState(StateInit, 2);
        init.AddTransition(InputAuthenticated, StateLogined);
        init.AddTransition(InputExit, StateExit);
        AddState(init);

        var logined = new FsmState(StateLogined, 1);
        logined.AddTransition(InputExit, StateExit);
        AddState(logined);

        AddState(new FsmState(StateExit, 0));
    }

    public override string GetStateIdString(int stateId) => stateId switch
    {
        StateZeroNoUse => nameof(StateZeroNoUse),
        StateInit => nameof(StateInit),
        StateLogined => nameof(StateLogined),
        StateExit => nameof(StateExit),
        StateSentinel => nameof(StateSentinel),
        _ => stateId.ToString(),
    };

    public override string GetInputIdString(int inputId) => inputId switch
    {
        InputAuthenticated => nameof(InputAuthenticated),
        InputExit => nameof(InputExit),
        _ => inputId.ToString(),
    };
}
