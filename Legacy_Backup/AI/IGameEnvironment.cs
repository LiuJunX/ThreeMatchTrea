using Match3.Core.Models.Gameplay;

namespace Match3.Core.AI;

public interface IGameEnvironment<TState, TAction>
{
    /// <summary>
    /// Resets the environment to an initial state and returns the initial observation.
    /// </summary>
    /// <param name="seed">Optional seed to ensure deterministic initial state.</param>
    TState Reset(int? seed = null);

    /// <summary>
    /// Run one timestep of the environment's dynamics.
    /// </summary>
    /// <param name="action">The action to take.</param>
    /// <returns>The result of the step (observation, reward, done, info).</returns>
    StepResult<TState> Step(TAction action);

    /// <summary>
    /// Returns the current state/observation.
    /// </summary>
    TState GetState();
}
