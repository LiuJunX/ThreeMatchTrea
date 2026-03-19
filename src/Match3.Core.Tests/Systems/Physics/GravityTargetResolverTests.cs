using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Core.Tests.TestFixtures;
using Xunit;

namespace Match3.Core.Tests.Systems.Physics;

public class GravityTargetResolverTests
{
    #region Single-Cell Vertical Tests

    [Fact]
    public void DetermineTarget_EmptyBelow_ShouldReturnNextCell()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 5, 5, random);
        ClearBoard(ref state);
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        Assert.Equal(1f, result.Position.X);
        Assert.Equal(1f, result.Position.Y); // single-cell, not bottom
    }

    [Fact]
    public void DetermineTarget_BlockedBelow_ShouldStayPut()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(1, 1, new Tile(2, ElementType.Item3, 1, 1));

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        Assert.Equal(1f, result.Position.X);
        Assert.Equal(0f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_AtBottom_ShouldStayPut()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);
        state.SetTile(1, 2, new Tile(1, ElementType.Item1, 1, 2));

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 2);

        Assert.Equal(1f, result.Position.X);
        Assert.Equal(2f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_SingleCellBoard_ShouldStayPut()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(1, 1, 5, random);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 0, 0);

        Assert.Equal(0f, result.Position.X);
        Assert.Equal(0f, result.Position.Y);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(9)]
    public void DetermineTarget_AllColumns_TargetsNextCell(int column)
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(10, 5, 5, random);
        ClearBoard(ref state);
        state.SetTile(column, 0, new Tile(1, ElementType.Item1, column, 0));

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, column, 0);

        Assert.Equal((float)column, result.Position.X);
        Assert.Equal(1f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_FallingTileBelow_ShouldWait()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 5, 5, random);
        ClearBoard(ref state);
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(1, 1, new Tile(2, ElementType.Item3, 1, 1) { IsFalling = true });

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        Assert.Equal(1f, result.Position.X);
        Assert.Equal(0f, result.Position.Y);
    }

    #endregion

    #region Diagonal Slide Tests

    [Fact]
    public void DetermineTarget_ObstacleBelow_SlideToDeadZone()
    {
        //   O T .     obstacle (0,0) makes (0,1) a dead zone
        //   . O .     obstacle (1,1) blocks vertical
        //   . . .
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 1));
        state.SetObstacle(0, 0, new Obstacle(ObstacleType.Box, 1));

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        Assert.Equal(0f, result.Position.X);
        Assert.Equal(1f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_ObstacleBelow_NoDeadZone_ShouldNotSlide()
    {
        //   . T .     no dead zone adjacent
        //   . O .     obstacle blocks vertical
        //   . . .
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 1));

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        Assert.Equal(1f, result.Position.X);
        Assert.Equal(0f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_BothDiagonalsDeadZone_RandomChoice()
    {
        //   O T O     obstacles make both sides dead zone
        //   . O .
        //   . . .
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 1));
        state.SetObstacle(0, 0, new Obstacle(ObstacleType.Box, 1));
        state.SetObstacle(2, 0, new Obstacle(ObstacleType.Box, 1));

        resolver.ClearReservations();
        var result1 = resolver.DetermineTarget(ref state, 1, 0);
        random.ReturnValue = 1;
        resolver.ClearReservations();
        var result2 = resolver.DetermineTarget(ref state, 1, 0);

        Assert.Equal(0f, result1.Position.X);
        Assert.Equal(2f, result2.Position.X);
    }

    [Fact]
    public void DetermineTarget_LeftEdge_SlideRight()
    {
        //   T O     obstacle (1,0) makes (1,1) dead zone
        //   O .     obstacle (0,1) blocks vertical
        //   . .
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(2, 3, 5, random);
        ClearBoard(ref state);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetObstacle(0, 1, new Obstacle(ObstacleType.Box, 1));
        state.SetObstacle(1, 0, new Obstacle(ObstacleType.Box, 1));

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 0, 0);

        Assert.Equal(1f, result.Position.X);
        Assert.Equal(1f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_DiagonalTargetOccupied_NoSlide()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetTile(1, 1, new Tile(2, ElementType.Item3, 1, 1));
        state.SetTile(0, 1, new Tile(3, ElementType.Item2, 0, 1));
        state.SetTile(2, 1, new Tile(4, ElementType.Item4, 2, 1));

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        Assert.Equal(1f, result.Position.X);
        Assert.Equal(0f, result.Position.Y);
    }

    [Fact]
    public void DetermineTarget_CollisionRisk_PreventsSlide()
    {
        //   B T .     B at (0,0) = collision risk for left slide
        //   O O .     obstacles make (0,1) dead zone
        //   . . .
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);
        state.SetTile(0, 0, new Tile(2, ElementType.Item2, 0, 0));
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));
        state.SetObstacle(0, 1, new Obstacle(ObstacleType.Box, 1));
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 1));

        resolver.ClearReservations();
        var result = resolver.DetermineTarget(ref state, 1, 0);

        Assert.Equal(1f, result.Position.X);
        Assert.Equal(0f, result.Position.Y);
    }

    #endregion

    #region Reservation Tests

    [Fact]
    public void Reservations_DifferentColumns_Independent()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 5, 5, random);
        ClearBoard(ref state);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));

        resolver.ClearReservations();
        var r1 = resolver.DetermineTarget(ref state, 0, 0);
        var r2 = resolver.DetermineTarget(ref state, 1, 0);

        Assert.Equal(1f, r1.Position.Y);
        Assert.Equal(1f, r2.Position.Y);
    }

    [Fact]
    public void Reservations_SameCell_SecondBlocked()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(1, 5, 5, random);
        ClearBoard(ref state);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));

        resolver.ClearReservations();
        var r1 = resolver.DetermineTarget(ref state, 0, 0);
        var r2 = resolver.DetermineTarget(ref state, 0, 0);

        Assert.Equal(1f, r1.Position.Y);
        Assert.Equal(0f, r2.Position.Y);
    }

    [Fact]
    public void ClearReservations_AllowsReuse()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(1, 5, 5, random);
        ClearBoard(ref state);
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));

        resolver.ClearReservations();
        var r1 = resolver.DetermineTarget(ref state, 0, 0);
        resolver.ClearReservations();
        var r2 = resolver.DetermineTarget(ref state, 0, 0);

        Assert.Equal(r1.Position.Y, r2.Position.Y);
    }

    #endregion

    #region PeekNextMove Tests

    [Fact]
    public void PeekNextMove_EmptyBelow_ReturnsVertical()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 5, 5, random);
        ClearBoard(ref state);

        Assert.Equal(NextMoveType.Vertical, resolver.PeekNextMove(ref state, 1, 2));
    }

    [Fact]
    public void PeekNextMove_AtBottom_ReturnsStop()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);

        Assert.Equal(NextMoveType.Stop, resolver.PeekNextMove(ref state, 1, 2));
    }

    [Fact]
    public void PeekNextMove_BlockedNoDeadZone_ReturnsStop()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);
        state.SetTile(1, 1, new Tile(2, ElementType.Item3, 1, 1));

        Assert.Equal(NextMoveType.Stop, resolver.PeekNextMove(ref state, 1, 0));
    }

    [Fact]
    public void PeekNextMove_ObstacleWithDeadZone_ReturnsDiagonal()
    {
        //   O . .     obstacle (0,0) makes (0,1) dead zone
        //   . O .     obstacle (1,1) blocks vertical
        //   . . .
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 3, 5, random);
        ClearBoard(ref state);
        state.SetObstacle(1, 1, new Obstacle(ObstacleType.Box, 1));
        state.SetObstacle(0, 0, new Obstacle(ObstacleType.Box, 1));

        Assert.Equal(NextMoveType.Diagonal, resolver.PeekNextMove(ref state, 1, 0));
    }

    [Fact]
    public void PeekNextMove_DoesNotAffectReservations()
    {
        var random = StubRandom.WithFixedValue(0);
        var resolver = new GravityTargetResolver(random);
        var state = new GameState(3, 5, 5, random);
        ClearBoard(ref state);
        state.SetTile(1, 0, new Tile(1, ElementType.Item1, 1, 0));

        resolver.ClearReservations();
        resolver.PeekNextMove(ref state, 1, 0);
        var result = resolver.DetermineTarget(ref state, 1, 0);

        Assert.Equal(1f, result.Position.Y); // not blocked by peek
    }

    #endregion

    #region Helper

    private static void ClearBoard(ref GameState state)
    {
        for (int y = 0; y < state.Height; y++)
            for (int x = 0; x < state.Width; x++)
                state.SetTile(x, y, new Tile(0, ElementType.None, x, y));
    }

    #endregion
}
