using System;
using Match3.Core.AI;
using Match3.Core.Config;
using Match3.Core.Events;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Simulation;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.Matching.Generation;
using Match3.Core.Systems.Physics;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Objectives;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Systems.Scoring;
using Match3.Random;

namespace Match3.Core.Analysis;

/// <summary>
/// 共享模拟上下文 - 用于高效的模拟和预览
/// 线程不安全，每个线程应该有自己的实例
/// </summary>
internal sealed class SharedSimulationContext : IDisposable
{
    public readonly Match3Config Config = new();
    public readonly XorShift64 StateRandom = new();
    public readonly XorShift64 MoveRandom = new();
    public readonly BombGenerator BombGenerator = new();
    public readonly BombEffectRegistry BombEffects = BombEffectRegistry.CreateDefault();

    private readonly SimpleScoreSystem _scoreSystem = new();
    private int _tileTypesCount;

    // 缓存的组件 - 主模拟用
    private RealtimeGravitySystem? _physics;
    private AnalysisSpawnModel? _spawnModel;
    private RealtimeRefillSystem? _refill;
    private ClassicMatchFinder? _matchFinder;
    private StandardMatchProcessor? _matchProcessor;
    private BombResolution? _powerUpHandler;

    // 预览状态缓存
    private GameState _previewState;

    public SharedSimulationContext(int tileTypesCount = 6)
    {
        _tileTypesCount = tileTypesCount;
    }

    /// <summary>
    /// 重置上下文用于新的模拟
    /// </summary>
    public void ResetForSimulation(ulong seed, int tileTypesCount)
    {
        StateRandom.SetState(seed);
        MoveRandom.SetState(seed + 1);
        _tileTypesCount = tileTypesCount;
        _spawnModel?.Reset(tileTypesCount);
    }

    public RealtimeGravitySystem GetPhysics() =>
        _physics ??= new RealtimeGravitySystem(Config, MoveRandom);

    public ClassicMatchFinder GetMatchFinder() =>
        _matchFinder ??= new ClassicMatchFinder(BombGenerator);

    public StandardMatchProcessor GetMatchProcessor()
    {
        if (_matchProcessor == null)
        {
            var objSys = CreateObjectiveSystem(); // Use new instance or shared? Shared context seems to create new ObjectiveSystem for preview, but main simulation might need one.
            // Wait, CreateObjectiveSystem returns a NEW one.
            // But GetMatchProcessor is used for the "main" simulation context.
            // I should probably use a persistent one if this context is long-lived.
            // But SharedSimulationContext says "thread unsafe, each thread should have its own".
            // So I can create one and store it?
            // But CreateObjectiveSystem() is public.
            // Let's create one here locally or use null if not needed?
            // StandardMatchProcessor needs CoverSystem and GroundSystem.
            // They need ObjectiveSystem.
            var obj = CreateObjectiveSystem();
            var cover = new Systems.Layers.CoverSystem(obj);
            var ground = new Systems.Layers.GroundSystem(obj);
            _matchProcessor = new StandardMatchProcessor(_scoreSystem, cover, ground, BombEffects);
        }
        return _matchProcessor;
    }

    public BombResolution GetPowerUpHandler() =>
        _powerUpHandler ??= new BombResolution(_scoreSystem);

    private AnalysisSpawnModel GetSpawnModel()
    {
        _spawnModel ??= new AnalysisSpawnModel(_tileTypesCount);
        return _spawnModel;
    }

    public RealtimeRefillSystem GetRefill() =>
        _refill ??= new RealtimeRefillSystem(GetSpawnModel());

    /// <summary>
    /// 创建新的 LevelObjectiveSystem（每次模拟需要新实例以避免状态污染）
    /// </summary>
    public LevelObjectiveSystem CreateObjectiveSystem() => new();

    /// <summary>
    /// 创建完整的模拟引擎
    /// </summary>
    public SimulationEngine CreateEngine(GameState state, LevelObjectiveSystem objectiveSystem)
    {
        return CreateEngineForPreview(state, objectiveSystem);
    }

    /// <summary>
    /// 构建预览/分析用 SimulationEngine 的统一入口，封装 CoverSystem、GroundSystem、ExplosionSystem 的创建
    /// </summary>
    private SimulationEngine CreateEngineForPreview(GameState state, LevelObjectiveSystem objectiveSystem)
    {
        var coverSystem = new Systems.Layers.CoverSystem(objectiveSystem);
        var groundSystem = new Systems.Layers.GroundSystem(objectiveSystem);
        var obstacleSystem = new ObstacleSystem(objectiveSystem);
        var explosionSystem = new ExplosionSystem(coverSystem, groundSystem, objectiveSystem);
        var cellEliminator = new Systems.Elimination.CellEliminator(coverSystem, groundSystem, objectiveSystem, obstacleSystem);

        return new SimulationEngine(
            state,
            SimulationConfig.ForAI(),
            GetPhysics(),
            GetRefill(),
            GetMatchFinder(),
            GetMatchProcessor(),
            GetPowerUpHandler(),
            null,
            NullEventCollector.Instance,
            explosionSystem,
            null,
            null,
            objectiveSystem,
            cellEliminator: cellEliminator);
    }

    /// <summary>
    /// 快速预览 move 的结果
    /// 复用内部引擎以提高性能
    /// </summary>
    public MovePreview PreviewMove(in GameState currentState, Position from, Position to)
    {
        // 克隆状态用于预览
        _previewState = currentState.Clone(StateRandom);

        // 创建临时的 ObjectiveSystem（不影响主游戏状态）
        var objectiveSystem = new LevelObjectiveSystem();

        // 从当前状态复制目标进度
        for (int i = 0; i < 4; i++)
        {
            _previewState.ObjectiveProgress[i] = currentState.ObjectiveProgress[i];
        }

        // 创建临时引擎执行预览
        using var engine = CreateEngineForPreview(_previewState, objectiveSystem);

        int scoreBefore = _previewState.Score;
        int tilesBefore = AnalysisUtility.CountTiles(in _previewState);

        engine.ApplyMove(from, to);
        engine.RunUntilStable();

        var stateAfter = engine.State;
        int tilesAfter = AnalysisUtility.CountTiles(in stateAfter);

        return new MovePreview
        {
            Move = new Move { From = from, To = to },
            ScoreGained = stateAfter.Score - scoreBefore,
            TilesCleared = Math.Max(0, tilesBefore - tilesAfter),
            FinalState = stateAfter
        };
    }

    /// <summary>
    /// 快速预览 move - 简化版本，只返回基本信息
    /// 用于 MCTS rollout 等不需要完整信息的场景
    /// </summary>
    public (int scoreGained, int tilesCleared, bool isValid) QuickPreviewMove(
        in GameState currentState, Position from, Position to)
    {
        _previewState = currentState.Clone(StateRandom);

        var objectiveSystem = new LevelObjectiveSystem();

        using var engine = CreateEngineForPreview(_previewState, objectiveSystem);

        int scoreBefore = _previewState.Score;
        int tilesBefore = AnalysisUtility.CountTiles(in _previewState);

        engine.ApplyMove(from, to);
        engine.RunUntilStable();

        var stateAfter = engine.State;
        int tilesAfter = AnalysisUtility.CountTiles(in stateAfter);
        int tilesCleared = Math.Max(0, tilesBefore - tilesAfter);

        return (stateAfter.Score - scoreBefore, tilesCleared, tilesCleared > 0);
    }

    /// <summary>
    /// 应用 move 并返回新状态
    /// </summary>
    public GameState ApplyMoveAndGetNewState(in GameState state, Position from, Position to, LevelObjectiveSystem objectiveSystem)
    {
        var newState = state.Clone(StateRandom);

        using var engine = CreateEngineForPreview(newState, objectiveSystem);

        engine.ApplyMove(from, to);
        engine.RunUntilStable();

        return engine.State;
    }

    public void Dispose()
    {
        // 目前没有需要释放的资源
        // 保留 IDisposable 以便未来扩展
    }

    /// <summary>
    /// 分析专用的生成模型
    /// </summary>
    private sealed class AnalysisSpawnModel : Systems.Spawning.ISpawnModel
    {
        private int _typeCount;
        private int _counter;

        public AnalysisSpawnModel(int typeCount) => _typeCount = typeCount;

        public void Reset(int typeCount)
        {
            _typeCount = typeCount;
            _counter = 0;
        }

        public ElementType Predict(ref GameState state, int spawnX, in Systems.Spawning.SpawnContext context)
        {
            int idx = (_counter++ + spawnX) % _typeCount;
            // Map index to ElementType (1-based, assuming Item1...Item6)
            return (ElementType)(idx + 1);
        }
    }

    private sealed class SimpleScoreSystem : IScoreSystem
    {
        public int CalculateMatchScore(Models.Gameplay.MatchGroup match) => match.Positions.Count * 10;
        public int CalculateSpecialMoveScore(ElementType t1, ElementType t2) => 100;
    }
}
