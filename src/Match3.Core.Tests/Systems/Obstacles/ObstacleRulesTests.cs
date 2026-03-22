using Match3.Core.Events.Enums;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Elimination;
using Match3.Core.Systems.Obstacles;
using Match3.Core.Systems.Projectiles.Targeting;
using Xunit;

namespace Match3.Core.Tests.Systems.Obstacles;

public class ObstacleRulesTests
{
    #region CanHit — Direct hit rules

    [Theory]
    [InlineData(ElimSource.Match)]
    [InlineData(ElimSource.Bomb)]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ConsumeBomb)]
    public void CanHit_Box_AlwaysTrue(ElimSource source)
    {
        var obs = new Obstacle(ObstacleType.Box, 4);
        Assert.True(ObstacleRules.CanHit(in obs, new ElimContext(source)));
    }

    [Theory]
    [InlineData(ElimSource.Match)]
    [InlineData(ElimSource.Bomb)]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ConsumeBomb)]
    public void CanHit_Bush_AlwaysTrue(ElimSource source)
    {
        var obs = new Obstacle(ObstacleType.Bush, 5);
        Assert.True(ObstacleRules.CanHit(in obs, new ElimContext(source)));
    }

    [Fact]
    public void CanHit_Safe_MatchBlocked()
    {
        var obs = new Obstacle(ObstacleType.Safe, 5);
        Assert.False(ObstacleRules.CanHit(in obs, new ElimContext(ElimSource.Match)));
    }

    [Theory]
    [InlineData(ElimSource.Bomb)]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ChainReaction)]
    [InlineData(ElimSource.ColorBomb)]
    [InlineData(ElimSource.SideItem)]
    [InlineData(ElimSource.ConsumeBomb)]
    public void CanHit_Safe_PowerUpSources(ElimSource source)
    {
        var obs = new Obstacle(ObstacleType.Safe, 5);
        Assert.True(ObstacleRules.CanHit(in obs, new ElimContext(source)));
    }

    [Fact]
    public void CanHit_ColorBox_MatchBlocked()
    {
        var obs = new Obstacle(ObstacleType.ColorBox, 3, (byte)ElementType.Item1);
        Assert.False(ObstacleRules.CanHit(in obs, new ElimContext(ElimSource.Match)));
    }

    [Theory]
    [InlineData(ElimSource.Bomb)]
    [InlineData(ElimSource.Projectile)]
    [InlineData(ElimSource.ChainReaction)]
    [InlineData(ElimSource.ColorBomb)]
    [InlineData(ElimSource.SideItem)]
    [InlineData(ElimSource.ConsumeBomb)]
    public void CanHit_ColorBox_PowerUpSources(ElimSource source)
    {
        var obs = new Obstacle(ObstacleType.ColorBox, 3, (byte)ElementType.Item1);
        Assert.True(ObstacleRules.CanHit(in obs, new ElimContext(source)));
    }

    [Theory]
    [InlineData(ElimSource.Match)]
    [InlineData(ElimSource.Bomb)]
    public void CanHit_MagicHat_AlwaysFalse(ElimSource source)
    {
        var obs = new Obstacle(ObstacleType.MagicHat, 1);
        Assert.False(ObstacleRules.CanHit(in obs, new ElimContext(source)));
    }

    [Theory]
    [InlineData(ElimSource.Match)]
    [InlineData(ElimSource.Bomb)]
    public void CanHit_Curtain_AlwaysFalse(ElimSource source)
    {
        var obs = new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item1);
        Assert.False(ObstacleRules.CanHit(in obs, new ElimContext(source)));
    }

    #endregion

    #region CanReactAdjacent — Adjacent reaction rules

    [Theory]
    [InlineData(ElementType.Item1)]
    [InlineData(ElementType.Item2)]
    [InlineData(ElementType.ColorBomb)]
    public void CanReactAdjacent_Box_AlwaysTrue(ElementType triggerType)
    {
        var obs = new Obstacle(ObstacleType.Box, 4);
        Assert.True(ObstacleRules.CanReactAdjacent(in obs, triggerType));
    }

    [Theory]
    [InlineData(ElementType.Item1)]
    [InlineData(ElementType.ColorBomb)]
    public void CanReactAdjacent_Bush_AlwaysTrue(ElementType triggerType)
    {
        var obs = new Obstacle(ObstacleType.Bush, 5);
        Assert.True(ObstacleRules.CanReactAdjacent(in obs, triggerType));
    }

    [Theory]
    [InlineData(ElementType.Item1)]
    [InlineData(ElementType.ColorBomb)]
    public void CanReactAdjacent_Safe_AlwaysFalse(ElementType triggerType)
    {
        var obs = new Obstacle(ObstacleType.Safe, 5);
        Assert.False(ObstacleRules.CanReactAdjacent(in obs, triggerType));
    }

    [Fact]
    public void CanReactAdjacent_ColorBox_MatchingColor_True()
    {
        var obs = new Obstacle(ObstacleType.ColorBox, 1, (byte)ElementType.Item1);
        Assert.True(ObstacleRules.CanReactAdjacent(in obs, ElementType.Item1));
    }

    [Fact]
    public void CanReactAdjacent_ColorBox_WrongColor_False()
    {
        var obs = new Obstacle(ObstacleType.ColorBox, 1, (byte)ElementType.Item1);
        Assert.False(ObstacleRules.CanReactAdjacent(in obs, ElementType.Item2));
    }

    [Fact]
    public void CanReactAdjacent_ColorBox_ColorBombWildcard_True()
    {
        var obs = new Obstacle(ObstacleType.ColorBox, 1, (byte)ElementType.Item1);
        Assert.True(ObstacleRules.CanReactAdjacent(in obs, ElementType.ColorBomb));
    }

    [Theory]
    [InlineData(ElementType.Item1)]
    [InlineData(ElementType.Item2)]
    public void CanReactAdjacent_MagicHat_AlwaysTrue(ElementType triggerType)
    {
        var obs = new Obstacle(ObstacleType.MagicHat, 1);
        Assert.True(ObstacleRules.CanReactAdjacent(in obs, triggerType));
    }

    [Theory]
    [InlineData(ElementType.Item1)]
    [InlineData(ElementType.ColorBomb)]
    public void CanReactAdjacent_Curtain_AlwaysFalse(ElementType triggerType)
    {
        var obs = new Obstacle(ObstacleType.Curtain, 1, (byte)ElementType.Item1);
        Assert.False(ObstacleRules.CanReactAdjacent(in obs, triggerType));
    }

    #endregion

    #region GetDefaultStage

    [Fact]
    public void GetDefaultStage_ColorBox_Is3()
    {
        Assert.Equal(3, ObstacleRules.GetDefaultStage(ObstacleType.ColorBox));
    }

    #endregion

    #region UfoTargetConfig — ColorBox

    [Fact]
    public void UfoCanTarget_ColorBox()
    {
        Assert.True(UfoTargetConfig.CanUfoHitObstacle(ObstacleType.ColorBox));
    }

    #endregion
}
