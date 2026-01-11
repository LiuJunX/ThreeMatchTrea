using System.Collections.Generic;

namespace Match3.Core.Models.Gameplay;

public readonly struct StepResult<TState>
{
    public readonly TState State;
    public readonly double Reward;
    public readonly bool IsDone;
    public readonly Dictionary<string, object> Info;

    public StepResult(TState state, double reward, bool isDone, Dictionary<string, object>? info = null)
    {
        State = state;
        Reward = reward;
        IsDone = isDone;
        Info = info ?? new Dictionary<string, object>();
    }
}
