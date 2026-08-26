namespace KncWX2Server.Core.Common.Fsm;

public sealed class FsmState
{
    private readonly int[] _inputs;
    private readonly int[] _outputStates;

    public FsmState(int stateId, int transitionCapacity)
    {
        StateId = stateId;
        var capacity = transitionCapacity == 0 ? 1 : transitionCapacity;
        _inputs = new int[capacity];
        _outputStates = new int[capacity];
    }

    public int StateId { get; }

    public void AddTransition(int input, int outputStateId)
    {
        for (var index = 0; index < _outputStates.Length; index++)
        {
            if (_outputStates[index] != 0)
                continue;

            _outputStates[index] = outputStateId;
            _inputs[index] = input;
            return;
        }
    }

    public void DeleteTransition(int outputStateId)
    {
        var index = Array.IndexOf(_outputStates, outputStateId);
        if (index < 0)
            return;

        _inputs[index] = 0;
        _outputStates[index] = 0;

        while (index < _outputStates.Length - 1 && _outputStates[index + 1] != 0)
        {
            _inputs[index] = _inputs[index + 1];
            _outputStates[index] = _outputStates[index + 1];
            index++;
        }

        _inputs[index] = 0;
        _outputStates[index] = 0;
    }

    public int GetOutput(int input)
    {
        for (var index = 0; index < _outputStates.Length; index++)
        {
            if (_outputStates[index] == 0)
                break;

            if (_inputs[index] == input)
                return _outputStates[index];
        }

        return StateId;
    }

    public bool CheckState(params int[] stateIds)
    {
        ArgumentNullException.ThrowIfNull(stateIds);
        return Array.IndexOf(stateIds, StateId) >= 0;
    }
}
