using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Matching;
using Match3.Core.Systems.PowerUps;
using Match3.Core.Systems.Scoring;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.Systems.Matching;

/// <summary>
/// StandardMatchProcessor 单元测试
///
/// 职责：
/// - 处理匹配组，清除匹配的方块
/// - 生成炸弹方块
/// - 触发炸弹连锁爆炸
/// - 计算分数
/// </summary>
public class StandardMatchProcessorTests
{
    private StandardMatchProcessor CreateProcessor()
    {
        var scoreSystem = new Match3.Core.Tests.TestFixtures.StubScoreSystem();
        var bombRegistry = BombEffectRegistry.CreateDefault();
        return new StandardMatchProcessor(scoreSystem, new Match3.Core.Systems.Layers.CoverSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), new Match3.Core.Systems.Layers.GroundSystem(new Match3.Core.Systems.Objectives.LevelObjectiveSystem()), bombRegistry);
    }

    private GameState CreateEmptyState(int width = 8, int height = 8)
    {
        return new Match3.Core.Tests.TestFixtures.GameStateBuilder()
            .WithSize(width, height)
            .WithEmptyTiles()
            .Build();
    }

    #region Basic ProcessMatches Tests

    [Fact]
    public void ProcessMatches_SimpleMatch_ClearsTiles()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        // Act
        int points = processor.ProcessMatches(ref state, groups);

        // Assert
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(1, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 0).Type);
        Assert.Equal(30, points); // 3 tiles * 10 points
    }

    [Fact]
    public void ProcessMatches_EmptyGroups_ReturnsZero()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>();

        // Act
        int points = processor.ProcessMatches(ref state, groups);

        // Assert
        Assert.Equal(0, points);
        Assert.Equal(ElementType.Item1, state.GetTile(0, 0).Type); // 未消除
    }

    [Fact]
    public void ProcessMatches_MultipleGroups_ClearsAllTiles()
    {
        // Arrange
        var state = CreateEmptyState();
        // 第一组
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        // 第二组
        state.SetTile(0, 2, new Tile(4, ElementType.Item3, 0, 2));
        state.SetTile(1, 2, new Tile(5, ElementType.Item3, 1, 2));
        state.SetTile(2, 2, new Tile(6, ElementType.Item3, 2, 2));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            },
            new MatchGroup
            {
                Type = ElementType.Item3,
                Positions = new HashSet<Position> { new(0, 2), new(1, 2), new(2, 2) }
            }
        };

        // Act
        int points = processor.ProcessMatches(ref state, groups);

        // Assert
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(0, 2).Type);
        Assert.Equal(60, points); // 6 tiles * 10 points
    }

    #endregion

    #region Bomb Generation Tests

    [Fact]
    public void ProcessMatches_WithBombSpawn_CreatesBombTile()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(4, ElementType.Item1, 3, 0));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0), new(3, 0) },
                SpawnBombType = BombType.Horizontal,
                BombOrigin = new Position(1, 0) // 炸弹生成位置
            }
        };

        // Act
        processor.ProcessMatches(ref state, groups);

        // Assert
        var bombTile = state.GetTile(1, 0);
        Assert.Equal(ElementType.Item1, bombTile.Type);
        Assert.Equal(BombType.Horizontal, bombTile.Bomb);
        // 其他位置被清除
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(3, 0).Type);
    }

    [Fact]
    public void ProcessMatches_ColorBomb_CreatesRainbowTile()
    {
        // Arrange
        var state = CreateEmptyState();
        for (int x = 0; x < 5; x++)
        {
            state.SetTile(x, 0, new Tile(x + 1, ElementType.Item5, x, 0));
        }

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item5,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 0) },
                SpawnBombType = BombType.Color,
                BombOrigin = new Position(2, 0)
            }
        };

        // Act
        processor.ProcessMatches(ref state, groups);

        // Assert
        var rainbowTile = state.GetTile(2, 0);
        Assert.Equal(ElementType.Universal, rainbowTile.Type);
        Assert.Equal(BombType.Color, rainbowTile.Bomb);
    }

    #endregion

    #region Chain Explosion Tests

    [Fact]
    public void ProcessMatches_BombInMatch_TriggersExplosion()
    {
        // Arrange
        var state = CreateEmptyState();
        // 创建包含炸弹的匹配
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        var bombTile = new Tile(2, ElementType.Item1, 1, 0);
        bombTile.Bomb = BombType.Horizontal;
        state.SetTile(1, 0, bombTile);
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        // 同一行的其他方块
        state.SetTile(4, 0, new Tile(4, ElementType.Item3, 4, 0));
        state.SetTile(5, 0, new Tile(5, ElementType.Item2, 5, 0));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        // Act
        processor.ProcessMatches(ref state, groups);

        // Assert: 水平炸弹应该清除整行
        Assert.Equal(ElementType.None, state.GetTile(4, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(5, 0).Type);
    }

    [Fact]
    public void ProcessMatches_VerticalBomb_ClearsColumn()
    {
        // Arrange
        var state = CreateEmptyState();
        var bombTile = new Tile(1, ElementType.Item1, 0, 0);
        bombTile.Bomb = BombType.Vertical;
        state.SetTile(0, 0, bombTile);
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        // 同一列的其他方块
        state.SetTile(0, 3, new Tile(4, ElementType.Item3, 0, 3));
        state.SetTile(0, 5, new Tile(5, ElementType.Item2, 0, 5));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        // Act
        processor.ProcessMatches(ref state, groups);

        // Assert: 垂直炸弹应该清除整列
        Assert.Equal(ElementType.None, state.GetTile(0, 3).Type);
        Assert.Equal(ElementType.None, state.GetTile(0, 5).Type);
    }

    [Fact]
    public void ProcessMatches_AreaBomb_ClearsArea()
    {
        // Arrange
        var state = CreateEmptyState();
        var bombTile = new Tile(1, ElementType.Item1, 2, 2);
        bombTile.Bomb = BombType.Square5x5;
        state.SetTile(2, 2, bombTile);
        state.SetTile(3, 2, new Tile(2, ElementType.Item1, 3, 2));
        state.SetTile(4, 2, new Tile(3, ElementType.Item1, 4, 2));
        // 周围的方块
        state.SetTile(1, 1, new Tile(4, ElementType.Item3, 1, 1));
        state.SetTile(3, 3, new Tile(5, ElementType.Item2, 3, 3));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(2, 2), new(3, 2), new(4, 2) }
            }
        };

        // Act
        processor.ProcessMatches(ref state, groups);

        // Assert: 5x5区域应该被清除
        Assert.Equal(ElementType.None, state.GetTile(1, 1).Type);
        Assert.Equal(ElementType.None, state.GetTile(3, 3).Type);
    }

    #endregion

    #region Protected Tiles Tests

    [Fact]
    public void ProcessMatches_BombOrigin_IsProtected()
    {
        // Arrange
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        state.SetTile(3, 0, new Tile(4, ElementType.Item1, 3, 0));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0), new(3, 0) },
                SpawnBombType = BombType.Horizontal,
                BombOrigin = new Position(2, 0)
            }
        };

        // Act
        processor.ProcessMatches(ref state, groups);

        // Assert: BombOrigin 位置应该保留炸弹，不被清除
        var tile = state.GetTile(2, 0);
        Assert.NotEqual(ElementType.None, tile.Type);
        Assert.Equal(BombType.Horizontal, tile.Bomb);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ProcessMatches_AlreadyClearedTile_DoesNotFail()
    {
        // Arrange: 同一个位置在多个组中
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            },
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(1, 0) } // 重复位置
            }
        };

        // Act & Assert: 不应该抛出异常
        var ex = Record.Exception(() => processor.ProcessMatches(ref state, groups));
        Assert.Null(ex);
    }

    [Fact]
    public void ProcessMatches_EmptyTileInGroup_HandlesGracefully()
    {
        // Arrange: 组中包含空位置
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));
        // (1, 0) 是空的

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        // Act & Assert
        var ex = Record.Exception(() => processor.ProcessMatches(ref state, groups));
        Assert.Null(ex);
    }

    #endregion

    #region Cover Protection Tests

    [Fact]
    public void ProcessMatches_CoverProtectsTile_DamagesCoverOnly()
    {
        // Arrange: Cover 保护棋子，只伤害 Cover
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item1, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item1, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item1, 2, 0));

        // Put a cover on (1,0) — it should be protected
        state.SetCover(new Position(1, 0), new Cover(CoverType.Cage, 1));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item1,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        // Act
        processor.ProcessMatches(ref state, groups);

        // Assert: (1,0) tile is protected — tile remains, cover is destroyed
        var protectedTile = state.GetTile(1, 0);
        Assert.Equal(ElementType.Item1, protectedTile.Type);

        // Other tiles without cover should be cleared
        Assert.Equal(ElementType.None, state.GetTile(0, 0).Type);
        Assert.Equal(ElementType.None, state.GetTile(2, 0).Type);

        // Cover should have been damaged/destroyed (health was 1)
        Assert.False(state.HasCover(new Position(1, 0)));
    }

    [Fact]
    public void ProcessMatches_CoverDestroyed_TileRemains()
    {
        // Arrange: Cover 被摧毁后棋子还在
        var state = CreateEmptyState();
        state.SetTile(0, 0, new Tile(1, ElementType.Item3, 0, 0));
        state.SetTile(1, 0, new Tile(2, ElementType.Item3, 1, 0));
        state.SetTile(2, 0, new Tile(3, ElementType.Item3, 2, 0));

        // Cover on first tile
        state.SetCover(new Position(0, 0), new Cover(CoverType.Chain, 1));

        var processor = CreateProcessor();
        var groups = new List<MatchGroup>
        {
            new MatchGroup
            {
                Type = ElementType.Item3,
                Positions = new HashSet<Position> { new(0, 0), new(1, 0), new(2, 0) }
            }
        };

        // Act
        processor.ProcessMatches(ref state, groups);

        // Assert: (0,0) tile survives because cover protected it
        var tile = state.GetTile(0, 0);
        Assert.Equal(ElementType.Item3, tile.Type);
        Assert.Equal(1, tile.Id);

        // Cover is now gone
        Assert.False(state.HasCover(new Position(0, 0)));
    }

    #endregion
}


