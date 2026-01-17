using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Choreography;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Presentation;

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
        _commands.Sort((a, b) =>
        {
            int cmp = a.StartTime.CompareTo(b.StartTime);
            return cmp != 0 ? cmp : a.Priority.CompareTo(b.Priority);
        });

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

        // Sort only the new portion then merge
        _commands.Sort(insertStart, commands.Count, Comparer<RenderCommand>.Create((a, b) =>
        {
            int cmp = a.StartTime.CompareTo(b.StartTime);
            return cmp != 0 ? cmp : a.Priority.CompareTo(b.Priority);
        }));

        // If new commands have start times before current time, we need to re-sort
        if (commands.Count > 0)
        {
            // Full sort to handle interleaving
            _commands.Sort((a, b) =>
            {
                int cmp = a.StartTime.CompareTo(b.StartTime);
                return cmp != 0 ? cmp : a.Priority.CompareTo(b.Priority);
            });

            // Recalculate next command index - find first command not yet started
            _nextCommandIndex = _commands.Count;
            for (int i = 0; i < _commands.Count; i++)
            {
                if (_commands[i].StartTime >= _currentTime)
                {
                    _nextCommandIndex = i;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Advance the player by delta time, executing commands and updating visual state.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0) return;

        float targetTime = _currentTime + deltaTime;

        // Start new commands
        while (_nextCommandIndex < _commands.Count && _commands[_nextCommandIndex].StartTime <= targetTime)
        {
            var cmd = _commands[_nextCommandIndex];
            StartCommand(cmd);
            _nextCommandIndex++;
        }

        // Update active commands
        for (int i = _activeCommands.Count - 1; i >= 0; i--)
        {
            var active = _activeCommands[i];

            if (targetTime >= active.Command.EndTime)
            {
                // Command has completed - don't call UpdateCommand as it may overwrite
                // state set by commands that started later
                CompleteCommand(active);
                _activeCommands.RemoveAt(i);
            }
            else
            {
                // Command is still active - update it
                UpdateCommand(active, targetTime);
            }
        }

        _currentTime = targetTime;
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
                // Update bomb type in visual state
                var tile = _visualState.GetTile(updateBomb.TileId);
                if (tile != null)
                {
                    // VisualState uses immutable TileVisual with init-only properties
                    // We need to recreate the tile with the new bomb type
                    _visualState.RemoveTile(updateBomb.TileId);
                    _visualState.AddTile(updateBomb.TileId, tile.TileType, updateBomb.BombType,
                        updateBomb.Position, tile.Position);
                }
                break;
        }

        // Mark tiles as being animated for position-affecting commands
        MarkTilesAnimating(cmd, true);

        // Add to active commands if duration > 0
        if (cmd.Duration > 0)
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
                float scale = 1f - t * 0.3f;
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
    /// Mark tiles as being animated or not.
    /// This prevents physics sync from overwriting animated positions.
    /// </summary>
    private void MarkTilesAnimating(RenderCommand cmd, bool isAnimating)
    {
        switch (cmd)
        {
            case MoveTileCommand move:
                var moveTile = _visualState.GetTile(move.TileId);
                if (moveTile != null)
                    moveTile.IsBeingAnimated = isAnimating;
                break;

            case SwapTilesCommand swap:
                var tileA = _visualState.GetTile(swap.TileAId);
                var tileB = _visualState.GetTile(swap.TileBId);
                if (tileA != null)
                    tileA.IsBeingAnimated = isAnimating;
                if (tileB != null)
                    tileB.IsBeingAnimated = isAnimating;
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

    private record struct ActiveCommand(RenderCommand Command, float StartedAt);
}
