namespace KncWX2Server.Common.Fsm;

public abstract class Fsm(int initialState)
{
    private readonly Dictionary<int, FsmState> _states = [with(capacity: 8)];
    private int _currentState = initialState;

    public int CurrentState
    {
        get => _currentState;
        set => _currentState = value;
    }

    public FsmState? GetState(int stateId) =>
        _states.GetValueOrDefault(stateId);

    public void AddState(FsmState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states.TryAdd(state.StateId, state);
    }

    public bool DeleteState(int stateId) =>
        _states.Remove(stateId);

    public int StateTransition(int input)
    {
        if (_currentState == 0)
            return _currentState;

        if (!_states.TryGetValue(_currentState, out var state))
        {
            _currentState = 0;
            return _currentState;
        }

        _currentState = state.GetOutput(input);
        return _currentState;
    }

    public abstract string GetStateIdString(int stateId);

    public abstract string GetInputIdString(int inputId);
}
