using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Match3.Core;
using Match3.Core.Config;
using Match3.Core.Systems.Core;
using Match3.Core.Systems.Generation;
using Match3.Core.Systems.Input;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.PowerUps;
using Match3.Core.View;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Models.Gameplay;
using Match3.Core.Utility;
using Match3.Core.Models.Input;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Core;

/// <summary>
/// Match3Engine 交互集成测试
///
/// 职责：
/// - 测试 Engine 与各系统的集成
/// - 测试点击炸弹激活
/// - 测试交换后是否正确回退无效交换
/// - 测试交换动画的触发
/// </summary>
public class Match3EngineInteractionTests
{
    #region Stub Classes

    private class StubAsyncGameLoopSystem : IAsyncGameLoopSystem
    {
        public bool ActivateBombCalled { get; private set; }
        public Position? LastBombPosition { get; private set; }

        public void Update(ref GameState state, float dt) { }

        public void ActivateBomb(ref GameState state, Position p)
        {
            ActivateBombCalled = true;
            LastBombPosition = p;
        }
    }

    private class StubInteractionSystem : IInteractionSystem
    {
        public string StatusMessage => "Stub";
        public bool ShouldSucceed { get; set; } = true;
        public Move? MoveToReturn { get; set; } = null;

        public bool TryHandleTap(ref GameState state, Position p, bool isInteractive, out Move? move)
        {
            move = MoveToReturn;
            return ShouldSucceed && MoveToReturn.HasValue;
        }

        public bool TryHandleSwipe(ref GameState state, Position from, Direction direction, bool isInteractive, out Move? move)
        {
            move = MoveToReturn;
            return ShouldSucceed && MoveToReturn.HasValue;
        }
    }

    private class StubAnimationSystem : IAnimationSystem
    {
        public bool IsVisuallyStable => true;
        public bool Animate(ref GameState state, float dt) => true;
        public bool IsVisualAtTarget(in GameState state, Position p) => true;
    }

    private class StubBoardInitializer : IBoardInitializer
    {
        public void Initialize(ref GameState state, LevelConfig? levelConfig)
        {
            // 初始化所有格子为红色
            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    var tile = new Tile(y * state.Width + x + 1, TileType.Red, x, y);
                    tile.Position = new Vector2(x, y);
                    state.SetTile(x, y, tile);
                }
            }
        }
    }

    private class StubGameView : IGameView
    {
        public void RenderBoard(TileType[,] board) { }
        public void ShowSwap(Position a, Position b, bool success) { }
        public void ShowMatches(IReadOnlyCollection<Position> matched) { }
        public void ShowGravity(IEnumerable<TileMove> moves) { }
        public void ShowRefill(IEnumerable<TileMove> moves) { }
    }

    private class StubLogger : IGameLogger
    {
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? ex = null) { }
        public void LogInfo<T>(string message, T args) { }
        public void LogInfo<T1, T2>(string message, T1 arg1, T2 arg2) { }
        public void LogInfo<T1, T2, T3>(string message, T1 arg1, T2 arg2, T3 arg3) { }
        public void LogWarning<T>(string message, T args) { }
    }

    private class StubRandom : IRandom
    {
        public int Next(int min, int max) => min;
    }

    private class StubMatchFinder : IMatchFinder
    {
        public bool HasMatchResult { get; set; } = true;
        public List<MatchGroup> FindMatchGroups(in GameState state, IEnumerable<Position>? foci = null) => new List<MatchGroup>();
        public bool HasMatchAt(in GameState state, Position p) => HasMatchResult;
        public bool HasMatches(in GameState state) => HasMatchResult;
    }

    private class StubBotSystem : IBotSystem
    {
        public bool TryGetRandomMove(ref GameState state, IInteractionSystem interactionSystem, out Move move)
        {
            move = default;
            return false;
        }
    }

    #endregion

    #region Bomb Activation Tests

    [Fact]
    public void OnTap_WithBomb_ShouldActivateBomb()
    {
        // Arrange
        var config = new Match3Config(8, 8, 5);
        var gameLoopStub = new StubAsyncGameLoopSystem();
        var interactionStub = new StubInteractionSystem();
        var animationStub = new StubAnimationSystem();
        var boardInitStub = new StubBoardInitializer();
        var matchFinderStub = new StubMatchFinder();
        var botSystemStub = new StubBotSystem();

        var random = new StubRandom();
        var view = new StubGameView();
        var logger = new StubLogger();

        var engine = new Match3Engine(
            config, random, view, logger,
            gameLoopStub, interactionStub, animationStub, boardInitStub, matchFinderStub, botSystemStub
        );

        // Inject a Bomb into State using Reflection
        var stateField = typeof(Match3Engine).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (GameState)stateField!.GetValue(engine)!;

        // Place a bomb at (0,0)
        var bombTile = new Tile(1, TileType.Red, 0, 0);
        bombTile.Bomb = BombType.Horizontal;
        state.SetTile(0, 0, bombTile);

        // Act
        engine.OnTap(new Position(0, 0));

        // Assert
        Assert.True(gameLoopStub.ActivateBombCalled, "ActivateBomb should be called when tapping a bomb");
        Assert.Equal(new Position(0, 0), gameLoopStub.LastBombPosition);
    }

    [Fact]
    public void OnTap_WithNormalTile_ShouldNotActivateBomb()
    {
        // Arrange
        var config = new Match3Config(8, 8, 5);
        var gameLoopStub = new StubAsyncGameLoopSystem();
        var interactionStub = new StubInteractionSystem();
        var animationStub = new StubAnimationSystem();
        var boardInitStub = new StubBoardInitializer();
        var matchFinderStub = new StubMatchFinder();
        var botSystemStub = new StubBotSystem();

        var random = new StubRandom();
        var view = new StubGameView();
        var logger = new StubLogger();

        var engine = new Match3Engine(
            config, random, view, logger,
            gameLoopStub, interactionStub, animationStub, boardInitStub, matchFinderStub, botSystemStub
        );

        // Act
        engine.OnTap(new Position(0, 0));

        // Assert
        Assert.False(gameLoopStub.ActivateBombCalled, "ActivateBomb should NOT be called when tapping a normal tile");
    }

    #endregion

    #region Swap Tests

    [Fact]
    public void OnSwipe_ShouldSwapTiles_WhenMatchFound()
    {
        // Arrange
        var config = new Match3Config(8, 8, 5);
        var gameLoopStub = new StubAsyncGameLoopSystem();
        var interactionStub = new StubInteractionSystem();
        var animationStub = new StubAnimationSystem();
        var boardInitStub = new StubBoardInitializer();
        var matchFinderStub = new StubMatchFinder { HasMatchResult = true }; // Simulate valid match
        var botSystemStub = new StubBotSystem();

        var random = new StubRandom();
        var view = new StubGameView();
        var logger = new StubLogger();

        // Setup Interaction to return a valid move
        interactionStub.MoveToReturn = new Move(new Position(0, 0), new Position(1, 0));

        var engine = new Match3Engine(
            config, random, view, logger,
            gameLoopStub, interactionStub, animationStub, boardInitStub, matchFinderStub, botSystemStub
        );

        // Inject tiles with different colors
        var stateField = typeof(Match3Engine).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (GameState)stateField!.GetValue(engine)!;

        var t1 = new Tile(1, TileType.Red, 0, 0);
        t1.Position = new Vector2(0, 0);
        var t2 = new Tile(2, TileType.Blue, 1, 0);
        t2.Position = new Vector2(1, 0);
        state.SetTile(0, 0, t1);
        state.SetTile(1, 0, t2);

        // Act - 直接调用 OnSwipe
        engine.OnSwipe(new Position(0, 0), Direction.Right);

        // Assert - 有 match，交换生效
        var tileAt00 = engine.State.GetTile(0, 0);
        var tileAt10 = engine.State.GetTile(1, 0);

        // Tiles should be swapped (Blue at 0,0, Red at 1,0)
        Assert.Equal(TileType.Blue, tileAt00.Type);
        Assert.Equal(TileType.Red, tileAt10.Type);
    }

    [Fact]
    public void OnSwipe_ShouldSwapBack_WhenNoMatchFound()
    {
        // Arrange
        var config = new Match3Config(8, 8, 5);
        var gameLoopStub = new StubAsyncGameLoopSystem();
        var interactionStub = new StubInteractionSystem();
        var animationStub = new StubAnimationSystem();
        var boardInitStub = new StubBoardInitializer();
        var matchFinderStub = new StubMatchFinder { HasMatchResult = false }; // No match
        var botSystemStub = new StubBotSystem();

        var random = new StubRandom();
        var view = new StubGameView();
        var logger = new StubLogger();

        // Setup Interaction to return a valid move
        interactionStub.MoveToReturn = new Move(new Position(0, 0), new Position(1, 0));

        var engine = new Match3Engine(
            config, random, view, logger,
            gameLoopStub, interactionStub, animationStub, boardInitStub, matchFinderStub, botSystemStub
        );

        // Inject tiles with different colors
        var stateField = typeof(Match3Engine).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (GameState)stateField!.GetValue(engine)!;

        var t1 = new Tile(1, TileType.Red, 0, 0);
        t1.Position = new Vector2(0, 0);
        var t2 = new Tile(2, TileType.Blue, 1, 0);
        t2.Position = new Vector2(1, 0);
        state.SetTile(0, 0, t1);
        state.SetTile(1, 0, t2);

        // Act - 直接调用 OnSwipe
        engine.OnSwipe(new Position(0, 0), Direction.Right);

        // 交换后立即检查 - tiles 应该已经交换（但还没验证回退）
        Assert.Equal(TileType.Blue, engine.State.GetTile(0, 0).Type);
        Assert.Equal(TileType.Red, engine.State.GetTile(1, 0).Type);

        // 运行 Update 来触发验证（StubAnimationSystem.IsVisualAtTarget 返回 true，所以立即验证）
        engine.Update(0.016f);

        // Assert - 无 match，交换回退
        var tileAt00 = engine.State.GetTile(0, 0);
        var tileAt10 = engine.State.GetTile(1, 0);

        // Tiles should be swapped back to original positions
        Assert.Equal(TileType.Red, tileAt00.Type);
        Assert.Equal(TileType.Blue, tileAt10.Type);
    }

    [Fact]
    public void OnSwipe_KeepsVisualPositions_ForSwapAnimation()
    {
        // Arrange
        var config = new Match3Config(8, 8, 5);
        var gameLoopStub = new StubAsyncGameLoopSystem();
        var interactionStub = new StubInteractionSystem();
        var animationStub = new StubAnimationSystem();
        var boardInitStub = new StubBoardInitializer();
        var matchFinderStub = new StubMatchFinder { HasMatchResult = true };
        var botSystemStub = new StubBotSystem();

        var random = new StubRandom();
        var view = new StubGameView();
        var logger = new StubLogger();

        // Setup Interaction to return a valid move
        interactionStub.MoveToReturn = new Move(new Position(0, 0), new Position(1, 0));

        var engine = new Match3Engine(
            config, random, view, logger,
            gameLoopStub, interactionStub, animationStub, boardInitStub, matchFinderStub, botSystemStub
        );

        // Inject tiles with specific visual positions
        var stateField = typeof(Match3Engine).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (GameState)stateField!.GetValue(engine)!;

        var t1 = new Tile(1, TileType.Red, 0, 0);
        t1.Position = new Vector2(0, 0);
        var t2 = new Tile(2, TileType.Blue, 1, 0);
        t2.Position = new Vector2(1, 0);
        state.SetTile(0, 0, t1);
        state.SetTile(1, 0, t2);

        // Act
        engine.OnSwipe(new Position(0, 0), Direction.Right);

        // Assert - 交换后视觉位置应保持不变（用于动画）
        var tileAt00 = engine.State.GetTile(0, 0); // 原来是 t2 (Blue)
        var tileAt10 = engine.State.GetTile(1, 0); // 原来是 t1 (Red)

        // 视觉位置应该保持原来的值，AnimationSystem 会将它们动画到新位置
        Assert.Equal(new Vector2(1, 0), tileAt00.Position); // Blue 的视觉位置仍在 (1,0)
        Assert.Equal(new Vector2(0, 0), tileAt10.Position); // Red 的视觉位置仍在 (0,0)
    }

    #endregion

    #region Vertical Swap Tests

    [Fact]
    public void OnSwipe_Vertical_ShouldSwapTiles_WhenMatchFound()
    {
        // Arrange
        var config = new Match3Config(8, 8, 5);
        var gameLoopStub = new StubAsyncGameLoopSystem();
        var interactionStub = new StubInteractionSystem();
        var animationStub = new StubAnimationSystem();
        var boardInitStub = new StubBoardInitializer();
        var matchFinderStub = new StubMatchFinder { HasMatchResult = true };
        var botSystemStub = new StubBotSystem();

        var random = new StubRandom();
        var view = new StubGameView();
        var logger = new StubLogger();

        // Setup Interaction to return a vertical move (down)
        interactionStub.MoveToReturn = new Move(new Position(0, 0), new Position(0, 1));

        var engine = new Match3Engine(
            config, random, view, logger,
            gameLoopStub, interactionStub, animationStub, boardInitStub, matchFinderStub, botSystemStub
        );

        // Inject tiles with different colors (vertically)
        var stateField = typeof(Match3Engine).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (GameState)stateField!.GetValue(engine)!;

        var t1 = new Tile(1, TileType.Red, 0, 0);
        t1.Position = new Vector2(0, 0);
        var t2 = new Tile(2, TileType.Blue, 0, 1);
        t2.Position = new Vector2(0, 1);
        state.SetTile(0, 0, t1);
        state.SetTile(0, 1, t2);

        // Act - 向下交换
        engine.OnSwipe(new Position(0, 0), Direction.Down);

        // Assert - 有 match，交换生效
        var tileAt00 = engine.State.GetTile(0, 0);
        var tileAt01 = engine.State.GetTile(0, 1);

        // Tiles should be swapped (Blue at 0,0, Red at 0,1)
        Assert.Equal(TileType.Blue, tileAt00.Type);
        Assert.Equal(TileType.Red, tileAt01.Type);
    }

    [Fact]
    public void OnSwipe_Vertical_ShouldSwapBack_WhenNoMatchFound()
    {
        // Arrange
        var config = new Match3Config(8, 8, 5);
        var gameLoopStub = new StubAsyncGameLoopSystem();
        var interactionStub = new StubInteractionSystem();
        var animationStub = new StubAnimationSystem();
        var boardInitStub = new StubBoardInitializer();
        var matchFinderStub = new StubMatchFinder { HasMatchResult = false }; // No match
        var botSystemStub = new StubBotSystem();

        var random = new StubRandom();
        var view = new StubGameView();
        var logger = new StubLogger();

        // Setup Interaction to return a vertical move (down)
        interactionStub.MoveToReturn = new Move(new Position(0, 0), new Position(0, 1));

        var engine = new Match3Engine(
            config, random, view, logger,
            gameLoopStub, interactionStub, animationStub, boardInitStub, matchFinderStub, botSystemStub
        );

        // Inject tiles with different colors (vertically)
        var stateField = typeof(Match3Engine).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (GameState)stateField!.GetValue(engine)!;

        var t1 = new Tile(1, TileType.Red, 0, 0);
        t1.Position = new Vector2(0, 0);
        var t2 = new Tile(2, TileType.Blue, 0, 1);
        t2.Position = new Vector2(0, 1);
        state.SetTile(0, 0, t1);
        state.SetTile(0, 1, t2);

        // Act - 向下交换
        engine.OnSwipe(new Position(0, 0), Direction.Down);

        // 交换后立即检查 - tiles 应该已经交换（但还没验证回退）
        Assert.Equal(TileType.Blue, engine.State.GetTile(0, 0).Type);
        Assert.Equal(TileType.Red, engine.State.GetTile(0, 1).Type);

        // 运行 Update 来触发验证
        engine.Update(0.016f);

        // Assert - 无 match，交换回退
        var tileAt00 = engine.State.GetTile(0, 0);
        var tileAt01 = engine.State.GetTile(0, 1);

        // Tiles should be swapped back to original positions
        Assert.Equal(TileType.Red, tileAt00.Type);
        Assert.Equal(TileType.Blue, tileAt01.Type);
    }

    [Fact]
    public void OnSwipe_Vertical_KeepsVisualPositions_ForSwapAnimation()
    {
        // Arrange
        var config = new Match3Config(8, 8, 5);
        var gameLoopStub = new StubAsyncGameLoopSystem();
        var interactionStub = new StubInteractionSystem();
        var animationStub = new StubAnimationSystem();
        var boardInitStub = new StubBoardInitializer();
        var matchFinderStub = new StubMatchFinder { HasMatchResult = true };
        var botSystemStub = new StubBotSystem();

        var random = new StubRandom();
        var view = new StubGameView();
        var logger = new StubLogger();

        // Setup Interaction to return a vertical move (down)
        interactionStub.MoveToReturn = new Move(new Position(0, 0), new Position(0, 1));

        var engine = new Match3Engine(
            config, random, view, logger,
            gameLoopStub, interactionStub, animationStub, boardInitStub, matchFinderStub, botSystemStub
        );

        // Inject tiles with specific visual positions (vertically)
        var stateField = typeof(Match3Engine).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (GameState)stateField!.GetValue(engine)!;

        var t1 = new Tile(1, TileType.Red, 0, 0);
        t1.Position = new Vector2(0, 0);
        var t2 = new Tile(2, TileType.Blue, 0, 1);
        t2.Position = new Vector2(0, 1);
        state.SetTile(0, 0, t1);
        state.SetTile(0, 1, t2);

        // Act - 向下交换
        engine.OnSwipe(new Position(0, 0), Direction.Down);

        // Assert - 交换后视觉位置应保持不变（用于动画）
        var tileAt00 = engine.State.GetTile(0, 0); // 原来是 t2 (Blue)
        var tileAt01 = engine.State.GetTile(0, 1); // 原来是 t1 (Red)

        // 视觉位置应该保持原来的值，AnimationSystem 会将它们动画到新位置
        Assert.Equal(new Vector2(0, 1), tileAt00.Position); // Blue 的视觉位置仍在 (0,1)
        Assert.Equal(new Vector2(0, 0), tileAt01.Position); // Red 的视觉位置仍在 (0,0)
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_ProcessesQueuedInputIntent()
    {
        // Arrange
        var config = new Match3Config(8, 8, 5);
        var gameLoopStub = new StubAsyncGameLoopSystem();
        var interactionStub = new StubInteractionSystem();
        var animationStub = new StubAnimationSystem();
        var boardInitStub = new StubBoardInitializer();
        var matchFinderStub = new StubMatchFinder { HasMatchResult = true };
        var botSystemStub = new StubBotSystem();

        var random = new StubRandom();
        var view = new StubGameView();
        var logger = new StubLogger();

        interactionStub.MoveToReturn = new Move(new Position(0, 0), new Position(1, 0));

        var engine = new Match3Engine(
            config, random, view, logger,
            gameLoopStub, interactionStub, animationStub, boardInitStub, matchFinderStub, botSystemStub
        );

        // Setup tiles
        var stateField = typeof(Match3Engine).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (GameState)stateField!.GetValue(engine)!;

        var t1 = new Tile(1, TileType.Red, 0, 0);
        t1.Position = new Vector2(0, 0);
        var t2 = new Tile(2, TileType.Blue, 1, 0);
        t2.Position = new Vector2(1, 0);
        state.SetTile(0, 0, t1);
        state.SetTile(1, 0, t2);

        // Act - 通过 EnqueueIntent 排队输入
        engine.EnqueueIntent(new SwipeIntent(new Position(0, 0), Direction.Right));
        engine.Update(0.016f);

        // Assert - 输入被处理，交换生效
        var tileAt00 = engine.State.GetTile(0, 0);
        var tileAt10 = engine.State.GetTile(1, 0);

        Assert.Equal(TileType.Blue, tileAt00.Type);
        Assert.Equal(TileType.Red, tileAt10.Type);
    }

    #endregion
}
