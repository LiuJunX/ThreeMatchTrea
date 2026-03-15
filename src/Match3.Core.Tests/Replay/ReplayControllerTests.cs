using System;
using System.Collections.Generic;
using Match3.Core.Commands;
using Match3.Core.Config;
using Match3.Core.DependencyInjection;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Replay;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Core.Systems.Spawning;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Replay;

public class ReplayControllerTests
{
    #region Helpers

    /// <summary>
    /// Stub IGameCommand that records when Execute is called and at which tick.
    /// </summary>
    private sealed record StubCommand : IGameCommand
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public int IssuedAtTick { get; init; }
        public int ExecuteCallCount { get; private set; }

        public bool Execute(SimulationEngine engine)
        {
            ExecuteCallCount++;
            return true;
        }

        public bool CanExecute(in GameState state) => true;
    }

    /// <summary>
    /// Minimal IGameServiceFactory that creates a real SimulationEngine
    /// with stub subsystems for test isolation.
    /// </summary>
    private sealed class StubGameServiceFactory : IGameServiceFactory
    {
        public SimulationEngine CreateSimulationEngine(
            GameState initialState,
            SimulationConfig config,
            IEventCollector? eventCollector = null)
        {
            var random = new StubRandom();
            var match3Config = new Match3Config(initialState.Width, initialState.Height, initialState.TileTypesCount);
            var physics = new RealtimeGravitySystem(match3Config, random);
            var refill = new RealtimeRefillSystem(new StubSpawnModel());
            var bombGenerator = new BombGenerator();
            var matchFinder = new ClassicMatchFinder(bombGenerator);
            var scoreSystem = new StubScoreSystem();
            var matchProcessor = new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), BombEffectRegistry.CreateDefault());
            var powerUpHandler = new PowerUpHandler(scoreSystem);

            var lockScheduler = new LockScheduler();
            var explosionSystem = new ExplosionSystem(
                new Match3.Core.Systems.Layers.CoverSystem(),
                new Match3.Core.Systems.Layers.GroundSystem(),
                null,
                lockScheduler);

            return new SimulationEngine(
                initialState,
                config,
                physics,
                refill,
                matchFinder,
                matchProcessor,
                powerUpHandler,
                null,
                eventCollector,
                explosionSystem);
        }

        public SimulationEngine CreateSimulationEngine(
            GameState initialState,
            SimulationConfig config,
            SeedManager seedManager,
            IEventCollector? eventCollector = null)
            => CreateSimulationEngine(initialState, config, eventCollector);

        public GameSession CreateGameSession(LevelConfig? levelConfig = null)
            => CreateGameSession(new GameServiceConfiguration(), levelConfig);

        public GameSession CreateGameSession(GameServiceConfiguration configuration, LevelConfig? levelConfig = null)
        {
            var seedManager = new SeedManager(configuration.RngSeed);
            var mainRng = seedManager.GetRandom(RandomDomain.Main);
            var state = new GameState(
                configuration.Width, configuration.Height,
                configuration.TileTypesCount, mainRng);

            // Fill board with checkerboard pattern (no random consumption needed for stub)
            for (int y = 0; y < state.Height; y++)
                for (int x = 0; x < state.Width; x++)
                {
                    var type = (x + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item3;
                    state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
                }

            var config = configuration.SimulationConfig ?? SimulationConfig.ForHumanPlay();
            var collector = new BufferedEventCollector();
            var engine = CreateSimulationEngine(state, config, collector);
            return new GameSession(engine, collector, seedManager, configuration);
        }

        public ISpawnModel CreateSpawnModel(IRandom random) => new StubSpawnModel();
        public ITileGenerator CreateTileGenerator(IRandom random) => throw new NotSupportedException();
        public IDeadlockDetectionSystem CreateDeadlockDetector(IMatchFinder matchFinder) => throw new NotSupportedException();
        public IBoardShuffleSystem CreateShuffleSystem(IDeadlockDetectionSystem deadlockDetector) => throw new NotSupportedException();
        public ILevelObjectiveSystem? CreateObjectiveSystem() => throw new NotSupportedException();
    }

    /// <summary>
    /// Creates a minimal GameStateSnapshot for a stable checkerboard board.
    /// </summary>
    private static GameStateSnapshot CreateStableSnapshot(int width = 8, int height = 8)
    {
        int size = width * height;
        var elementTypes = new ElementType[size];
        var coverLayers = new Cover[size];
        var groundLayers = new Ground[size];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                elementTypes[index] = (x + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item3;
                coverLayers[index] = default;
                groundLayers[index] = default;
            }
        }

        return new GameStateSnapshot
        {
            Width = width,
            Height = height,
            TileTypesCount = 6,
            TileTypes = elementTypes,
            CoverLayers = coverLayers,
            GroundLayers = groundLayers,
            NextTileId = size,
            Score = 0,
            MoveCount = 0
        };
    }

    /// <summary>
    /// Creates a GameRecording with the given commands and duration.
    /// </summary>
    private static GameRecording CreateRecording(
        IReadOnlyList<IGameCommand>? commands = null,
        int durationTicks = 120)
    {
        return new GameRecording
        {
            InitialState = CreateStableSnapshot(),
            RandomSeed = 42,
            Commands = commands ?? Array.Empty<IGameCommand>(),
            DurationTicks = durationTicks,
            FinalScore = 0,
            TotalMoves = 0
        };
    }

    private static ReplayController CreateController(
        GameRecording? recording = null,
        IGameServiceFactory? factory = null)
    {
        return new ReplayController(
            recording ?? CreateRecording(),
            factory ?? new StubGameServiceFactory());
    }

    #endregion

    #region Constructor

    [Fact]
    public void Constructor_NullRecording_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReplayController(null!, new StubGameServiceFactory()));
    }

    [Fact]
    public void Constructor_NullFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReplayController(CreateRecording(), null!));
    }

    [Fact]
    public void Constructor_InitialState_IsStopped()
    {
        var controller = CreateController();

        Assert.Equal(ReplayState.Stopped, controller.State);
    }

    [Fact]
    public void Constructor_InitialProperties()
    {
        var recording = CreateRecording(durationTicks: 200);
        var controller = new ReplayController(recording, new StubGameServiceFactory());

        Assert.Equal(0, controller.CurrentTick);
        Assert.Equal(200, controller.TotalTicks);
        Assert.Equal(0, controller.CommandsExecuted);
        Assert.Equal(0, controller.TotalCommands);
        Assert.Equal(0f, controller.Progress);
        Assert.Equal(1.0f, controller.PlaybackSpeed);
        Assert.Null(controller.Engine);
    }

    #endregion

    #region State Machine: Stopped -> Playing -> Paused -> Stopped

    [Fact]
    public void Play_FromStopped_TransitionsToPlaying()
    {
        var controller = CreateController();

        controller.Play();

        Assert.Equal(ReplayState.Playing, controller.State);
    }

    [Fact]
    public void Play_FromStopped_CreatesEngine()
    {
        var controller = CreateController();

        controller.Play();

        Assert.NotNull(controller.Engine);
    }

    [Fact]
    public void Pause_FromPlaying_TransitionsToPaused()
    {
        var controller = CreateController();
        controller.Play();

        controller.Pause();

        Assert.Equal(ReplayState.Paused, controller.State);
    }

    [Fact]
    public void Stop_FromPlaying_TransitionsToStopped()
    {
        var controller = CreateController();
        controller.Play();

        controller.Stop();

        Assert.Equal(ReplayState.Stopped, controller.State);
    }

    [Fact]
    public void Stop_ResetsPositionToBeginning()
    {
        var controller = CreateController();
        controller.Play();
        // Advance a bit
        controller.Tick(0.1f);

        controller.Stop();

        Assert.Equal(0, controller.CurrentTick);
        Assert.Equal(0, controller.CommandsExecuted);
    }

    [Fact]
    public void Stop_DisposesEngine()
    {
        var controller = CreateController();
        controller.Play();
        Assert.NotNull(controller.Engine);

        controller.Stop();

        Assert.Null(controller.Engine);
    }

    [Fact]
    public void Play_FromPaused_TransitionsToPlaying()
    {
        var controller = CreateController();
        controller.Play();
        controller.Pause();

        controller.Play();

        Assert.Equal(ReplayState.Playing, controller.State);
    }

    [Fact]
    public void FullCycle_StoppedPlayingPausedStopped()
    {
        var controller = CreateController();

        Assert.Equal(ReplayState.Stopped, controller.State);

        controller.Play();
        Assert.Equal(ReplayState.Playing, controller.State);

        controller.Pause();
        Assert.Equal(ReplayState.Paused, controller.State);

        controller.Play();
        Assert.Equal(ReplayState.Playing, controller.State);

        controller.Stop();
        Assert.Equal(ReplayState.Stopped, controller.State);
    }

    #endregion

    #region Play/Pause/Stop/TogglePause Transitions

    [Fact]
    public void Pause_WhenStopped_StaysStopped()
    {
        var controller = CreateController();

        controller.Pause();

        Assert.Equal(ReplayState.Stopped, controller.State);
    }

    [Fact]
    public void Pause_WhenPaused_StaysPaused()
    {
        var controller = CreateController();
        controller.Play();
        controller.Pause();

        controller.Pause();

        Assert.Equal(ReplayState.Paused, controller.State);
    }

    [Fact]
    public void Play_WhenAlreadyPlaying_StaysPlaying()
    {
        var controller = CreateController();
        controller.Play();

        controller.Play();

        Assert.Equal(ReplayState.Playing, controller.State);
    }

    [Fact]
    public void TogglePause_FromStopped_TransitionsToPlaying()
    {
        var controller = CreateController();

        controller.TogglePause();

        Assert.Equal(ReplayState.Playing, controller.State);
    }

    [Fact]
    public void TogglePause_FromPlaying_TransitionsToPaused()
    {
        var controller = CreateController();
        controller.Play();

        controller.TogglePause();

        Assert.Equal(ReplayState.Paused, controller.State);
    }

    [Fact]
    public void TogglePause_FromPaused_TransitionsToPlaying()
    {
        var controller = CreateController();
        controller.Play();
        controller.Pause();

        controller.TogglePause();

        Assert.Equal(ReplayState.Playing, controller.State);
    }

    [Fact]
    public void Stop_WhenAlreadyStopped_RemainsStoppedAndEngineNull()
    {
        var controller = CreateController();

        controller.Stop();

        Assert.Equal(ReplayState.Stopped, controller.State);
        Assert.Null(controller.Engine);
    }

    #endregion

    #region Tick Advances Playback

    [Fact]
    public void Tick_WhenPlaying_AdvancesCurrentTick()
    {
        var controller = CreateController(CreateRecording(durationTicks: 600));
        controller.Play();

        // One frame at 60fps = 1/60 second should advance 1 tick
        controller.Tick(1f / 60f);

        Assert.True(controller.CurrentTick > 0);
    }

    [Fact]
    public void Tick_WhenStopped_DoesNotAdvance()
    {
        var controller = CreateController();

        controller.Tick(0.1f);

        Assert.Equal(0, controller.CurrentTick);
    }

    [Fact]
    public void Tick_WhenPaused_DoesNotAdvance()
    {
        var controller = CreateController(CreateRecording(durationTicks: 600));
        controller.Play();
        controller.Pause();
        var tickBefore = controller.CurrentTick;

        controller.Tick(0.1f);

        Assert.Equal(tickBefore, controller.CurrentTick);
    }

    [Fact]
    public void Tick_PlaybackSpeed_AffectsAdvancement()
    {
        var recording = CreateRecording(durationTicks: 6000);
        var controller = CreateController(recording);
        controller.PlaybackSpeed = 2.0f;
        controller.Play();

        // With 2x speed, should advance roughly twice as many ticks
        controller.Tick(1f / 60f);
        var ticksAt2x = controller.CurrentTick;

        controller.Stop();
        controller.PlaybackSpeed = 1.0f;
        controller.Play();
        controller.Tick(1f / 60f);
        var ticksAt1x = controller.CurrentTick;

        // At 2x speed, should have approximately double the ticks
        Assert.True(ticksAt2x >= ticksAt1x,
            $"2x speed ticks ({ticksAt2x}) should be >= 1x speed ticks ({ticksAt1x})");
    }

    [Fact]
    public void Tick_WhenDisposed_DoesNotAdvance()
    {
        var controller = CreateController(CreateRecording(durationTicks: 600));
        controller.Play();
        controller.Dispose();

        controller.Tick(0.1f);

        // Should not throw and should not advance
        Assert.Equal(0, controller.CurrentTick);
    }

    #endregion

    #region Seek Forward and Backward

    [Fact]
    public void Seek_ForwardToMiddle_AdvancesPosition()
    {
        var recording = CreateRecording(
            commands: new IGameCommand[]
            {
                new StubCommand { IssuedAtTick = 10 },
                new StubCommand { IssuedAtTick = 50 },
                new StubCommand { IssuedAtTick = 100 }
            },
            durationTicks: 120);
        var controller = CreateController(recording);
        controller.Play();

        controller.Seek(0.5f);

        Assert.True(controller.CurrentTick >= 50,
            $"After seeking to 50%, tick should be >= 50 but was {controller.CurrentTick}");
    }

    [Fact]
    public void Seek_BackwardFromMiddle_ResetsAndReplays()
    {
        var recording = CreateRecording(
            commands: new IGameCommand[]
            {
                new StubCommand { IssuedAtTick = 10 },
                new StubCommand { IssuedAtTick = 50 }
            },
            durationTicks: 120);
        var controller = CreateController(recording);
        controller.Play();

        // Seek forward first
        controller.Seek(0.8f);
        var tickAfterForward = controller.CurrentTick;

        // Seek backward
        controller.Seek(0.1f);

        Assert.True(controller.CurrentTick < tickAfterForward,
            $"After backward seek, tick ({controller.CurrentTick}) should be < forward tick ({tickAfterForward})");
    }

    [Fact]
    public void Seek_ToZero_ResetsToBeginning()
    {
        var controller = CreateController(CreateRecording(durationTicks: 120));
        controller.Play();
        controller.Seek(0.5f);

        controller.Seek(0f);

        Assert.Equal(0, controller.CurrentTick);
    }

    [Fact]
    public void Seek_ToOne_AdvancesThroughAllCommands()
    {
        // Seek fast-forwards through commands up to the target tick.
        // The while loop exits when all commands are processed OR target tick is reached.
        var commands = new IGameCommand[]
        {
            new StubCommand { IssuedAtTick = 10 },
            new StubCommand { IssuedAtTick = 50 }
        };
        var recording = CreateRecording(commands: commands, durationTicks: 120);
        var controller = CreateController(recording);
        controller.Play();

        controller.Seek(1.0f);

        // All commands should have been executed
        Assert.Equal(2, controller.CommandsExecuted);
    }

    [Fact]
    public void Seek_ClampsAboveOne_ToValidRange()
    {
        // Seek(2.0f) should be clamped to Seek(1.0f)
        var commands = new IGameCommand[]
        {
            new StubCommand { IssuedAtTick = 5 },
            new StubCommand { IssuedAtTick = 10 }
        };
        var recording = CreateRecording(commands: commands, durationTicks: 20);
        var controller = CreateController(recording);
        controller.Play();

        controller.Seek(2.0f);

        // All commands should be processed despite out-of-range progress value
        Assert.Equal(2, controller.CommandsExecuted);
    }

    [Fact]
    public void Seek_ClampsBelowZero()
    {
        var controller = CreateController(CreateRecording(durationTicks: 120));
        controller.Play();
        controller.Seek(0.5f);

        controller.Seek(-1.0f);

        Assert.Equal(0, controller.CurrentTick);
    }

    [Fact]
    public void Seek_WhenDisposed_DoesNothing()
    {
        var controller = CreateController(CreateRecording(durationTicks: 120));
        controller.Play();
        controller.Dispose();

        // Should not throw
        controller.Seek(0.5f);
    }

    #endregion

    #region StepForward

    [Fact]
    public void StepForward_FromStopped_InitializesAndPauses()
    {
        var commands = new IGameCommand[]
        {
            new StubCommand { IssuedAtTick = 5 }
        };
        var controller = CreateController(CreateRecording(commands: commands, durationTicks: 120));

        controller.StepForward();

        Assert.Equal(ReplayState.Paused, controller.State);
        Assert.NotNull(controller.Engine);
    }

    [Fact]
    public void StepForward_ExecutesOneCommand()
    {
        var cmd1 = new StubCommand { IssuedAtTick = 5 };
        var cmd2 = new StubCommand { IssuedAtTick = 15 };
        var commands = new IGameCommand[] { cmd1, cmd2 };
        var controller = CreateController(CreateRecording(commands: commands, durationTicks: 120));

        controller.StepForward();

        Assert.Equal(1, controller.CommandsExecuted);
    }

    [Fact]
    public void StepForward_SetsCurrentTickToCommandTick()
    {
        var cmd = new StubCommand { IssuedAtTick = 42 };
        var commands = new IGameCommand[] { cmd };
        var controller = CreateController(CreateRecording(commands: commands, durationTicks: 120));

        controller.StepForward();

        Assert.Equal(42, controller.CurrentTick);
    }

    [Fact]
    public void StepForward_MultipleSteps_ExecutesCommandsSequentially()
    {
        var cmd1 = new StubCommand { IssuedAtTick = 5 };
        var cmd2 = new StubCommand { IssuedAtTick = 15 };
        var cmd3 = new StubCommand { IssuedAtTick = 25 };
        var commands = new IGameCommand[] { cmd1, cmd2, cmd3 };
        var controller = CreateController(CreateRecording(commands: commands, durationTicks: 120));

        controller.StepForward();
        Assert.Equal(1, controller.CommandsExecuted);
        Assert.Equal(5, controller.CurrentTick);

        controller.StepForward();
        Assert.Equal(2, controller.CommandsExecuted);
        Assert.Equal(15, controller.CurrentTick);

        controller.StepForward();
        Assert.Equal(3, controller.CommandsExecuted);
        Assert.Equal(25, controller.CurrentTick);
    }

    [Fact]
    public void StepForward_BeyondLastCommand_DoesNotAdvance()
    {
        var cmd = new StubCommand { IssuedAtTick = 5 };
        var commands = new IGameCommand[] { cmd };
        var controller = CreateController(CreateRecording(commands: commands, durationTicks: 120));

        controller.StepForward(); // executes cmd
        var tickAfterStep = controller.CurrentTick;
        var executedAfterStep = controller.CommandsExecuted;

        controller.StepForward(); // no more commands

        Assert.Equal(tickAfterStep, controller.CurrentTick);
        Assert.Equal(executedAfterStep, controller.CommandsExecuted);
    }

    [Fact]
    public void StepForward_WhenDisposed_DoesNothing()
    {
        var commands = new IGameCommand[] { new StubCommand { IssuedAtTick = 5 } };
        var controller = CreateController(CreateRecording(commands: commands, durationTicks: 120));
        controller.Dispose();

        controller.StepForward();

        Assert.Equal(0, controller.CommandsExecuted);
    }

    #endregion

    #region Playback Completion Event

    [Fact]
    public void PlaybackCompleted_FiredWhenReachingEnd()
    {
        // Create a recording with very short duration and no commands
        var recording = CreateRecording(durationTicks: 2);
        var controller = CreateController(recording);
        bool completedFired = false;
        controller.PlaybackCompleted += () => completedFired = true;
        controller.Play();

        // Tick enough to reach the end (2 ticks at 60fps = ~0.033s)
        controller.Tick(1.0f); // large enough deltaTime to exceed duration

        Assert.True(completedFired, "PlaybackCompleted should have been fired");
        Assert.Equal(ReplayState.Completed, controller.State);
    }

    [Fact]
    public void PlaybackCompleted_NotFiredBeforeEnd()
    {
        var recording = CreateRecording(durationTicks: 6000);
        var controller = CreateController(recording);
        bool completedFired = false;
        controller.PlaybackCompleted += () => completedFired = true;
        controller.Play();

        controller.Tick(1f / 60f); // one tick

        Assert.False(completedFired);
        Assert.NotEqual(ReplayState.Completed, controller.State);
    }

    #endregion

    #region CommandExecuted Event

    [Fact]
    public void CommandExecuted_FiredForEachCommand()
    {
        var cmd1 = new StubCommand { IssuedAtTick = 0 };
        var cmd2 = new StubCommand { IssuedAtTick = 0 };
        var commands = new IGameCommand[] { cmd1, cmd2 };
        var recording = CreateRecording(commands: commands, durationTicks: 120);
        var controller = CreateController(recording);

        var executedCommands = new List<IGameCommand>();
        controller.CommandExecuted += cmd => executedCommands.Add(cmd);
        controller.Play();

        // Tick enough to process commands at tick 0
        controller.Tick(1f / 60f);

        Assert.Equal(2, executedCommands.Count);
    }

    [Fact]
    public void CommandExecuted_FiredOnStepForward()
    {
        var cmd = new StubCommand { IssuedAtTick = 5 };
        var commands = new IGameCommand[] { cmd };
        var recording = CreateRecording(commands: commands, durationTicks: 120);
        var controller = CreateController(recording);

        IGameCommand? executedCmd = null;
        controller.CommandExecuted += c => executedCmd = c;

        controller.StepForward();

        Assert.NotNull(executedCmd);
        Assert.Same(cmd, executedCmd);
    }

    #endregion

    #region Dispose Cleanup

    [Fact]
    public void Dispose_SetsDisposedState()
    {
        var controller = CreateController();
        controller.Play();

        controller.Dispose();

        // Engine should be cleaned up
        Assert.Null(controller.Engine);
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var controller = CreateController();
        controller.Play();

        controller.Dispose();
        controller.Dispose(); // should not throw
    }

    [Fact]
    public void Play_AfterDispose_DoesNotCreateEngine()
    {
        var controller = CreateController();
        controller.Dispose();

        controller.Play();

        Assert.Null(controller.Engine);
    }

    [Fact]
    public void Stop_AfterDispose_DoesNotThrow()
    {
        var controller = CreateController();
        controller.Dispose();

        controller.Stop(); // should not throw
    }

    #endregion

    #region Progress Property

    [Fact]
    public void Progress_InitiallyZero()
    {
        var controller = CreateController(CreateRecording(durationTicks: 120));

        Assert.Equal(0f, controller.Progress);
    }

    [Fact]
    public void Progress_ZeroDuration_ReturnsZero()
    {
        var recording = CreateRecording(durationTicks: 0);
        var controller = CreateController(recording);

        Assert.Equal(0f, controller.Progress);
    }

    [Fact]
    public void Progress_AfterSeek_ReflectsPosition()
    {
        // Create commands spanning the full duration so Seek can advance
        var commands = new List<IGameCommand>();
        for (int i = 0; i < 100; i++)
        {
            commands.Add(new StubCommand { IssuedAtTick = i });
        }
        var recording = CreateRecording(commands: commands, durationTicks: 100);
        var controller = CreateController(recording);
        controller.Play();

        controller.Seek(0.5f);

        Assert.True(controller.Progress >= 0.4f && controller.Progress <= 0.6f,
            $"Progress should be approximately 0.5 but was {controller.Progress}");
    }

    #endregion

    #region TotalCommands Property

    [Fact]
    public void TotalCommands_ReflectsRecording()
    {
        var commands = new IGameCommand[]
        {
            new StubCommand { IssuedAtTick = 1 },
            new StubCommand { IssuedAtTick = 2 },
            new StubCommand { IssuedAtTick = 3 }
        };
        var recording = CreateRecording(commands: commands, durationTicks: 120);
        var controller = CreateController(recording);

        Assert.Equal(3, controller.TotalCommands);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyRecording_PlayAndTickToCompletion()
    {
        var recording = CreateRecording(commands: Array.Empty<IGameCommand>(), durationTicks: 3);
        var controller = CreateController(recording);
        bool completedFired = false;
        controller.PlaybackCompleted += () => completedFired = true;

        controller.Play();
        // Tick enough to complete (3 ticks at 60fps)
        controller.Tick(1.0f);

        Assert.True(completedFired);
        Assert.Equal(ReplayState.Completed, controller.State);
    }

    [Fact]
    public void StepForward_EmptyRecording_NoAdvance()
    {
        var recording = CreateRecording(commands: Array.Empty<IGameCommand>(), durationTicks: 120);
        var controller = CreateController(recording);

        controller.StepForward();

        Assert.Equal(0, controller.CommandsExecuted);
        Assert.Equal(ReplayState.Paused, controller.State);
    }

    [Fact]
    public void Stop_ThenPlay_RestartsFromBeginning()
    {
        var commands = new IGameCommand[]
        {
            new StubCommand { IssuedAtTick = 0 }
        };
        var recording = CreateRecording(commands: commands, durationTicks: 600);
        var controller = CreateController(recording);

        controller.Play();
        controller.Tick(0.1f);
        var tickMidway = controller.CurrentTick;
        Assert.True(tickMidway > 0);

        controller.Stop();
        controller.Play();

        Assert.Equal(0, controller.CurrentTick);
        Assert.Equal(0, controller.CommandsExecuted);
    }

    [Fact]
    public void PlaybackSpeed_CanBeChanged()
    {
        var controller = CreateController();

        controller.PlaybackSpeed = 3.0f;

        Assert.Equal(3.0f, controller.PlaybackSpeed);
    }

    #endregion

    #region Seek With Engine Ticking

    [Fact]
    public void Seek_InitializesEngineIfStopped()
    {
        var controller = CreateController(CreateRecording(durationTicks: 120));
        Assert.Null(controller.Engine);

        controller.Seek(0.5f);

        Assert.NotNull(controller.Engine);
    }

    [Fact]
    public void Seek_ForwardThenBackward_ResetsAndReplays()
    {
        var commands = new IGameCommand[]
        {
            new StubCommand { IssuedAtTick = 10 },
            new StubCommand { IssuedAtTick = 50 }
        };
        var recording = CreateRecording(commands: commands, durationTicks: 120);
        var controller = CreateController(recording);

        // Seek forward
        controller.Seek(0.8f);
        Assert.Equal(2, controller.CommandsExecuted);

        // Seek backward
        controller.Seek(0.05f);
        Assert.True(controller.CurrentTick < 50,
            $"After backward seek, tick should be < 50 but was {controller.CurrentTick}");
    }

    [Fact]
    public void Seek_ToEnd_ExecutesAllCommands()
    {
        var commands = new IGameCommand[]
        {
            new StubCommand { IssuedAtTick = 5 },
            new StubCommand { IssuedAtTick = 10 },
            new StubCommand { IssuedAtTick = 20 }
        };
        var recording = CreateRecording(commands: commands, durationTicks: 30);
        var controller = CreateController(recording);

        controller.Seek(1.0f);

        Assert.Equal(3, controller.CommandsExecuted);
        Assert.Equal(30, controller.CurrentTick);
    }

    [Fact]
    public void Seek_MultipleForwardSeeks_WorkCorrectly()
    {
        var commands = new IGameCommand[]
        {
            new StubCommand { IssuedAtTick = 10 },
            new StubCommand { IssuedAtTick = 50 },
            new StubCommand { IssuedAtTick = 100 }
        };
        var recording = CreateRecording(commands: commands, durationTicks: 120);
        var controller = CreateController(recording);

        controller.Seek(0.2f);
        Assert.Equal(1, controller.CommandsExecuted);

        controller.Seek(0.5f);
        Assert.Equal(2, controller.CommandsExecuted);

        controller.Seek(0.9f);
        Assert.Equal(3, controller.CommandsExecuted);
    }

    #endregion
}
