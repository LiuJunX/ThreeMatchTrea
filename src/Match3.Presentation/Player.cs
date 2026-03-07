using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Presentation;

/// <summary>
/// Static comparers to avoid Lambda closure allocations in hot paths.
/// </summary>
internal static class RenderCommandComparers
{
    /// <summary>
    /// Compares RenderCommands by StartTime, then Priority.
    /// Thread-safe singleton instance.
    /// </summary>
    public static readonly Comparison<RenderCommand> ByStartTimeThenPriority = static (a, b) =>
    {
        int cmp = a.StartTime.CompareTo(b.StartTime);
        return cmp != 0 ? cmp : a.Priority.CompareTo(b.Priority);
    };

    /// <summary>
    /// IComparer wrapper for List.Sort range overload.
    /// </summary>
    public static readonly IComparer<RenderCommand> ComparerByStartTimeThenPriority =
        Comparer<RenderCommand>.Create(ByStartTimeThenPriority);
}

/// <summary>
/// Plays render command sequences, updating VisualState.
/// Supports seeking and replay functionality.
/// </summary>
public sealed class Player
{
    private readonly VisualState _visualState;
    private readonly List<RenderCommand> _commands = new();
    private readonly List<ActiveCommand> _activeCommands = new();
    private float _currentTime;
    private int _nextCommandIndex;

    /// <summary>
    /// Visual state being updated by this player.
    /// </summary>
    public VisualState VisualState => _visualState;

    /// <summary>
    /// Current playback time.
    /// </summary>
    public float CurrentTime => _currentTime;

    /// <summary>
    /// Whether there are active animations.
    /// </summary>
    public bool HasActiveAnimations => _activeCommands.Count > 0 || _nextCommandIndex < _commands.Count;

    /// <summary>
    /// Diagnostic string showing what's blocking HasActiveAnimations.
    /// </summary>
    public string GetAnimationDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"time={_currentTime:F3} active={_activeCommands.Count} pending={_commands.Count - _nextCommandIndex}/{_commands.Count}");

        // Show active commands with remaining time
        for (int i = 0; i < _activeCommands.Count && i < 3; i++)
        {
            var ac = _activeCommands[i];
            float remaining = ac.Command.EndTime - _currentTime;
            sb.Append($" | [{ac.Command.GetType().Name} end={ac.Command.EndTime:F3} rem={remaining:F3}]");
        }

        // Show next pending command start time
        if (_nextCommandIndex < _commands.Count)
        {
            var next = _commands[_nextCommandIndex];
            float wait = next.StartTime - _currentTime;
            sb.Append($" | next=[{next.GetType().Name} start={next.StartTime:F3} wait={wait:F3}]");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a new player with the specified visual state.
    /// </summary>
    public Player(VisualState visualState)
    {
        _visualState = visualState ?? throw new ArgumentNullException(nameof(visualState));
    }

    /// <summary>
    /// Creates a new player with a new visual state.
    /// </summary>
    public Player() : this(new VisualState())
    {
    }

    /// <summary>
    /// Load a new command sequence, replacing existing commands.
    /// Resets playback to time 0.
    /// </summary>
    public void Load(IReadOnlyList<RenderCommand> commands)
    {
        _commands.Clear();
        _commands.AddRange(commands);
        _commands.Sort(RenderCommandComparers.ByStartTimeThenPriority);

        _activeCommands.Clear();
        _currentTime = 0;
        _nextCommandIndex = 0;
    }

    /// <summary>
    /// Append commands to existing sequence.
    /// </summary>
    public void Append(IReadOnlyList<RenderCommand> commands)
    {
        int insertStart = _commands.Count;
        _commands.AddRange(commands);

        if (commands.Count == 0) return;

        // Check if new commands interleave with existing ones
        bool needsFullSort = false;
        if (insertStart > 0)
        {
            float lastExistingStart = _commands[insertStart - 1].StartTime;
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].StartTime < lastExistingStart)
                {
                    needsFullSort = true;
                    break;
                }
            }
        }

        if (needsFullSort)
        {
            _commands.Sort(RenderCommandComparers.ByStartTimeThenPriority);
        }
        else
        {
            // New commands are all after existing ones - only sort the new portion
            _commands.Sort(insertStart, commands.Count, RenderCommandComparers.ComparerByStartTimeThenPriority);
        }

        // Recalculate next command index via binary search (O(log n) instead of O(n)).
        _nextCommandIndex = BinarySearchNextCommand(_currentTime);
    }

    /// <summary>
    /// Advance the player by delta time, executing commands and updating visual state.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0) return;

        float targetTime = _currentTime + deltaTime;

        // Complete finished commands FIRST (clears IsBeingAnimated)
        // This must happen before starting new commands to avoid overwriting
        // the IsBeingAnimated flag set by new commands on the same tiles
        CompleteFinishedCommands(targetTime);

        // Fast-forward: if no active commands but pending commands exist with
        // future start times, skip ahead to close the gap. This prevents idle
        // waiting when Choreographer's cascade delays accumulate faster than
        // real-time playback advances.
        if (_activeCommands.Count == 0 && _nextCommandIndex < _commands.Count)
        {
            float nextStart = _commands[_nextCommandIndex].StartTime;
            if (nextStart > targetTime)
            {
                targetTime = nextStart;
            }
        }

        // Start new commands (sets IsBeingAnimated)
        while (_nextCommandIndex < _commands.Count && _commands[_nextCommandIndex].StartTime <= targetTime)
        {
            var cmd = _commands[_nextCommandIndex];
            StartCommand(cmd);
            _nextCommandIndex++;
        }

        // Complete any commands that started AND finished within this tick
        // (handles large deltaTime that skips entire animations)
        CompleteFinishedCommands(targetTime);

        // Update active commands (interpolate positions)
        for (int i = 0; i < _activeCommands.Count; i++)
        {
            UpdateCommand(_activeCommands[i], targetTime);
        }

        _currentTime = targetTime;

        // When all animations are done and no pending commands remain,
        // reset time to 0 and trim the command list.
        // This prevents float precision degradation from unbounded time accumulation
        // and avoids O(n) scans over completed commands in Append().
        if (_activeCommands.Count == 0 && _nextCommandIndex >= _commands.Count)
        {
            _commands.Clear();
            _nextCommandIndex = 0;
            _currentTime = 0;
        }
    }

    private void CompleteFinishedCommands(float targetTime)
    {
        for (int i = _activeCommands.Count - 1; i >= 0; i--)
        {
            var active = _activeCommands[i];

            if (targetTime >= active.Command.EndTime)
            {
                CompleteCommand(active);
                _activeCommands.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Seek to a specific time. For replay functionality.
    /// </summary>
    public void SeekTo(float targetTime)
    {
        if (targetTime < _currentTime)
        {
            // Reset and replay from start
            _activeCommands.Clear();
            _currentTime = 0;
            _nextCommandIndex = 0;
        }

        // Execute until we reach target time
        while (_currentTime < targetTime && (_activeCommands.Count > 0 || _nextCommandIndex < _commands.Count))
        {
            float dt = Math.Min(0.016f, targetTime - _currentTime);
            Tick(dt);
        }
    }

    /// <summary>
    /// Skip to the end of all commands.
    /// </summary>
    public void SkipToEnd()
    {
        // Find the latest end time
        float latestEnd = _currentTime;
        foreach (var cmd in _commands)
        {
            if (cmd.EndTime > latestEnd)
                latestEnd = cmd.EndTime;
        }

        SeekTo(latestEnd + 0.001f);
    }

    /// <summary>
    /// Synchronize visual state from game state.
    /// Call this to reset visual state to match simulation.
    /// </summary>
    public void SyncFromGameState(in GameState state)
    {
        _visualState.SyncFromGameState(in state);
        _commands.Clear();
        _activeCommands.Clear();
        _currentTime = 0;
        _nextCommandIndex = 0;
    }

    /// <summary>
    /// Clear all commands and active animations.
    /// </summary>
    public void Clear()
    {
        _commands.Clear();
        _activeCommands.Clear();
        _nextCommandIndex = 0;
    }

    #region Command Execution

    private void StartCommand(RenderCommand cmd)
    {
        switch (cmd)
        {
            case SpawnTileCommand spawn:
                _visualState.AddTile(spawn.TileId, spawn.Type, spawn.Bomb, spawn.GridPos, spawn.SpawnPos);
                break;

            case SpawnProjectileCommand spawnProj:
                _visualState.AddProjectile(spawnProj.ProjectileId, spawnProj.Origin);
                break;

            case ShowEffectCommand effect:
                _visualState.AddEffect(effect.EffectType, effect.Position, effect.Duration);
                break;

            case ShowMatchHighlightCommand highlight:
                foreach (var pos in highlight.Positions)
                {
                    _visualState.AddEffect("match_highlight", new Vector2(pos.X, pos.Y), highlight.Duration);
                }
                break;

            case UpdateTileBombCommand updateBomb:
                var tile = _visualState.GetTile(updateBomb.TileId);
                if (tile != null)
                {
                    tile.BombType = updateBomb.BombType;
                }
                break;

            case UpdateTileTypeCommand updateType:
                var tileToUpdate = _visualState.GetTile(updateType.TileId);
                if (tileToUpdate != null)
                {
                    tileToUpdate.TileType = updateType.TileType;
                }
                break;
        }

        // Mark tiles as being animated for position-affecting commands
        MarkTilesAnimating(cmd, true);

        // Add to active commands if duration > 0, except pure visual effects
        // which are managed by VisualState.UpdateEffects and should not block
        // HasActiveAnimations (and therefore should not block player input).
        bool isFireAndForget = cmd is ShowEffectCommand or ShowMatchHighlightCommand;
        if (cmd.Duration > 0 && !isFireAndForget)
        {
            _activeCommands.Add(new ActiveCommand(cmd, _currentTime));
        }
        else
        {
            // Instant commands - execute immediately
            ExecuteInstantCommand(cmd);
            // Clear animation flag for instant commands
            MarkTilesAnimating(cmd, false);
        }
    }

    private void ExecuteInstantCommand(RenderCommand cmd)
    {
        switch (cmd)
        {
            case RemoveTileCommand remove:
                _visualState.RemoveTile(remove.TileId);
                break;

            case RemoveProjectileCommand removeProj:
                _visualState.RemoveProjectile(removeProj.ProjectileId);
                break;
        }
    }

    private void UpdateCommand(ActiveCommand active, float currentTime)
    {
        var cmd = active.Command;
        float t = cmd.Duration > 0
            ? Math.Clamp((currentTime - cmd.StartTime) / cmd.Duration, 0f, 1f)
            : 1f;

        switch (cmd)
        {
            case MoveTileCommand move:
                float easedT = ApplyEasing(t, move.Easing);
                var pos = Vector2.Lerp(move.From, move.To, easedT);
                _visualState.SetTilePosition(move.TileId, pos);
                break;

            case SwapTilesCommand swap:
                float swapT = ApplyEasing(t, swap.Easing);
                var posA = Vector2.Lerp(swap.PosA, swap.PosB, swapT);
                var posB = Vector2.Lerp(swap.PosB, swap.PosA, swapT);
                _visualState.SetTilePosition(swap.TileAId, posA);
                _visualState.SetTilePosition(swap.TileBId, posB);
                break;

            case DestroyTileCommand destroy:
                // Fade out and scale down
                float alpha = 1f - t;
                float scale = 1f - t;
                _visualState.SetTileAlpha(destroy.TileId, alpha);
                _visualState.SetTileScale(destroy.TileId, new Vector2(scale, scale));
                break;

            case MoveProjectileCommand moveProj:
                var projPos = Vector2.Lerp(moveProj.From, moveProj.To, t);
                _visualState.SetProjectilePosition(moveProj.ProjectileId, projPos);
                break;

            case SpawnProjectileCommand spawnProj:
                // Takeoff animation - arc upward
                float arcProgress = (float)Math.Sin(t * Math.PI);
                var takeoffPos = spawnProj.Origin + new Vector2(0, -spawnProj.ArcHeight * arcProgress);
                _visualState.SetProjectilePosition(spawnProj.ProjectileId, takeoffPos);
                break;

            case ImpactProjectileCommand impact:
                // Fade out projectile
                _visualState.SetProjectileVisible(impact.ProjectileId, t < 0.5f);
                break;
        }
    }

    private void CompleteCommand(ActiveCommand active)
    {
        var cmd = active.Command;

        switch (cmd)
        {
            case MoveTileCommand move:
                _visualState.SetTilePosition(move.TileId, move.To);
                break;

            case SwapTilesCommand swap:
                _visualState.SetTilePosition(swap.TileAId, swap.PosB);
                _visualState.SetTilePosition(swap.TileBId, swap.PosA);
                break;

            case DestroyTileCommand destroy:
                _visualState.SetTileScale(destroy.TileId, System.Numerics.Vector2.Zero);
                _visualState.SetTileAlpha(destroy.TileId, 0);
                _visualState.SetTileVisible(destroy.TileId, false);
                break;

            case MoveProjectileCommand moveProj:
                _visualState.SetProjectilePosition(moveProj.ProjectileId, moveProj.To);
                break;

            case ImpactProjectileCommand impact:
                _visualState.SetProjectileVisible(impact.ProjectileId, false);
                break;
        }

        // Clear animation flag when animation completes
        MarkTilesAnimating(cmd, false);
    }

    /// <summary>
    /// Update animation reference count for tiles affected by a command.
    /// Uses ref counting to handle overlapping animations correctly.
    /// </summary>
    private void MarkTilesAnimating(RenderCommand cmd, bool isStarting)
    {
        switch (cmd)
        {
            case MoveTileCommand move:
                var moveTile = _visualState.GetTile(move.TileId);
                if (moveTile != null)
                {
                    if (isStarting)
                        moveTile.AddAnimationRef();
                    else
                        moveTile.ReleaseAnimationRef();
                }
                break;

            case DestroyTileCommand destroy:
                var destroyTile = _visualState.GetTile(destroy.TileId);
                if (destroyTile != null)
                {
                    if (isStarting)
                        destroyTile.AddAnimationRef();
                    else
                        destroyTile.ReleaseAnimationRef();
                }
                break;

            case SwapTilesCommand swap:
                var tileA = _visualState.GetTile(swap.TileAId);
                var tileB = _visualState.GetTile(swap.TileBId);
                if (tileA != null)
                {
                    if (isStarting)
                        tileA.AddAnimationRef();
                    else
                        tileA.ReleaseAnimationRef();
                }
                if (tileB != null)
                {
                    if (isStarting)
                        tileB.AddAnimationRef();
                    else
                        tileB.ReleaseAnimationRef();
                }
                break;
        }
    }

    #endregion

    #region Easing Functions

    private static float ApplyEasing(float t, EasingType easing)
    {
        return easing switch
        {
            EasingType.Linear => t,
            EasingType.OutQuadratic => 1f - (1f - t) * (1f - t),
            EasingType.OutCubic => 1f - (float)Math.Pow(1f - t, 3),
            EasingType.InOutCubic => t < 0.5f
                ? 4f * t * t * t
                : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f,
            EasingType.OutBounce => OutBounce(t),
            _ => t
        };
    }

    private static float OutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
            return n1 * t * t;
        if (t < 2f / d1)
            return n1 * (t -= 1.5f / d1) * t + 0.75f;
        if (t < 2.5f / d1)
            return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }

    #endregion

    /// <summary>
    /// Binary search for the first command with StartTime >= currentTime.
    /// </summary>
    private int BinarySearchNextCommand(float currentTime)
    {
        int lo = 0, hi = _commands.Count;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (_commands[mid].StartTime < currentTime)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    private record struct ActiveCommand(RenderCommand Command, float StartedAt);
}
