using System;
using System.Collections.Generic;
using System.Numerics;
using Match3.Core.Events;
using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;

namespace Match3.Core.Choreography;

/// <summary>
/// Converts GameEvent sequences into RenderCommand sequences with pre-calculated timing.
/// This enables deterministic replay and easy serialization.
/// </summary>
public sealed class Choreographer : IEventVisitor
{
    private readonly List<RenderCommand> _commands = new();
    private float _baseTime;
    private float _minSimulationTime;

    /// <summary>
    /// Timing configuration for all choreography animations.
    /// </summary>
    public ChoreographyConfig Config { get; set; } = new();

    /// <summary>
    /// Whether the last Choreograph() call produced a bomb merge sequence.
    /// Use this to decide whether to suppress physics sync.
    /// </summary>
    public bool LastBatchHadMerge { get; private set; }

    /// <summary>
    /// Whether the last Choreograph() call had non-merge match destroys.
    /// Use this to decide whether to apply drop delay locks.
    /// </summary>
    public bool LastBatchHadMatch { get; private set; }

    /// <summary>
    /// Per-cell lock schedule emitted by the last Choreograph() call.
    /// Bridge should acquire locks and release them after each entry's Duration.
    /// </summary>
    public IReadOnlyList<CellLockEntry> LockEntries => _lockEntries;
    private readonly List<CellLockEntry> _lockEntries = new();

    // Timing tracking for cascade calculations
    private readonly Dictionary<int, float> _columnDestroyEndTimes = new();
    private readonly Dictionary<int, List<MoveRecord>> _columnMoves = new();

    // Visual-only beam projectile IDs (negative to avoid collision with Core projectile IDs)
    private int _nextBeamId;

    // Color bomb beam hit times — maps target grid position to beam arrival time.
    // Used to delay TileDestroyedEvent animation until the beam visually reaches the target.
    private readonly Dictionary<Position, float> _beamHitTimes = new();

    // UFO flight tracking — projectile-driven: ProjectileLaunchedEvent.SourceTileId
    // links to the visual tile, ProjectileRetargetedEvent emits UfoRetargetCommand,
    // ProjectileImpactEvent emits destroy + remove.
    // Persists across Choreograph() batches because launch/retarget/impact may span ticks.
    private readonly Dictionary<int, UfoFlightInfo> _activeUfoFlights = new();

    private record struct UfoFlightInfo(int TileId, float LaunchTime, float Duration, Vector2 Origin, Vector2 Target, float StayFraction);

    private record struct MoveRecord(float StartTime, float EndTime, int TargetRow, Vector2 From, Vector2 To);

    /// <summary>
    /// Convert a sequence of game events into render commands.
    /// </summary>
    /// <param name="events">Events from simulation.</param>
    /// <param name="baseTime">Base timeline time for command scheduling.</param>
    /// <returns>List of render commands with pre-calculated timing.</returns>
    public IReadOnlyList<RenderCommand> Choreograph(IReadOnlyList<GameEvent> events, float baseTime = 0f)
    {
        _commands.Clear();
        _baseTime = baseTime;
        LastBatchHadMerge = false;
        LastBatchHadMatch = false;
        _lockEntries.Clear();
        _nextBeamId = -1;
        // Note: _beamHitTimes is NOT cleared here — it must persist across batches
        // because ExplosionSystem emits BombActivatedEvent and TileDestroyedEvents
        // in separate ticks. Entries are consumed (Remove) in EmitBeamTargetDestroy.

        // Calculate minimum simulation time to use relative offsets
        // This ensures events start at baseTime, not baseTime + cumulative engine time
        _minSimulationTime = 0f;
        if (events.Count > 0)
        {
            _minSimulationTime = float.MaxValue;
            foreach (var evt in events)
            {
                if (evt.SimulationTime < _minSimulationTime)
                    _minSimulationTime = evt.SimulationTime;
            }
        }

        // Clear timing tracking (reuse inner lists to avoid re-allocation)
        _columnDestroyEndTimes.Clear();
        foreach (var kvp in _columnMoves)
            kvp.Value.Clear();

        // Note: _activeUfoFlights persists across batches because ProjectileSystem
        // may emit launch/retarget/impact events across multiple ticks.

        // Process each event
        foreach (var evt in events)
        {
            evt.Accept(this);
        }

        return _commands;
    }

    private float GetStartTime(GameEvent evt)
    {
        // Use relative simulation time so first event starts at baseTime
        return _baseTime + (evt.SimulationTime - _minSimulationTime);
    }

    #region IEventVisitor Implementation

    /// <inheritdoc />
    public void Visit(TileMovedEvent evt)
    {
        int column = (int)evt.ToPosition.X;
        int targetRow = (int)evt.ToPosition.Y;

        // Calculate start time considering cascading animations
        float eventTime = GetStartTime(evt);
        float startTime = CalculateMoveStartTime(column, targetRow, eventTime);

        // If movement is delayed (waiting for destroy/merge to finish),
        // hold the tile at its current position. This sets IsBeingAnimated=true,
        // preventing SyncFallingTilesFromGameState from updating the position
        // via physics during the wait.
        if (startTime > eventTime)
        {
            _commands.Add(new MoveTileCommand
            {
                TileId = evt.TileId,
                From = evt.FromPosition,
                To = evt.FromPosition,
                StartTime = eventTime,
                Duration = startTime - eventTime,
                Easing = EasingType.Linear
            });
        }

        var command = new MoveTileCommand
        {
            TileId = evt.TileId,
            From = evt.FromPosition,
            To = evt.ToPosition,
            StartTime = startTime,
            Duration = Config.MoveDuration,
            Easing = EasingType.OutCubic
        };

        _commands.Add(command);

        // Track this move for cascade timing
        TrackMove(column, startTime, startTime + Config.MoveDuration, targetRow, evt.FromPosition, evt.ToPosition);
    }

    /// <inheritdoc />
    public void Visit(TileDestroyedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);
        int column = evt.GridPosition.X;

        if (evt.MergeTarget.HasValue)
        {
            // Merge animation: move to bomb origin and shrink
            var target = new Vector2(evt.MergeTarget.Value.X, evt.MergeTarget.Value.Y);

            _commands.Add(new MoveTileCommand
            {
                TileId = evt.TileId,
                From = position,
                To = target,
                StartTime = startTime,
                Duration = Config.MergeDuration,
                Easing = EasingType.InOutCubic
            });

            _commands.Add(new ScaleTileCommand
            {
                TileId = evt.TileId,
                FromScale = Vector2.One,
                ToScale = Vector2.Zero,
                StartTime = startTime,
                Duration = Config.MergeDuration,
                Easing = EasingType.InOutCubic
            });

            float endTime = startTime + Config.MergeDuration;

            // Remove tile after merge completes
            _commands.Add(new RemoveTileCommand
            {
                TileId = evt.TileId,
                StartTime = endTime,
                Duration = 0,
                Priority = 10
            });

            // Lock merge source for merge duration only
            // Source positions become empty after tile slides away — no bomb pop-in here
            _lockEntries.Add(new CellLockEntry
            {
                Position = evt.GridPosition,
                LockType = CellLockType.Receive,
                Duration = Config.MergeDuration,
                IsMerge = true
            });

            // Track merge end time for column cascade stalling
            if (!_columnDestroyEndTimes.TryGetValue(column, out float existing) || endTime > existing)
            {
                _columnDestroyEndTimes[column] = endTime;
            }
        }
        else if (_beamHitTimes.TryGetValue(evt.GridPosition, out float beamHitTime))
        {
            // Color bomb beam target: delay destruction until beam arrives
            _beamHitTimes.Remove(evt.GridPosition);
            EmitBeamTargetDestroy(evt, position, column, beamHitTime);
        }
        else
        {
            LastBatchHadMatch = true;

            // Wave delay: later waves (higher SimulationTime) get longer locks
            float waveDelay = evt.SimulationTime - _minSimulationTime;
            float dropDelay = evt.Reason switch
            {
                DestroyReason.BombEffect => Config.BombDropDelay,
                DestroyReason.Projectile => 0.05f, // Fast unlock for UFO/Projectile hits
                _ => Config.DropDelay
            };

            // Emit cell lock for destroy position (drop delay + wave offset)
            _lockEntries.Add(new CellLockEntry
            {
                Position = evt.GridPosition,
                LockType = CellLockType.Receive,
                Duration = waveDelay + dropDelay,
                IsMerge = false
            });

            // Standard destroy animation: fade + scale down in place
            _commands.Add(new DestroyTileCommand
            {
                TileId = evt.TileId,
                Position = position,
                Reason = evt.Reason,
                StartTime = startTime,
                Duration = Config.DestroyDuration
            });

            float endTime = startTime + Config.DestroyDuration;

            if (!_columnDestroyEndTimes.TryGetValue(column, out float existing) || endTime > existing)
            {
                _columnDestroyEndTimes[column] = endTime;
            }

            // Add visual effect
            if (!evt.IsGoal)
            {
                string effectType = evt.Reason switch
                {
                    DestroyReason.Match => "match_pop",
                    DestroyReason.BombEffect => "explosion",
                    DestroyReason.Projectile => "projectile_hit",
                    DestroyReason.ChainReaction => "chain_pop",
                    _ => "pop"
                };

                _commands.Add(new ShowEffectCommand
                {
                    EffectType = effectType,
                    Position = position,
                    StartTime = startTime,
                    Duration = Config.DestroyDuration
                });
            }

            // Remove tile after destroy animation completes
            _commands.Add(new RemoveTileCommand
            {
                TileId = evt.TileId,
                StartTime = endTime,
                Duration = 0,
                Priority = 10
            });
        }
    }

    // EmitUfoTargetDestroy removed — UFO target destruction is now handled by
    // ProjectileImpactEvent via the ProjectileSystem.

    /// <summary>
    /// Color bomb beam target: delay destruction until beam arrives.
    /// </summary>
    private void EmitBeamTargetDestroy(TileDestroyedEvent evt, Vector2 position, int column, float beamHitTime)
    {
        LastBatchHadMatch = true;

        // Pause after beam impact before tile starts dissolving —
        // gives the player time to read the beam pattern before targets explode.
        const float hitPause = 0.5f;
        float destroyStart = beamHitTime + hitPause;

        // Hold tile in place until destroy starts — keeps IsBeingAnimated=true
        // so SyncFallingTilesFromGameState won't garbage-collect it early.
        float holdStart = GetStartTime(evt);
        if (destroyStart > holdStart)
        {
            _commands.Add(new MoveTileCommand
            {
                TileId = evt.TileId,
                From = position,
                To = position,
                StartTime = holdStart,
                Duration = destroyStart - holdStart,
                Easing = EasingType.Linear
            });
        }

        // Destroy animation starts after hit pause
        _commands.Add(new DestroyTileCommand
        {
            TileId = evt.TileId,
            Position = position,
            Reason = evt.Reason,
            StartTime = destroyStart,
            Duration = Config.DestroyDuration
        });

        _commands.Add(new ShowEffectCommand
        {
            EffectType = "explosion",
            Position = position,
            StartTime = destroyStart,
            Duration = Config.DestroyDuration
        });

        float endTime = destroyStart + Config.DestroyDuration;

        _commands.Add(new RemoveTileCommand
        {
            TileId = evt.TileId,
            StartTime = endTime,
            Duration = 0,
            Priority = 10
        });

        float lockEnd = endTime + Config.BombDropDelay;

        _lockEntries.Add(new CellLockEntry
        {
            Position = evt.GridPosition,
            LockType = CellLockType.Receive,
            Duration = lockEnd,
            IsMerge = false
        });

        if (!_columnDestroyEndTimes.TryGetValue(column, out float existing) || lockEnd > existing)
        {
            _columnDestroyEndTimes[column] = lockEnd;
        }
    }

    /// <inheritdoc />
    public void Visit(TileSpawnedEvent evt)
    {
        int column = evt.GridPosition.X;
        float startTime = GetStartTime(evt);

        // Delay spawn until after destroy/merge animations in this column.
        // Without this, tiles would appear and start falling (via physics sync)
        // while merge animations are still playing.
        if (_columnDestroyEndTimes.TryGetValue(column, out float destroyEndTime))
        {
            startTime = Math.Max(startTime, destroyEndTime);
        }

        // Spawn command - creates the tile in visual state
        // Physics system handles the falling animation via SyncFallingTilesFromGameState
        var spawnCommand = new SpawnTileCommand
        {
            TileId = evt.TileId,
            Type = evt.Type,
            GridPos = evt.GridPosition,
            SpawnPos = evt.SpawnPosition,
            StartTime = startTime,
            Duration = 0
        };
        _commands.Add(spawnCommand);

        // No MoveTileCommand - physics system controls the falling movement
        // Cascade timing is handled naturally by physics (tiles wait for space below)
    }

    /// <inheritdoc />
    public void Visit(TilesSwappedEvent evt)
    {
        // New move boundary — clear any stale beam hit times from previous moves
        _beamHitTimes.Clear();

        float startTime = GetStartTime(evt);
        var posA = new Vector2(evt.PositionA.X, evt.PositionA.Y);
        var posB = new Vector2(evt.PositionB.X, evt.PositionB.Y);

        var swapCommand = new SwapTilesCommand
        {
            TileAId = evt.TileAId,
            TileBId = evt.TileBId,
            PosA = posA,
            PosB = posB,
            IsRevert = evt.IsRevert,
            StartTime = startTime,
            Duration = Config.SwapDuration,
            Easing = EasingType.OutCubic
        };
        _commands.Add(swapCommand);
    }

    /// <inheritdoc />
    public void Visit(MatchDetectedEvent evt)
    {
        float startTime = GetStartTime(evt);

        // Convert to array for the command
        var positions = new Models.Grid.Position[evt.Positions.Count];
        int i = 0;
        foreach (var pos in evt.Positions)
        {
            positions[i++] = pos;
        }

        var highlightCommand = new ShowMatchHighlightCommand
        {
            Positions = positions,
            StartTime = startTime,
            Duration = Config.MatchHighlightDuration
        };
        _commands.Add(highlightCommand);
    }

    /// <inheritdoc />
    public void Visit(BombCreatedEvent evt)
    {
        LastBatchHadMerge = true;

        // Lock bomb origin for merge + pop-in duration to prevent gravity overlap
        float totalBombDuration = Config.MergeDuration + Config.BombPopDuration;
        _lockEntries.Add(new CellLockEntry
        {
            Position = evt.Position,
            LockType = CellLockType.Receive | CellLockType.Drop,
            Duration = totalBombDuration,
            IsMerge = true
        });

        float baseStart = GetStartTime(evt);
        float mergeEndTime = baseStart + Config.MergeDuration;
        var position = new Vector2(evt.Position.X, evt.Position.Y);
        int column = evt.Position.X;

        // Hold the bomb-origin tile in place during merge.
        // This sets IsBeingAnimated=true, preventing SyncFallingTilesFromGameState
        // from removing it (old ID no longer in game state) during the merge.
        _commands.Add(new MoveTileCommand
        {
            TileId = evt.TileId,
            From = position,
            To = position,
            StartTime = baseStart,
            Duration = Config.MergeDuration,
            Easing = EasingType.Linear
        });

        // Spawn the new bomb tile immediately at scale=0 so that
        // SyncFallingTilesFromGameState doesn't create it at full size.
        // The tile already exists in GameState after ProcessMatches.
        // BombType IS the tile's ElementType in the unified model.
        _commands.Add(new SpawnTileCommand
        {
            TileId = evt.NewTileId,
            Type = evt.BombType,
            GridPos = evt.Position,
            SpawnPos = position,
            StartTime = baseStart,
            Duration = 0,
            Priority = 1
        });

        // Immediately set scale to zero — invisible during merge animation
        _commands.Add(new ScaleTileCommand
        {
            TileId = evt.NewTileId,
            FromScale = Vector2.Zero,
            ToScale = Vector2.Zero,
            StartTime = baseStart,
            Duration = Config.MergeDuration,
            Easing = EasingType.Linear,
            Priority = 2
        });

        // Remove old tile after merge completes
        _commands.Add(new RemoveTileCommand
        {
            TileId = evt.TileId,
            StartTime = mergeEndTime,
            Duration = 0,
            Priority = 5
        });

        // Bomb pop-in scale animation (0 → 1)
        _commands.Add(new ScaleTileCommand
        {
            TileId = evt.NewTileId,
            FromScale = Vector2.Zero,
            ToScale = Vector2.One,
            StartTime = mergeEndTime,
            Duration = Config.BombPopDuration,
            Easing = EasingType.OutBack,
            Priority = 7
        });

        // Visual effect
        _commands.Add(new ShowEffectCommand
        {
            EffectType = "bomb_created",
            Position = position,
            StartTime = mergeEndTime,
            Duration = 0.3f
        });

        // Track full bomb appearance time for this column (delays gravity)
        // Include BombPopDuration so tiles above don't fall until pop-in completes
        float bombReadyTime = mergeEndTime + Config.BombPopDuration;
        if (!_columnDestroyEndTimes.TryGetValue(column, out float existing) || bombReadyTime > existing)
        {
            _columnDestroyEndTimes[column] = bombReadyTime;
        }
    }

    /// <inheritdoc />
    public void Visit(BombActivatedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var origin = new Vector2(evt.Position.X, evt.Position.Y);

        // Common flash at origin (skip for UFO — it flies away, flash looks wrong)
        if (evt.BombType != ElementType.Ufo)
        {
            _commands.Add(new ShowEffectCommand
            {
                EffectType = "bomb_flash",
                Position = origin,
                StartTime = startTime,
                Duration = 0.15f
            });
        }

        switch (evt.BombType)
        {
            case ElementType.HorizontalRocket:
            case ElementType.VerticalRocket:
                EmitRocketEffects(evt, startTime, origin);
                break;

            case ElementType.ColorBomb:
                EmitColorBombEffects(evt, startTime, origin);
                break;

            case ElementType.Ufo:
                EmitUfoEffects(evt, startTime, origin);
                break;

            default:
                // Square5x5, etc. — generic shockwave + explosion
                _commands.Add(new ShowEffectCommand
                {
                    EffectType = "bomb_shockwave",
                    Position = origin,
                    StartTime = startTime,
                    Duration = 0.3f
                });
                _commands.Add(new ShowEffectCommand
                {
                    EffectType = "bomb_explosion",
                    Position = origin,
                    StartTime = startTime,
                    Duration = 0.25f
                });
                break;
        }
    }

    /// <summary>
    /// Emit directional trail + head effects for Horizontal/Vertical rockets.
    /// </summary>
    private void EmitRocketEffects(BombActivatedEvent evt, float startTime, Vector2 origin)
    {
        bool isHorizontal = evt.BombType == ElementType.HorizontalRocket;
        string trailType = isHorizontal ? "rocket_trail_h" : "rocket_trail_v";
        const float trailAccel = 0.8f; // matches logical layer acceleration

        int minExtent = int.MaxValue;
        int maxExtent = int.MinValue;

        // Pre-compute cumulative delays with acceleration: delay[d] = sum of interval * accel^i for i in [0, d-1]
        // Find max distance first
        int maxDist = 0;
        int originAxis = isHorizontal ? evt.Position.X : evt.Position.Y;
        foreach (var pos in evt.AffectedPositions)
        {
            int axis = isHorizontal ? pos.X : pos.Y;
            int d = Math.Abs(axis - originAxis);
            if (d > maxDist) maxDist = d;
        }

        // Build cumulative delay table
        float[] cumulativeDelay = new float[maxDist + 1];
        cumulativeDelay[0] = 0f;
        float interval = Config.RocketTrailInterval;
        for (int i = 1; i <= maxDist; i++)
        {
            cumulativeDelay[i] = cumulativeDelay[i - 1] + interval;
            interval *= trailAccel;
        }

        // Emit staggered trail effects along the rocket path
        foreach (var pos in evt.AffectedPositions)
        {
            int axis = isHorizontal ? pos.X : pos.Y;
            int dist = Math.Abs(axis - originAxis);

            // Track extents for head effects
            if (axis < minExtent) minExtent = axis;
            if (axis > maxExtent) maxExtent = axis;

            // Skip the origin cell (already has flash)
            if (dist == 0) continue;

            _commands.Add(new ShowEffectCommand
            {
                EffectType = trailType,
                Position = new Vector2(pos.X, pos.Y),
                StartTime = startTime + cumulativeDelay[dist],
                Duration = 0.15f
            });
        }

        // Head effects at the two endpoints
        if (minExtent != int.MaxValue)
        {
            int crossAxis = isHorizontal ? evt.Position.Y : evt.Position.X;

            int minDist = Math.Abs(minExtent - originAxis);
            int maxDistEnd = Math.Abs(maxExtent - originAxis);

            var minPos = isHorizontal
                ? new Vector2(minExtent, crossAxis)
                : new Vector2(crossAxis, minExtent);
            var maxPos = isHorizontal
                ? new Vector2(maxExtent, crossAxis)
                : new Vector2(crossAxis, maxExtent);

            _commands.Add(new ShowEffectCommand
            {
                EffectType = "rocket_head",
                Position = minPos,
                StartTime = startTime + cumulativeDelay[minDist],
                Duration = 0.2f
            });
            _commands.Add(new ShowEffectCommand
            {
                EffectType = "rocket_head",
                Position = maxPos,
                StartTime = startTime + cumulativeDelay[maxDistEnd],
                Duration = 0.2f
            });
        }
    }

    /// <summary>
    /// Color bomb tap activation: delegates to shared performance method.
    /// </summary>
    private void EmitColorBombEffects(BombActivatedEvent evt, float startTime, Vector2 origin)
        => EmitColorBombPerformance(evt.TileId, evt.Position, evt.AffectedPositions, startTime, origin);

    /// <summary>
    /// Color bomb performance: charge-up (scale + hop + spin) → beams → shrink + remove.
    /// Shared by tap (BombActivatedEvent) and swap (BombComboEvent) paths.
    /// ClearBombAttribute sets the tile to None before choreography, so no TileDestroyedEvent
    /// is emitted for the origin. We emit RemoveTileCommand explicitly after beams land.
    /// </summary>
    private void EmitColorBombPerformance(int tileId, Position gridOrigin,
        IReadOnlyCollection<Position> affectedPositions, float startTime, Vector2 origin)
    {
        float chargeDuration = Config.ColorBombChargeDuration;
        float chargeScale = Config.ColorBombChargeScale;
        float beamLaunchTime = startTime + chargeDuration;

        // Phase 1: Charge-up — scale pulse + hop up + accelerating spin
        var hopOffset = new Vector2(0, Config.ColorBombHopOffset);

        _commands.Add(new ScaleTileCommand
        {
            TileId = tileId,
            FromScale = Vector2.One,
            ToScale = new Vector2(chargeScale, chargeScale),
            Easing = EasingType.OutBack,
            StartTime = startTime,
            Duration = chargeDuration
        });

        _commands.Add(new MoveTileCommand
        {
            TileId = tileId,
            From = origin,
            To = origin + hopOffset,
            Easing = EasingType.OutBack,
            StartTime = startTime,
            Duration = chargeDuration
        });

        // Phase 2: Beams launch after charge-up (from hopped position)
        var hoppedOrigin = origin + hopOffset;
        float maxHitTime = EmitColorBombBeams(gridOrigin, affectedPositions, beamLaunchTime, hoppedOrigin);

        // Spin covers the entire performance (charge + beam flight + hit pause)
        // Hit pause = 0.5s matches EmitBeamTargetDestroy so bomb disappears
        // at the same moment targets start dissolving.
        const float hitPause = 0.5f;
        float spinEndTime = Math.Max(maxHitTime + hitPause, beamLaunchTime + 0.1f);
        float spinDuration = spinEndTime - startTime;

        _commands.Add(new RotateTileCommand
        {
            TileId = tileId,
            FromAngle = 0f,
            ToAngle = Config.ColorBombSpinRate * spinDuration,
            Easing = EasingType.InQuadratic,
            StartTime = startTime,
            Duration = spinDuration
        });

        // Glow/flash persists throughout the entire performance
        _commands.Add(new ShowEffectCommand
        {
            EffectType = "bomb_flash",
            Position = hoppedOrigin,
            StartTime = startTime,
            Duration = spinDuration
        });

        // Phase 3: After targets start dissolving — shrink to nothing + remove
        float shrinkDuration = Config.ColorBombShrinkDuration;

        _commands.Add(new ScaleTileCommand
        {
            TileId = tileId,
            FromScale = new Vector2(chargeScale, chargeScale),
            ToScale = Vector2.Zero,
            Easing = EasingType.InQuadratic,
            StartTime = spinEndTime,
            Duration = shrinkDuration
        });

        _commands.Add(new RemoveTileCommand
        {
            TileId = tileId,
            StartTime = spinEndTime + shrinkDuration,
            Duration = 0,
            Priority = 10
        });

        // Lock the origin cell until the performance completes
        _lockEntries.Add(new CellLockEntry
        {
            Position = gridOrigin,
            LockType = CellLockType.Receive,
            Duration = spinEndTime + shrinkDuration,
            IsMerge = false
        });
    }

    /// <summary>
    /// Emit rainbow wave at origin + flying beam projectiles + sparkle hits for Color bomb.
    /// Beams fire farthest-first with staggered launches, speeds adjusted so ALL beams
    /// arrive simultaneously. Same-distance targets are also staggered via angle tiebreak.
    /// Returns the arrival time (for sequencing follow-up animations).
    /// </summary>
    private float EmitColorBombBeams(Position gridOrigin, IReadOnlyCollection<Position> affectedPositions,
        float startTime, Vector2 origin)
    {
        // Rainbow wave at origin
        _commands.Add(new ShowEffectCommand
        {
            EffectType = "color_bomb_wave",
            Position = origin,
            StartTime = startTime,
            Duration = 0.6f
        });

        // NOTE: bomb_flash moved to EmitColorBombPerformance to span the full performance

        if (affectedPositions.Count == 0) return startTime;

        // 1. Build target list with euclidean distance and angle for sorting
        //    Skip origin position — no beam needed for self
        var targets = new List<(Position pos, Vector2 target, float dist, float angle)>();
        foreach (var pos in affectedPositions)
        {
            if (pos.X == gridOrigin.X && pos.Y == gridOrigin.Y) continue;
            var targetPos = new Vector2(pos.X, pos.Y);
            float dist = Vector2.Distance(origin, targetPos);
            float angle = MathF.Atan2(pos.Y - gridOrigin.Y, pos.X - gridOrigin.X);
            targets.Add((pos, targetPos, dist, angle));
        }

        if (targets.Count == 0) return startTime;

        // 2. Sort: farthest first; same distance → by angle (deterministic spread)
        targets.Sort((a, b) =>
        {
            int cmp = b.dist.CompareTo(a.dist);
            return cmp != 0 ? cmp : a.angle.CompareTo(b.angle);
        });

        // 3. Compute launch times: farthest fires first, each subsequent beam staggered
        float stagger = Config.ColorBombBeamStagger;
        float firstLaunch = startTime + 0.05f;
        float lastLaunch = firstLaunch + (targets.Count - 1) * stagger;
        float maxDist = targets[0].dist;

        // 4. Arrival time: all beams land at this moment
        //    Must satisfy both the farthest beam's flight (at base speed)
        //    and the closest beam's minimum flight duration.
        float arrivalTime = Math.Max(
            firstLaunch + maxDist / Config.ColorBombBeamSpeed,
            lastLaunch + Config.ColorBombMinFlightDuration);

        byte colorIndex = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            var (pos, targetPos, dist, _) = targets[i];
            float launchTime = firstLaunch + i * stagger;
            float flightDuration = arrivalTime - launchTime;

            // Record hit time so TileDestroyedEvent delays destruction until beam arrives
            _beamHitTimes[pos] = arrivalTime;

            // Beam projectile: spawn → fly → impact → remove
            int beamId = _nextBeamId--;
            byte beamColor = colorIndex;
            colorIndex = (byte)((colorIndex + 1) % 6);

            _commands.Add(new SpawnProjectileCommand
            {
                ProjectileId = beamId,
                Origin = origin,
                ArcHeight = 0f,
                Type = ProjectileType.ColorBombBeam,
                ColorIndex = beamColor,
                StartTime = launchTime,
                Duration = 0
            });

            _commands.Add(new MoveProjectileCommand
            {
                ProjectileId = beamId,
                From = origin,
                To = targetPos,
                StartTime = launchTime,
                Duration = flightDuration
            });

            _commands.Add(new ImpactProjectileCommand
            {
                ProjectileId = beamId,
                Position = targetPos,
                EffectType = "color_bomb_hit",
                StartTime = arrivalTime,
                Duration = 0.5f
            });

            _commands.Add(new RemoveProjectileCommand
            {
                ProjectileId = beamId,
                StartTime = arrivalTime + 0.5f,
                Duration = 0
            });

            _commands.Add(new ShowEffectCommand
            {
                EffectType = "color_bomb_hit",
                Position = targetPos,
                StartTime = arrivalTime,
                Duration = 0.5f
            });
        }

        return arrivalTime;
    }

    /// <summary>
    /// Emit UFO-specific effects: origin cell lock for launch clearing.
    /// UfoLaunchCommand is emitted from Visit(ProjectileLaunchedEvent) using SourceTileId.
    /// Cross tiles get standard destroy; remote target is destroyed on projectile impact.
    /// </summary>
    private void EmitUfoEffects(BombActivatedEvent evt, float startTime, Vector2 origin)
    {

        // Origin cell is free shortly after launch
        int column = evt.Position.X;
        float launchClearTime = 0.45f;
        _lockEntries.Add(new CellLockEntry
        {
            Position = evt.Position,
            LockType = CellLockType.Receive,
            Duration = launchClearTime + Config.BombDropDelay,
            IsMerge = false
        });

        float endTime = startTime + launchClearTime;
        if (!_columnDestroyEndTimes.TryGetValue(column, out float existing) || endTime > existing)
        {
            _columnDestroyEndTimes[column] = endTime;
        }
    }

    /// <inheritdoc />
    public void Visit(BombComboEvent evt)
    {
        float startTime = GetStartTime(evt);
        var posA = new Vector2(evt.PositionA.X, evt.PositionA.Y);
        var posB = new Vector2(evt.PositionB.X, evt.PositionB.Y);

        // Double color bomb gets the full 4-phase treatment
        if (evt.BombTypeA == ElementType.ColorBomb && evt.BombTypeB == ElementType.ColorBomb)
        {
            EmitDoubleColorBombEffects(evt, startTime, posA, posB);
            return;
        }

        // Color bomb + normal tile: same performance as tap path
        if (evt.BombTypeA == ElementType.ColorBomb || evt.BombTypeB == ElementType.ColorBomb)
        {
            bool aIsColor = evt.BombTypeA == ElementType.ColorBomb;
            var colorPos = aIsColor ? evt.PositionA : evt.PositionB;
            var origin = aIsColor ? posA : posB;
            int colorTileId = aIsColor ? evt.TileIdA : evt.TileIdB;
            EmitColorBombPerformance(colorTileId, colorPos, evt.AffectedPositions, startTime, origin);
            return;
        }

        // Other combos: generic effect at both positions
        _commands.Add(new ShowEffectCommand
        {
            EffectType = "bomb_combo",
            Position = posA,
            StartTime = startTime,
            Duration = 0.5f
        });
        _commands.Add(new ShowEffectCommand
        {
            EffectType = "bomb_combo",
            Position = posB,
            StartTime = startTime,
            Duration = 0.5f
        });
    }

    /// <summary>
    /// Four-phase choreography for double color bomb combo.
    /// Phase 1: Converge — two bombs shrink toward midpoint, point lights grow
    /// Phase 2: Fusion — bright flash at midpoint, time freeze
    /// Phase 3: Wipe — radial light wave destroys all tiles outward
    /// Phase 4: Aftermath — camera shake, bloom decay
    /// </summary>
    private void EmitDoubleColorBombEffects(BombComboEvent evt, float startTime, Vector2 posA, Vector2 posB)
    {
        var midpoint = (posA + posB) * 0.5f;
        float t = startTime;

        // ── Phase 1: Converge ──
        float convergeDur = Config.DoubleColorConvergeDuration;

        // Point lights on both bombs ramp up during converge
        _commands.Add(new ShowEffectCommand
        {
            EffectType = "colorx2_converge",
            Position = midpoint,
            StartTime = t,
            Duration = convergeDur
        });

        t += convergeDur;

        // ── Phase 2: Fusion ──
        float fusionDur = Config.DoubleColorFusionDuration;

        _commands.Add(new ShowEffectCommand
        {
            EffectType = "colorx2_fusion",
            Position = midpoint,
            StartTime = t,
            Duration = fusionDur
        });

        t += fusionDur;

        // ── Phase 3: Wipe ──
        // Compute max Chebyshev distance from origin
        int originX = evt.PositionB.X;
        int originY = evt.PositionB.Y;
        int maxDist = 0;
        foreach (var pos in evt.AffectedPositions)
        {
            int d = Math.Max(Math.Abs(pos.X - originX), Math.Abs(pos.Y - originY));
            if (d > maxDist) maxDist = d;
        }

        // Build cumulative delay table with acceleration
        float[] wipeCumDelay = new float[maxDist + 1];
        wipeCumDelay[0] = 0f;
        float wipeInterval = Config.DoubleColorWipeInterval;
        for (int i = 1; i <= maxDist; i++)
        {
            wipeCumDelay[i] = wipeCumDelay[i - 1] + wipeInterval;
            wipeInterval *= Config.DoubleColorWipeAccel;
        }

        float wipeStart = t;

        // Per-tile wipe hit: staggered sparkle + point light burst
        foreach (var pos in evt.AffectedPositions)
        {
            int dist = Math.Max(Math.Abs(pos.X - originX), Math.Abs(pos.Y - originY));
            float hitTime = wipeStart + wipeCumDelay[dist];

            _commands.Add(new ShowEffectCommand
            {
                EffectType = "colorx2_wipe_hit",
                Position = new Vector2(pos.X, pos.Y),
                StartTime = hitTime,
                Duration = 0.15f
            });
        }

        // No blanket cell locks here — the ExplosionSystem handles tile suspension,
        // and each TileDestroyedEvent adds its own per-cell BombDropDelay lock.
        // ShowEffectCommands are fire-and-forget and don't block gravity.

        float totalWipeDur = wipeCumDelay[maxDist] + 0.15f;
        t += totalWipeDur;

        // ── Phase 4: Aftermath ──
        _commands.Add(new ShowEffectCommand
        {
            EffectType = "colorx2_aftermath",
            Position = midpoint,
            StartTime = t,
            Duration = 0.3f
        });
    }

    /// <inheritdoc />
    public void Visit(ScoreAddedEvent evt)
    {
        // Score events don't generate render commands
        // UI handles score display separately
    }

    /// <inheritdoc />
    public void Visit(ComboChangedEvent evt)
    {
        // Combo events don't generate render commands
        // UI handles combo display separately
    }

    /// <inheritdoc />
    public void Visit(MoveCompletedEvent evt)
    {
        // Move completed events don't generate render commands
    }

    /// <inheritdoc />
    public void Visit(ProjectileLaunchedEvent evt)
    {
        float startTime = GetStartTime(evt);

        // UFO projectile: emit UfoLaunchCommand instead of SpawnProjectileCommand
        if (evt.Type == ProjectileType.Ufo)
        {
            int tileId = evt.SourceTileId ?? 0;

            if (tileId == 0) return; // No tile to animate

            var targetPos = evt.TargetPosition.HasValue
                ? new Vector2(evt.TargetPosition.Value.X, evt.TargetPosition.Value.Y)
                : evt.Origin;

            // Overshoot along flight direction: the UFO's propeller sits on top and
            // the bomb body hangs below, so the propeller must fly slightly past the
            // target for the bomb body to align with the target cell center.
            float distance = Vector2.Distance(evt.Origin, targetPos);
            if (distance > 0)
            {
                var dir = (targetPos - evt.Origin) / distance;
                targetPos += dir * 0.3f;
            }
            // Additional screen-up nudge (grid Y is inverted) to fine-tune
            // the bomb body's vertical alignment with the target cell.
            targetPos.Y -= 0.40f;
            float flightTime = distance > 0 ? distance / Config.UfoFlightSpeed : 0.01f;
            float totalDuration = Config.UfoLaunchOverhead + flightTime;

            _commands.Add(new UfoLaunchCommand
            {
                TileId = tileId,
                Origin = evt.Origin,
                Target = targetPos,
                StayFraction = 0.35f,
                StartTime = startTime,
                Duration = totalDuration
            });

            // Track active flight for retarget/impact handling
            _activeUfoFlights[evt.ProjectileId] = new UfoFlightInfo(
                tileId, startTime, totalDuration, evt.Origin, targetPos, 0.35f);

            return;
        }

        var spawnCommand = new SpawnProjectileCommand
        {
            ProjectileId = evt.ProjectileId,
            Origin = evt.Origin,
            ArcHeight = Config.ProjectileArcHeight,
            Type = evt.Type,
            StartTime = startTime,
            Duration = Config.ProjectileTakeoffDuration
        };
        _commands.Add(spawnCommand);
    }

    /// <inheritdoc />
    public void Visit(ProjectileMovedEvent evt)
    {
        float startTime = GetStartTime(evt);

        // Calculate duration based on velocity
        float distance = Vector2.Distance(evt.FromPosition, evt.ToPosition);
        float velocity = evt.Velocity.Length();
        float duration = velocity > 0 ? distance / velocity : 0.016f;

        var moveCommand = new MoveProjectileCommand
        {
            ProjectileId = evt.ProjectileId,
            From = evt.FromPosition,
            To = evt.ToPosition,
            StartTime = startTime,
            Duration = duration
        };
        _commands.Add(moveCommand);
    }

    /// <inheritdoc />
    public void Visit(ProjectileRetargetedEvent evt)
    {
        if (!_activeUfoFlights.TryGetValue(evt.ProjectileId, out var flight))
            return;

        float startTime = GetStartTime(evt);
        var newTargetVec = new Vector2(evt.NewTarget.X, evt.NewTarget.Y);

        // Estimate current position using smoothstep + stayFraction to match Player's easing.
        // Without this, the Choreographer's linear estimate diverges from the Player's actual
        // visual position, causing flightEndTime to be too early and the tile to be removed
        // before the Player's animation reaches the target.
        float elapsed = startTime - flight.LaunchTime;
        float t = flight.Duration > 0 ? Math.Clamp(elapsed / flight.Duration, 0f, 1f) : 0f;
        float stayFrac = flight.StayFraction;
        const float arriveFrac = 0.97f;
        float moveT;
        if (t <= stayFrac)
            moveT = 0f;
        else if (t >= arriveFrac)
            moveT = 1f;
        else
            moveT = (t - stayFrac) / (arriveFrac - stayFrac);
        float eased = moveT * moveT * (3f - 2f * moveT);
        var currentPos = Vector2.Lerp(flight.Origin, flight.Target, eased);

        // Overshoot along flight direction (same as initial launch)
        float retargetDist = Vector2.Distance(currentPos, newTargetVec);
        if (retargetDist > 0)
        {
            var dir = (newTargetVec - currentPos) / retargetDist;
            newTargetVec += dir * 0.3f;
        }
        newTargetVec.Y -= 0.40f;

        // New flight segment duration based on distance to new target
        float newDistance = Vector2.Distance(currentPos, newTargetVec);
        float newDuration = newDistance > 0 ? newDistance / Config.UfoFlightSpeed : 0.01f;

        _commands.Add(new UfoRetargetCommand
        {
            TileId = flight.TileId,
            NewTarget = newTargetVec,
            NewDuration = newDuration,
            StartTime = startTime,
            Duration = 0 // Instant command
        });

        // Update tracked flight info (StayFraction=0 for retarget segments)
        _activeUfoFlights[evt.ProjectileId] = new UfoFlightInfo(
            flight.TileId, startTime, newDuration, currentPos, newTargetVec, 0f);
    }

    /// <inheritdoc />
    public void Visit(ProjectileImpactEvent evt)
    {
        float startTime = GetStartTime(evt);
        var position = new Vector2(evt.ImpactPosition.X, evt.ImpactPosition.Y);

        // UFO impact: remove UFO tile, destroy target, lock cell
        if (_activeUfoFlights.TryGetValue(evt.ProjectileId, out var flight))
        {
            _activeUfoFlights.Remove(evt.ProjectileId);

            // Ensure removal doesn't fire before the visual flight animation completes.
            // Impact event may arrive in a different batch whose baseTime doesn't
            // perfectly align with the UfoLaunchCommand's EndTime.
            float flightEndTime = flight.LaunchTime + flight.Duration;
            float removeTime = Math.Max(startTime, flightEndTime);

            Console.WriteLine($"[UFO] Impact: tileId={flight.TileId} target={position} " +
                $"flightEnd={flightEndTime:F3} impactStart={startTime:F3} removeTime={removeTime:F3} " +
                $"flightOrigin={flight.Origin} flightTarget={flight.Target} flightDur={flight.Duration:F3}");

            // Impact effect at target
            _commands.Add(new ShowEffectCommand
            {
                EffectType = "ufo_impact",
                Position = position,
                StartTime = removeTime,
                Duration = 0.3f
            });

            // Remove UFO tile on arrival
            _commands.Add(new RemoveTileCommand
            {
                TileId = flight.TileId,
                StartTime = removeTime,
                Duration = 0,
                Priority = 10
            });

            // Lock target cell for impact — brief hold so the landing reads before tiles drop in
            int column = evt.ImpactPosition.X;
            _lockEntries.Add(new CellLockEntry
            {
                Position = evt.ImpactPosition,
                LockType = CellLockType.Receive,
                Duration = 0.15f,
                IsMerge = false
            });

            if (!_columnDestroyEndTimes.TryGetValue(column, out float existing) || startTime > existing)
            {
                _columnDestroyEndTimes[column] = startTime;
            }

            return;
        }

        // Non-UFO projectiles: standard impact handling
        var impactCommand = new ImpactProjectileCommand
        {
            ProjectileId = evt.ProjectileId,
            Position = position,
            EffectType = "projectile_explosion",
            StartTime = startTime,
            Duration = 0.3f
        };
        _commands.Add(impactCommand);

        // Remove projectile after impact
        var removeCommand = new RemoveProjectileCommand
        {
            ProjectileId = evt.ProjectileId,
            StartTime = startTime + 0.3f,
            Duration = 0
        };
        _commands.Add(removeCommand);
    }

    /// <inheritdoc />
    public void Visit(CoverDestroyedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        var destroyCommand = new DestroyCoverCommand
        {
            GridPos = evt.GridPosition,
            CoverType = evt.Type,
            StartTime = startTime,
            Duration = 0.25f
        };
        _commands.Add(destroyCommand);

        if (!evt.IsGoal)
        {
            var effectCommand = new ShowEffectCommand
            {
                EffectType = "cover_destroyed",
                Position = position,
                StartTime = startTime,
                Duration = 0.25f
            };
            _commands.Add(effectCommand);
        }
    }

    /// <inheritdoc />
    public void Visit(GroundDestroyedEvent evt)
    {
        float startTime = GetStartTime(evt);
        var position = new Vector2(evt.GridPosition.X, evt.GridPosition.Y);

        var destroyCommand = new DestroyGroundCommand
        {
            GridPos = evt.GridPosition,
            GroundType = evt.Type,
            StartTime = startTime,
            Duration = 0.25f
        };
        _commands.Add(destroyCommand);

        if (!evt.IsGoal)
        {
            var effectCommand = new ShowEffectCommand
            {
                EffectType = "ground_destroyed",
                Position = position,
                StartTime = startTime,
                Duration = 0.25f
            };
            _commands.Add(effectCommand);
        }
    }

    /// <inheritdoc />
    public void Visit(DeadlockDetectedEvent evt)
    {
        // Deadlock detection events don't generate render commands
        // UI can handle deadlock notification separately
    }

    /// <inheritdoc />
    public void Visit(BoardShuffledEvent evt)
    {
        float startTime = GetStartTime(evt);

        // Emit update commands for all changed tiles
        foreach (var change in evt.Changes)
        {
            var updateCommand = new UpdateTileTypeCommand
            {
                TileId = change.TileId,
                Position = change.Position,
                TileType = change.ToType,
                StartTime = startTime,
                Duration = 0 // Instant update
            };
            _commands.Add(updateCommand);
        }

        // Optional: Add visual effect for shuffle notification
        // UI can show a shuffle animation/notification if needed
    }

    /// <inheritdoc />
    public void Visit(ObjectiveProgressEvent evt)
    {
        // Objective progress events don't generate render commands
        // UI handles objective display via GameState directly
    }

    /// <inheritdoc />
    public void Visit(LevelCompletedEvent evt)
    {
        // Level completed events don't generate render commands
        // UI handles victory/defeat display separately
    }

    #endregion

    #region Cascade Timing

    /// <summary>
    /// Calculate the start time for a move animation considering cascading.
    /// Tiles should wait for destroyed tiles above them and for tiles below them to clear space.
    /// </summary>
    private float CalculateMoveStartTime(int column, int targetRow, float eventTime)
    {
        float startTime = eventTime;

        // Wait for destroy animations in this column at or above the target row
        if (_columnDestroyEndTimes.TryGetValue(column, out float destroyEndTime))
        {
            startTime = Math.Max(startTime, destroyEndTime);
        }

        // Wait for tiles below to clear 0.5 cells of space
        if (_columnMoves.TryGetValue(column, out var moves))
        {
            foreach (var move in moves)
            {
                // Check if this existing move ends at or below our target row
                if (move.TargetRow >= targetRow)
                {
                    // Calculate when this tile clears half a cell from its start
                    float totalDistance = move.To.Y - move.From.Y;
                    if (totalDistance > 0)
                    {
                        float halfCellRatio = Math.Min(0.5f / totalDistance, 1.0f);
                        float halfCellTime = move.StartTime + (move.EndTime - move.StartTime) * halfCellRatio;
                        startTime = Math.Max(startTime, halfCellTime);
                    }
                }
            }
        }

        return startTime;
    }

    private void TrackMove(int column, float startTime, float endTime, int targetRow, Vector2 from, Vector2 to)
    {
        if (!_columnMoves.TryGetValue(column, out var moves))
        {
            moves = new List<MoveRecord>();
            _columnMoves[column] = moves;
        }
        moves.Add(new MoveRecord(startTime, endTime, targetRow, from, to));
    }

    #endregion
}
