using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Scoring;
using Xunit;

namespace Match3.Core.Tests.Systems.Scoring;

public class StandardScoreSystemTests
{
    private readonly StandardScoreSystem _system = new();

    #region CalculateMatchScore Tests

    [Fact]
    public void CalculateMatchScore_Match3_ShouldReturn30()
    {
        var group = new MatchGroup
        {
            Type = TileType.Red,
            Positions = new HashSet<Position>
            {
                new Position(0, 0),
                new Position(1, 0),
                new Position(2, 0)
            }
        };

        var score = _system.CalculateMatchScore(group);

        Assert.Equal(30, score); // 3 tiles * 10 points
    }

    [Fact]
    public void CalculateMatchScore_Match4_ShouldReturn40()
    {
        var group = new MatchGroup
        {
            Type = TileType.Blue,
            Positions = new HashSet<Position>
            {
                new Position(0, 0),
                new Position(1, 0),
                new Position(2, 0),
                new Position(3, 0)
            }
        };

        var score = _system.CalculateMatchScore(group);

        Assert.Equal(40, score); // 4 tiles * 10 points
    }

    [Fact]
    public void CalculateMatchScore_Match5_ShouldReturn50()
    {
        var group = new MatchGroup
        {
            Type = TileType.Green,
            Positions = new HashSet<Position>
            {
                new Position(0, 0),
                new Position(1, 0),
                new Position(2, 0),
                new Position(3, 0),
                new Position(4, 0)
            }
        };

        var score = _system.CalculateMatchScore(group);

        Assert.Equal(50, score); // 5 tiles * 10 points
    }

    [Fact]
    public void CalculateMatchScore_TShape5_ShouldReturn50()
    {
        // T-shape: 5 unique positions
        var group = new MatchGroup
        {
            Type = TileType.Yellow,
            Shape = MatchShape.Cross,
            Positions = new HashSet<Position>
            {
                new Position(0, 1), new Position(1, 1), new Position(2, 1),
                new Position(1, 0), new Position(1, 2)
            }
        };

        var score = _system.CalculateMatchScore(group);

        Assert.Equal(50, score); // 5 tiles * 10 points
    }

    [Theory]
    [InlineData(3, 30)]
    [InlineData(4, 40)]
    [InlineData(5, 50)]
    [InlineData(6, 60)]
    [InlineData(7, 70)]
    [InlineData(10, 100)]
    public void CalculateMatchScore_VariousSizes_ShouldScaleLinearly(int size, int expectedScore)
    {
        var positions = new HashSet<Position>();
        for (int i = 0; i < size; i++)
        {
            positions.Add(new Position(i, 0));
        }

        var group = new MatchGroup
        {
            Type = TileType.Red,
            Positions = positions
        };

        var score = _system.CalculateMatchScore(group);

        Assert.Equal(expectedScore, score);
    }

    #endregion

    #region CalculateSpecialMoveScore Tests - Rainbow Combinations

    [Fact]
    public void CalculateSpecialMoveScore_RainbowPlusRainbow_ShouldReturn5000()
    {
        var score = _system.CalculateSpecialMoveScore(
            TileType.Rainbow, BombType.None,
            TileType.Rainbow, BombType.None);

        Assert.Equal(5000, score);
    }

    [Fact]
    public void CalculateSpecialMoveScore_RainbowPlusNormal_ShouldReturn2000()
    {
        var score = _system.CalculateSpecialMoveScore(
            TileType.Rainbow, BombType.None,
            TileType.Red, BombType.None);

        Assert.Equal(2000, score);
    }

    [Fact]
    public void CalculateSpecialMoveScore_NormalPlusRainbow_ShouldReturn2000()
    {
        // Order shouldn't matter
        var score = _system.CalculateSpecialMoveScore(
            TileType.Blue, BombType.None,
            TileType.Rainbow, BombType.None);

        Assert.Equal(2000, score);
    }

    [Theory]
    [InlineData(BombType.Horizontal)]
    [InlineData(BombType.Vertical)]
    [InlineData(BombType.Square5x5)]
    [InlineData(BombType.Ufo)]
    [InlineData(BombType.Color)]
    public void CalculateSpecialMoveScore_RainbowPlusBomb_ShouldReturn2500(BombType bombType)
    {
        var score = _system.CalculateSpecialMoveScore(
            TileType.Rainbow, BombType.None,
            TileType.Red, bombType);

        Assert.Equal(2500, score);
    }

    [Theory]
    [InlineData(BombType.Horizontal)]
    [InlineData(BombType.Vertical)]
    [InlineData(BombType.Square5x5)]
    public void CalculateSpecialMoveScore_BombPlusRainbow_ShouldReturn2500(BombType bombType)
    {
        // Order shouldn't matter
        var score = _system.CalculateSpecialMoveScore(
            TileType.Red, bombType,
            TileType.Rainbow, BombType.None);

        Assert.Equal(2500, score);
    }

    #endregion

    #region CalculateSpecialMoveScore Tests - Bomb Combinations

    [Theory]
    [InlineData(BombType.Horizontal, BombType.Horizontal)]
    [InlineData(BombType.Horizontal, BombType.Vertical)]
    [InlineData(BombType.Vertical, BombType.Vertical)]
    [InlineData(BombType.Horizontal, BombType.Square5x5)]
    [InlineData(BombType.Square5x5, BombType.Square5x5)]
    [InlineData(BombType.Ufo, BombType.Horizontal)]
    [InlineData(BombType.Ufo, BombType.Ufo)]
    public void CalculateSpecialMoveScore_BombPlusBomb_ShouldReturn1000(BombType bomb1, BombType bomb2)
    {
        var score = _system.CalculateSpecialMoveScore(
            TileType.Red, bomb1,
            TileType.Blue, bomb2);

        Assert.Equal(1000, score);
    }

    #endregion

    #region CalculateSpecialMoveScore Tests - No Special

    [Fact]
    public void CalculateSpecialMoveScore_NormalPlusNormal_ShouldReturn0()
    {
        var score = _system.CalculateSpecialMoveScore(
            TileType.Red, BombType.None,
            TileType.Blue, BombType.None);

        Assert.Equal(0, score);
    }

    [Fact]
    public void CalculateSpecialMoveScore_SingleBomb_ShouldReturn0()
    {
        // One bomb, one normal - not a special combo
        var score = _system.CalculateSpecialMoveScore(
            TileType.Red, BombType.Horizontal,
            TileType.Blue, BombType.None);

        Assert.Equal(0, score);
    }

    #endregion
}
