using System.Collections.Generic;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Xunit;

namespace Match3.Core.Tests.Config;

public class InsightSelectorTests
{
    private static EffectivePool PoolWithCageAndIce()
    {
        var pool = new EffectivePool();
        pool.Covers[CoverType.Cage] = new CoverAllowance { Type = CoverType.Cage, MaxHealth = 1 };
        pool.Grounds[GroundType.Ice] = new GroundAllowance { Type = GroundType.Ice, MaxHealth = 1 };
        return pool;
    }

    private static EffectivePool PoolWithBoxOnly()
    {
        var pool = new EffectivePool();
        pool.Obstacles[ObstacleType.Box] = new ObstacleAllowance { Type = ObstacleType.Box, MaxStage = 2 };
        return pool;
    }

    private static EffectivePool EmptyPool() => new EffectivePool();

    private static List<DesignInsight> SampleInsights() => new()
    {
        new DesignInsight
        {
            Category = "foundation", Tags = System.Array.Empty<string>(),
            Title = "Random AI 不造炸弹",
            Finding = "纯随机 AI 不会主动造炸弹",
            Recommendation = "需要炸弹的目标胜率偏低"
        },
        new DesignInsight
        {
            Category = "element", Tags = new[] { "Cage" },
            Title = "Cage 不设为目标",
            Finding = "Cage 清除目标 AI 胜率≈0%",
            Recommendation = "Cage 仅做空间约束"
        },
        new DesignInsight
        {
            Category = "element", Tags = new[] { "Ice" },
            Title = "Ice 成片放置",
            Finding = "零散 Ice 无挑战也无视觉效果",
            Recommendation = "至少 6 块成片放置"
        },
        new DesignInsight
        {
            Category = "element", Tags = new[] { "Box" },
            Title = "Box 步数预算",
            Finding = "每个 Box 消耗约 2 步",
            Recommendation = "N 个 Box → 预留 2N 步"
        },
        new DesignInsight
        {
            Category = "combination", Tags = new[] { "Cage", "Ice" },
            Title = "Cage+Ice 双目标不可行",
            Finding = "两者都作为目标时 AI 胜率 0%",
            Recommendation = "用 Tile 目标替代，两者做装饰"
        },
        new DesignInsight
        {
            Category = "combination", Tags = new[] { "Box", "Ice" },
            Title = "Box+Ice 空间分离",
            Finding = "Box 和 Ice 在同一区域互相阻挡",
            Recommendation = "Box 和 Ice 放不同区域"
        }
    };

    #region Foundation Always Loaded

    [Fact]
    public void Foundation_AlwaysLoaded_EvenEmptyPool()
    {
        var insights = InsightSelector.SelectRelevant(SampleInsights(), EmptyPool());

        Assert.Contains(insights, i => i.Title == "Random AI 不造炸弹");
    }

    [Fact]
    public void Foundation_AlwaysLoaded_AnyPool()
    {
        var insights = InsightSelector.SelectRelevant(SampleInsights(), PoolWithBoxOnly());

        Assert.Contains(insights, i => i.Category == "foundation");
    }

    #endregion

    #region Element Insights

    [Fact]
    public void Element_LoadedWhenInPool()
    {
        var insights = InsightSelector.SelectRelevant(SampleInsights(), PoolWithCageAndIce());

        Assert.Contains(insights, i => i.Title == "Cage 不设为目标");
        Assert.Contains(insights, i => i.Title == "Ice 成片放置");
    }

    [Fact]
    public void Element_NotLoadedWhenNotInPool()
    {
        var insights = InsightSelector.SelectRelevant(SampleInsights(), PoolWithCageAndIce());

        Assert.DoesNotContain(insights, i => i.Title == "Box 步数预算");
    }

    [Fact]
    public void Element_BoxPool_LoadsBoxOnly()
    {
        var insights = InsightSelector.SelectRelevant(SampleInsights(), PoolWithBoxOnly());

        Assert.Contains(insights, i => i.Title == "Box 步数预算");
        Assert.DoesNotContain(insights, i => i.Title == "Cage 不设为目标");
        Assert.DoesNotContain(insights, i => i.Title == "Ice 成片放置");
    }

    #endregion

    #region Combination Insights

    [Fact]
    public void Combination_LoadedWhenAllTagsInPool()
    {
        var insights = InsightSelector.SelectRelevant(SampleInsights(), PoolWithCageAndIce());

        Assert.Contains(insights, i => i.Title == "Cage+Ice 双目标不可行");
    }

    [Fact]
    public void Combination_NotLoadedWhenPartialTags()
    {
        // Pool has Box but not Ice
        var insights = InsightSelector.SelectRelevant(SampleInsights(), PoolWithBoxOnly());

        Assert.DoesNotContain(insights, i => i.Title == "Box+Ice 空间分离");
    }

    [Fact]
    public void Combination_NotLoadedWhenNoTags()
    {
        var insights = InsightSelector.SelectRelevant(SampleInsights(), EmptyPool());

        Assert.DoesNotContain(insights, i => i.Category == "combination");
    }

    #endregion

    #region Full Selection Count

    [Fact]
    public void CageIcePool_Gets4Insights()
    {
        // foundation(1) + element/Cage(1) + element/Ice(1) + combination/Cage+Ice(1) = 4
        var insights = InsightSelector.SelectRelevant(SampleInsights(), PoolWithCageAndIce());

        Assert.Equal(4, insights.Count);
    }

    [Fact]
    public void BoxPool_Gets2Insights()
    {
        // foundation(1) + element/Box(1) = 2
        var insights = InsightSelector.SelectRelevant(SampleInsights(), PoolWithBoxOnly());

        Assert.Equal(2, insights.Count);
    }

    [Fact]
    public void EmptyPool_GetsFoundationOnly()
    {
        var insights = InsightSelector.SelectRelevant(SampleInsights(), EmptyPool());

        Assert.Single(insights);
        Assert.Equal("foundation", insights[0].Category);
    }

    #endregion

    #region FormatForPrompt

    [Fact]
    public void FormatForPrompt_GroupsByCategory()
    {
        var insights = InsightSelector.SelectRelevant(SampleInsights(), PoolWithCageAndIce());
        var text = InsightSelector.FormatForPrompt(insights);

        Assert.Contains("基础经验", text);
        Assert.Contains("Cage 经验", text);
        Assert.Contains("Ice 经验", text);
        Assert.Contains("Cage+Ice 组合经验", text);
    }

    [Fact]
    public void FormatForPrompt_EmptyInsights_ReturnsEmpty()
    {
        var text = InsightSelector.FormatForPrompt(new List<DesignInsight>());

        Assert.Empty(text);
    }

    #endregion

    #region DeduplicationKey

    [Fact]
    public void DeduplicationKey_TagOrderIndependent()
    {
        var a = new DesignInsight
        {
            Category = "combination", Tags = new[] { "Ice", "Cage" },
            Title = "Test"
        };
        var b = new DesignInsight
        {
            Category = "combination", Tags = new[] { "Cage", "Ice" },
            Title = "Test"
        };

        Assert.Equal(a.DeduplicationKey(), b.DeduplicationKey());
    }

    #endregion
}
