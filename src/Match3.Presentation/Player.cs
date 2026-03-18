using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core;
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
                _visualState.AddTile(spawn.TileId, spawn.Type, spawn.GridPos, spawn.SpawnPos);
                break;

            case SpawnProjectileCommand spawnProj:
                _visualState.AddProjectile(spawnProj.ProjectileId, spawnProj.Origin, spawnProj.Type, spawnProj.ColorIndex);
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

            case UpdateTileTypeCommand updateType:
                var tileToUpdate = _visualState.GetTile(updateType.TileId);
                if (tileToUpdate != null)
                {
                    tileToUpdate.TileType = updateType.TileType;
                }
                break;

            case ScaleTileCommand scaleCmd:
                _visualState.SetTileScale(scaleCmd.TileId, scaleCmd.FromScale);
                break;

            case RotateTileCommand rotateCmd:
                _visualState.SetTileRotation(rotateCmd.TileId, rotateCmd.FromAngle);
                break;

            case UfoLaunchCommand ufo:
                var ufoTile = _visualState.GetTile(ufo.TileId);
                if (ufoTile != null)
                {
                    ufoTile.UfoFlightProgress = 0f;
                    ufoTile.UfoFlightDuration = ufo.Duration;
                }
                break;

            case UfoRetargetCommand retarget:
                HandleUfoRetarget(retarget);
                break;

            case SpawnObstacleCommand spawnObs:
                _visualState.AddObstacle(spawnObs.GridPos, spawnObs.ObstacleType, spawnObs.Stage);
                break;

            case DamageObstacleCommand damageObs:
            {
                var obsVisual = _visualState.GetObstacle(damageObs.GridPos);
                if (obsVisual != null)
                {
                    obsVisual.CurrentStage = damageObs.NewStage;
                    obsVisual.DamageProgress = 0f;
                }
                break;
            }

            case DestroyObstacleCommand destroyObs:
            {
                var obsVisual = _visualState.GetObstacle(destroyObs.GridPos);
                if (obsVisual != null)
                {
                    obsVisual.IsDestroying = true;
                    obsVisual.DeathProgress = 0f;
                }
                break;
            }
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
            {
                _visualState.RemoveTile(remove.TileId);
                break;
            }

            case RemoveProjectileCommand removeProj:
                _visualState.RemoveProjectile(removeProj.ProjectileId);
                break;

            case RemoveObstacleCommand removeObs:
                _visualState.RemoveObstacle(removeObs.GridPos);
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

            case ScaleTileCommand scaleCmd:
                float scaleEased = ApplyEasing(t, scaleCmd.Easing);
                var scaleVal = Vector2.Lerp(scaleCmd.FromScale, scaleCmd.ToScale, scaleEased);
                _visualState.SetTileScale(scaleCmd.TileId, scaleVal);
                break;

            case RotateTileCommand rotateCmd:
                float rotEased = ApplyEasing(t, rotateCmd.Easing);
                float angle = rotateCmd.FromAngle + (rotateCmd.ToAngle - rotateCmd.FromAngle) * rotEased;
                _visualState.SetTileRotation(rotateCmd.TileId, angle);
                break;

            case ShakeTileCommand shake:
                float elapsed = t * shake.Duration;
                float s = 1f + shake.Amplitude * (float)Math.Sin(elapsed * shake.Frequency * Math.PI * 2);
                _visualState.SetTileScale(shake.TileId, new Vector2(s, s));
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

            case DamageObstacleCommand damageObs:
            {
                var obsVisual = _visualState.GetObstacle(damageObs.GridPos);
                if (obsVisual != null)
                    obsVisual.DamageProgress = t;
                break;
            }

            case DestroyObstacleCommand destroyObs:
            {
                var obsVisual = _visualState.GetObstacle(destroyObs.GridPos);
                if (obsVisual != null)
                    obsVisual.DeathProgress = t;
                break;
            }

            case UfoLaunchCommand ufo:
            {
                var ufoT = _visualState.GetTile(ufo.TileId);
                if (ufoT != null)
                {
                    // For retarget segments (StayFraction==0), remap progress to [0.35, 1.0]
                    // so the View skips the takeoff phase (spin ramp, orientation tilt, Z depth)
                    // and stays in cruise mode. Without this, progress resets to 0 and the UFO
                    // visually "re-launches" from a grounded state.
                    ufoT.UfoFlightProgress = ufo.StayFraction > 0 ? t : 0.35f + t * 0.65f;

                    // XY position: stay at origin during spin-up, then cruise to target
                    float stayFrac = ufo.StayFraction;
                    const float arriveFrac = 0.97f;
                    float moveT;
                    if (t <= stayFrac)
                        moveT = 0f;
                    else if (t >= arriveFrac)
                        moveT = 1f;
                    else
                        moveT = (t - stayFrac) / (arriveFrac - stayFrac);

                    // Retarget segments: ease-out (full speed at entry, decelerate to land)
                    // Initial/diverge launches: smoothstep (accelerate from origin, decelerate to target)
                    float eased = ufo.MomentumControl.HasValue
                        ? 1f - (1f - moveT) * (1f - moveT)  // ease-out quadratic
                        : moveT * moveT * (3f - 2f * moveT); // smoothstep

                    Vector2 ufoPos;
                    if (ufo.DivergeControl.HasValue && ufo.ApproachControl.HasValue)
                    {
                        // Cubic Bezier: P0=Origin, P1=DivergeControl, P2=ApproachControl, P3=Target
                        ufoPos = UfoConstants.CubicBezier(eased,
                            ufo.Origin, ufo.DivergeControl.Value,
                            ufo.ApproachControl.Value, ufo.Target);
                    }
                    else if (ufo.MomentumControl.HasValue)
                    {
                        // Quadratic Bezier: P0=Origin, P1=Control, P2=Target
                        float inv = 1f - eased;
                        ufoPos = inv * inv * ufo.Origin
                               + 2f * inv * eased * ufo.MomentumControl.Value
                               + eased * eased * ufo.Target;
                    }
                    else
                    {
                        ufoPos = Vector2.Lerp(ufo.Origin, ufo.Target, eased);
                    }
                    _visualState.SetTilePosition(ufo.TileId, ufoPos);
                }
                break;
            }
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

            case ScaleTileCommand scaleCmd:
                _visualState.SetTileScale(scaleCmd.TileId, scaleCmd.ToScale);
                break;

            case RotateTileCommand rotateCmd:
                _visualState.SetTileRotation(rotateCmd.TileId, rotateCmd.ToAngle % 360f);
                break;

            case ShakeTileCommand shake:
                _visualState.SetTileScale(shake.TileId, Vector2.One);
                break;

            case ImpactProjectileCommand impact:
                _visualState.SetProjectileVisible(impact.ProjectileId, false);
                break;

            case UfoLaunchCommand ufo:
                var ufoTile = _visualState.GetTile(ufo.TileId);
                if (ufoTile != null)
                {
                    ufoTile.UfoFlightProgress = 1f;
                    _visualState.SetTilePosition(ufo.TileId, ufo.Target);
                }
                break;

            case DamageObstacleCommand damageObs:
            {
                var obsVisual = _visualState.GetObstacle(damageObs.GridPos);
                if (obsVisual != null)
                    obsVisual.DamageProgress = 1f;
                break;
            }

            case DestroyObstacleCommand destroyObs:
            {
                var obsVisual = _visualState.GetObstacle(destroyObs.GridPos);
                if (obsVisual != null)
                    obsVisual.DeathProgress = 1f;
                break;
            }
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

            case ScaleTileCommand scaleTile:
                var sTile = _visualState.GetTile(scaleTile.TileId);
                if (sTile != null)
                {
                    if (isStarting)
                        sTile.AddAnimationRef();
                    else
                        sTile.ReleaseAnimationRef();
                }
                break;

            case RotateTileCommand rotateTile:
                var rTile = _visualState.GetTile(rotateTile.TileId);
                if (rTile != null)
                {
                    if (isStarting)
                        rTile.AddAnimationRef();
                    else
                        rTile.ReleaseAnimationRef();
                }
                break;

            case ShakeTileCommand shakeTile:
                var shTile = _visualState.GetTile(shakeTile.TileId);
                if (shTile != null)
                {
                    if (isStarting)
                        shTile.AddAnimationRef();
                    else
                        shTile.ReleaseAnimationRef();
                }
                break;

            case UfoLaunchCommand ufoCmd:
                var uTile = _visualState.GetTile(ufoCmd.TileId);
                if (uTile != null)
                {
                    if (isStarting)
                        uTile.AddAnimationRef();
                    else
                        uTile.ReleaseAnimationRef();
                }
                break;

            case UfoRetargetCommand retargetCmd:
                // UfoRetargetCommand is instant — HandleUfoRetarget manages animation refs
                break;
        }
    }

    /// <summary>
    /// Handle UFO retarget: find the active UfoLaunchCommand, compute current position,
    /// and replace it with a new segment targeting the new destination.
    /// </summary>
    private void HandleUfoRetarget(UfoRetargetCommand retarget)
    {
        // Find and remove the active UfoLaunchCommand for this tile
        for (int i = _activeCommands.Count - 1; i >= 0; i--)
        {
            if (_activeCommands[i].Command is UfoLaunchCommand activeUfo && activeUfo.TileId == retarget.TileId)
            {
                // Compute current visual position for seamless handoff.
                // Use Max(_currentTime, retarget.StartTime) so that:
                //  - Normal case: matches the last rendered position (no 1-frame jump)
                //  - Same-tick case (launch+retarget in one tick): avoids t<0 → Origin
                var cmd = activeUfo;
                float retargetTime = Math.Max(_currentTime, retarget.StartTime);
                float t = cmd.Duration > 0
                    ? Math.Clamp((retargetTime - cmd.StartTime) / cmd.Duration, 0f, 1f)
                    : 1f;

                float stayFrac = cmd.StayFraction;
                const float arriveFrac = 0.97f;
                float moveT;
                if (t <= stayFrac)
                    moveT = 0f;
                else if (t >= arriveFrac)
                    moveT = 1f;
                else
                    moveT = (t - stayFrac) / (arriveFrac - stayFrac);

                // Match easing to UpdateCommand: smoothstep for initial/diverge, ease-out for retarget
                float eased = cmd.MomentumControl.HasValue
                    ? 1f - (1f - moveT) * (1f - moveT)   // ease-out quadratic (retarget)
                    : moveT * moveT * (3f - 2f * moveT);  // smoothstep (initial/diverge)
                Vector2 currentPos;
                if (cmd.DivergeControl.HasValue && cmd.ApproachControl.HasValue)
                {
                    currentPos = UfoConstants.CubicBezier(eased,
                        cmd.Origin, cmd.DivergeControl.Value,
                        cmd.ApproachControl.Value, cmd.Target);
                }
                else if (cmd.MomentumControl.HasValue)
                {
                    float inv = 1f - eased;
                    currentPos = inv * inv * cmd.Origin
                               + 2f * inv * eased * cmd.MomentumControl.Value
                               + eased * eased * cmd.Target;
                }
                else
                {
                    currentPos = Vector2.Lerp(cmd.Origin, cmd.Target, eased);
                }

                // Release animation ref for old command
                MarkTilesAnimating(activeUfo, false);
                _activeCommands.RemoveAt(i);

                // Compute duration from actual visual distance
                float visualDistance = Vector2.Distance(currentPos, retarget.NewTarget);
                float newDuration = visualDistance > 0
                    ? visualDistance / UfoConstants.FlightSpeed
                    : 0.01f;

                // Momentum control point: extend old flight direction to create a curved path.
                // Use cubic Bezier tangent when available for accurate direction.
                Vector2 oldDirVec;
                if (cmd.DivergeControl.HasValue && cmd.ApproachControl.HasValue)
                {
                    oldDirVec = UfoConstants.CubicBezierTangent(eased,
                        cmd.Origin, cmd.DivergeControl.Value,
                        cmd.ApproachControl.Value, cmd.Target);
                }
                else
                {
                    oldDirVec = cmd.Target - cmd.Origin;
                }
                float oldLen = oldDirVec.Length();
                Vector2? momentum = null;
                if (oldLen > 1e-4f && visualDistance > 1e-4f)
                {
                    var oldNorm = oldDirVec / oldLen;
                    float strength = UfoConstants.MomentumStrength(oldDirVec, retarget.NewTarget - currentPos);
                    momentum = currentPos + oldNorm * strength;
                }

                // Create new UfoLaunchCommand for the retarget segment
                var newCmd = new UfoLaunchCommand
                {
                    TileId = retarget.TileId,
                    Origin = currentPos,
                    Target = retarget.NewTarget,
                    MomentumControl = momentum,
                    StayFraction = 0f, // No spin-up for retarget
                    StartTime = retargetTime,
                    Duration = newDuration
                };

                // Start the new command — keep progress in cruise range (0.35+)
                // so the View doesn't replay the takeoff animation.
                // Set retarget flag so View can blend the arc transition smoothly.
                var ufoTile = _visualState.GetTile(retarget.TileId);
                if (ufoTile != null)
                {
                    ufoTile.UfoRetargetFlag = true;
                    ufoTile.UfoFlightProgress = 0.35f;
                    ufoTile.UfoFlightDuration = newDuration;
                }

                MarkTilesAnimating(newCmd, true);
                _activeCommands.Add(new ActiveCommand(newCmd, retargetTime));
                break;
            }
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
            EasingType.OutBack => OutBack(t),
            EasingType.InQuadratic => t * t,
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

    private static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * (float)Math.Pow(t - 1f, 3) + c1 * (float)Math.Pow(t - 1f, 2);
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
