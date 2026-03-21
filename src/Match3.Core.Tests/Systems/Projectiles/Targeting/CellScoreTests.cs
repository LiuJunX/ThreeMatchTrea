using Match3.Core.Systems.Projectiles.Targeting;
using Xunit;

namespace Match3.Core.Tests.Systems.Projectiles.Targeting;

public class CellScoreTests
{
    [Fact]
    public void CompareTo_HigherTierWins()
    {
        var low = new CellScore { Tier = 2, Urgency = 255, BaseValue = 1000 };
        var high = new CellScore { Tier = 3, Urgency = 0, BaseValue = 1 };

        Assert.True(high.CompareTo(low) > 0);
        Assert.True(low.CompareTo(high) < 0);
    }

    [Fact]
    public void CompareTo_SameTier_HigherUrgencyWins()
    {
        var low = new CellScore { Tier = 3, Urgency = 0, BaseValue = 1000 };
        var high = new CellScore { Tier = 3, Urgency = 255, BaseValue = 1 };

        Assert.True(high.CompareTo(low) > 0);
    }

    [Fact]
    public void CompareTo_SameTierAndUrgency_HigherTotalValueWins()
    {
        var low = new CellScore { Tier = 3, Urgency = 0, BaseValue = 10, TargetBonus = 0 };
        var high = new CellScore { Tier = 3, Urgency = 0, BaseValue = 50, TargetBonus = 100 };

        Assert.True(high.CompareTo(low) > 0);
    }

    [Fact]
    public void CompareTo_Equal_ReturnsZero()
    {
        var a = new CellScore { Tier = 2, Urgency = 100, BaseValue = 50, TargetBonus = 30 };
        var b = new CellScore { Tier = 2, Urgency = 100, BaseValue = 50, TargetBonus = 30 };

        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void TotalValue_IncludesSynergy()
    {
        var score = new CellScore { BaseValue = 50, TargetBonus = 100, Synergy = -10 };
        Assert.Equal(140, score.TotalValue);
    }
}
