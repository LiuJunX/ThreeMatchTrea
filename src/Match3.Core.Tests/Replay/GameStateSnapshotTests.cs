using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Replay;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Replay;

public class GameStateSnapshotTests
{
    #region Helpers

    private static GameState CreateTestState(
        int width = 4, int height = 4,
        int score = 100, int moveCount = 5,
        int moveLimit = 20, float targetDifficulty = 0.7f,
        LevelStatus levelStatus = LevelStatus.InProgress)
    {
        var random = new StubRandom();
        var state = new GameState(width, height, 6, random)
        {
            Score = score,
            MoveCount = moveCount,
            MoveLimit = moveLimit,
            TargetDifficulty = targetDifficulty,
            LevelStatus = levelStatus
        };

        // Fill with a non-matching pattern
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var type = (x + y) % 2 == 0 ? ElementType.Item1 : ElementType.Item3;
                state.SetTile(x, y, new Tile(state.NextTileId++, type, x, y));
            }
        }

        return state;
    }

    #endregion

    #region FromState/ToState Round-Trip: Basic Fields

    [Fact]
    public void RoundTrip_PreservesWidthAndHeight()
    {
        var state = CreateTestState(width: 5, height: 7);
        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        Assert.Equal(5, restored.Width);
        Assert.Equal(7, restored.Height);
    }

    [Fact]
    public void RoundTrip_PreservesScore()
    {
        var state = CreateTestState(score: 1234);
        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        Assert.Equal(1234, restored.Score);
    }

    [Fact]
    public void RoundTrip_PreservesMoveCount()
    {
        var state = CreateTestState(moveCount: 15);
        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        Assert.Equal(15, restored.MoveCount);
    }

    [Fact]
    public void RoundTrip_PreservesMoveLimit()
    {
        var state = CreateTestState(moveLimit: 30);
        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        Assert.Equal(30, restored.MoveLimit);
    }

    [Fact]
    public void RoundTrip_PreservesTargetDifficulty()
    {
        var state = CreateTestState(targetDifficulty: 0.85f);
        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        Assert.Equal(0.85f, restored.TargetDifficulty);
    }

    [Fact]
    public void RoundTrip_PreservesLevelStatus()
    {
        var state = CreateTestState(levelStatus: LevelStatus.Victory);
        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        Assert.Equal(LevelStatus.Victory, restored.LevelStatus);
    }

    #endregion

    #region FromState/ToState Round-Trip: Tile Layer

    [Fact]
    public void RoundTrip_PreservesTileTypes()
    {
        var state = CreateTestState();
        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                Assert.Equal(
                    state.GetType(x, y),
                    restored.GetType(x, y));
            }
        }
    }

    [Fact]
    public void RoundTrip_PreservesBombTypes()
    {
        var random = new StubRandom();
        var state = new GameState(3, 3, 6, random);
        state.SetTile(0, 0, new Tile(1, ElementType.HorizontalRocket, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.ColorBomb, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Ufo, 2, 0));

        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        Assert.Equal(ElementType.HorizontalRocket, restored.GetType(0, 0));
        Assert.Equal(ElementType.ColorBomb, restored.GetType(1, 0));
        Assert.Equal(ElementType.Ufo, restored.GetType(2, 0));
    }

    #endregion

    #region FromState/ToState Round-Trip: Cover and Ground Layers

    [Fact]
    public void RoundTrip_PreservesCoverLayer()
    {
        var state = CreateTestState();
        state.SetCover(0, 0, new Cover(CoverType.Cage, 2, false));
        state.SetCover(1, 1, new Cover(CoverType.Bubble, 1, true));

        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        var cover00 = restored.GetCover(0, 0);
        Assert.Equal(CoverType.Cage, cover00.Type);
        Assert.Equal(2, cover00.Health);
        Assert.False(cover00.IsDynamic);

        var cover11 = restored.GetCover(1, 1);
        Assert.Equal(CoverType.Bubble, cover11.Type);
        Assert.True(cover11.IsDynamic);
    }

    [Fact]
    public void RoundTrip_PreservesGroundLayer()
    {
        var state = CreateTestState();
        state.SetGround(2, 2, new Ground(GroundType.Ice, 3));

        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        var ground = restored.GetGround(2, 2);
        Assert.Equal(GroundType.Ice, ground.Type);
        Assert.Equal(3, ground.Health);
    }

    #endregion

    #region FromState/ToState Round-Trip: Cells

    [Fact]
    public void RoundTrip_PreservesCells()
    {
        var state = CreateTestState();
        state.SetCell(0, 0, CellKind.Void);
        state.SetCell(1, 0, CellKind.Spawner);
        state.SetCell(2, 0, CellKind.Sink);

        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        Assert.Equal(CellKind.Void, restored.GetCell(0, 0));
        Assert.Equal(CellKind.Spawner, restored.GetCell(1, 0));
        Assert.Equal(CellKind.Sink, restored.GetCell(2, 0));
    }

    #endregion

    #region FromState/ToState Round-Trip: ObjectiveProgress

    [Fact]
    public void RoundTrip_PreservesObjectiveProgress()
    {
        var state = CreateTestState();
        state.ObjectiveProgress[0] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Tile,
            ElementType = (int)ElementType.Item1,
            TargetCount = 10,
            CurrentCount = 3
        };
        state.ObjectiveProgress[1] = new ObjectiveProgress
        {
            TargetLayer = ObjectiveTargetLayer.Cover,
            ElementType = (int)CoverType.Cage,
            TargetCount = 5,
            CurrentCount = 5
        };

        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        Assert.Equal(ObjectiveTargetLayer.Tile, restored.ObjectiveProgress[0].TargetLayer);
        Assert.Equal((int)ElementType.Item1, restored.ObjectiveProgress[0].ElementType);
        Assert.Equal(10, restored.ObjectiveProgress[0].TargetCount);
        Assert.Equal(3, restored.ObjectiveProgress[0].CurrentCount);

        Assert.Equal(ObjectiveTargetLayer.Cover, restored.ObjectiveProgress[1].TargetLayer);
        Assert.True(restored.ObjectiveProgress[1].IsCompleted);
    }

    #endregion

    #region FromState/ToState Round-Trip: NextTileId

    [Fact]
    public void RoundTrip_PreservesNextTileId()
    {
        var state = CreateTestState();
        // NextTileId was incremented during tile creation
        int expectedId = state.NextTileId;

        var snapshot = GameStateSnapshot.FromState(in state);
        var restored = snapshot.ToState(new StubRandom());

        Assert.Equal(expectedId, restored.NextTileId);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FromState_EmptyBoard_ProducesValidSnapshot()
    {
        var random = new StubRandom();
        var state = new GameState(2, 2, 3, random);
        // No tiles set - all ElementType.None

        var snapshot = GameStateSnapshot.FromState(in state);

        Assert.Equal(2, snapshot.Width);
        Assert.Equal(2, snapshot.Height);
        Assert.Equal(4, snapshot.TileTypes.Length);
        Assert.All(snapshot.TileTypes, t => Assert.Equal(ElementType.None, t));
    }

    [Fact]
    public void ToState_AssignsProvidedRandom()
    {
        var snapshot = new GameStateSnapshot
        {
            Width = 2,
            Height = 2,
            TileTypesCount = 3,
            TileTypes = new[] { ElementType.Item1, ElementType.Item2, ElementType.Item3, ElementType.Item1 },
            Cells = new[] { CellKind.Slot, CellKind.Slot, CellKind.Slot, CellKind.Slot }
        };

        var random = new StubRandom();
        var state = snapshot.ToState(random);

        Assert.Same(random, state.Random);
    }

    [Fact]
    public void RoundTrip_AllLevelStatuses()
    {
        foreach (LevelStatus status in Enum.GetValues(typeof(LevelStatus)))
        {
            var state = CreateTestState(levelStatus: status);
            var snapshot = GameStateSnapshot.FromState(in state);
            var restored = snapshot.ToState(new StubRandom());

            Assert.Equal(status, restored.LevelStatus);
        }
    }

    #endregion
}
