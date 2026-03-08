using Match3.Core.AI;
using Match3.Core.AI.Strategies;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Core.Tests.TestFixtures;
using Match3.Random;
using Xunit;

namespace Match3.Core.Tests.AI;

public class GreedyStrategyTests
{
    private static readonly GreedyStrategy Strategy = new();

    private static GameState CreateDefaultState()
    {
        return new GameState(5, 5, 5, new StubRandom());
    }

    private static Move CreateMove(int fromX, int fromY, int toX, int toY)
    {
        return new Move(new Position(fromX, fromY), new Position(toX, toY));
    }

    [Fact]
    public void Name_ReturnsGreedy()
    {
        Assert.Equal("Greedy", Strategy.Name);
    }

    [Fact]
    public void ScoreMove_InvalidMove_ReturnsLargeNegative()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 0,
            MatchesProcessed = 0,
            ScoreGained = 0,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        float score = Strategy.ScoreMove(in state, move, preview);

        Assert.Equal(-1000f, score);
    }

    [Fact]
    public void ScoreMove_HigherScoreGained_ReturnsHigherValue()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);

        var lowScorePreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        var highScorePreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 500,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        float lowResult = Strategy.ScoreMove(in state, move, lowScorePreview);
        float highResult = Strategy.ScoreMove(in state, move, highScorePreview);

        Assert.True(highResult > lowResult,
            $"Higher ScoreGained ({highScorePreview.ScoreGained}) should produce higher result ({highResult}) than lower ScoreGained ({lowScorePreview.ScoreGained}) result ({lowResult})");
    }

    [Fact]
    public void ScoreMove_MoreTilesCleared_ReturnsHigherValue()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);

        var fewTilesPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        var manyTilesPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 8,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        float fewResult = Strategy.ScoreMove(in state, move, fewTilesPreview);
        float manyResult = Strategy.ScoreMove(in state, move, manyTilesPreview);

        Assert.True(manyResult > fewResult,
            $"More tiles cleared ({manyTilesPreview.TilesCleared}) should produce higher result ({manyResult}) than fewer ({fewTilesPreview.TilesCleared}) result ({fewResult})");
    }

    [Fact]
    public void ScoreMove_DeeperCascade_ReturnsHigherValue()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);

        var noCascadePreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        var deepCascadePreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 3,
            BombsActivated = 0
        };

        float noResult = Strategy.ScoreMove(in state, move, noCascadePreview);
        float deepResult = Strategy.ScoreMove(in state, move, deepCascadePreview);

        Assert.True(deepResult > noResult,
            $"Deeper cascade (depth {deepCascadePreview.MaxCascadeDepth}) should score higher ({deepResult}) than no cascade ({noResult})");
    }

    [Theory]
    [InlineData(3, 30, 0)]   // minimal match
    [InlineData(5, 100, 1)]  // medium match with cascade
    [InlineData(10, 300, 3)] // large match with deep cascade
    public void ScoreMove_ValidMove_ReturnsPositiveScore(int tilesCleared, int scoreGained, int cascadeDepth)
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = tilesCleared,
            MatchesProcessed = 1,
            ScoreGained = scoreGained,
            MaxCascadeDepth = cascadeDepth,
            BombsActivated = 0
        };

        float score = Strategy.ScoreMove(in state, move, preview);

        Assert.True(score > 0, $"Valid move should have positive score, got {score}");
    }

    [Fact]
    public void ScoreMove_ZeroScoreValidMove_StillPositive()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,     // IsValidMove = true because TilesCleared > 0
            MatchesProcessed = 1,
            ScoreGained = 0,      // zero score
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        float score = Strategy.ScoreMove(in state, move, preview);

        // TilesCleared * 10 = 30, so score should be positive
        Assert.True(score > 0, $"Valid move with zero score should still have positive value from tiles bonus, got {score}");
    }

    [Fact]
    public void ScoreMove_VerifyFormula()
    {
        // Greedy formula: ScoreGained + MaxCascadeDepth * 50 + TilesCleared * 10
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 5,
            MatchesProcessed = 1,
            ScoreGained = 200,
            MaxCascadeDepth = 2,
            BombsActivated = 0
        };

        float score = Strategy.ScoreMove(in state, move, preview);

        float expected = 200f + 2 * 50f + 5 * 10f; // 200 + 100 + 50 = 350
        Assert.Equal(expected, score);
    }
}

public class BombPriorityStrategyTests
{
    private static readonly BombPriorityStrategy Strategy = new();

    private static GameState CreateDefaultState()
    {
        return new GameState(5, 5, 5, new StubRandom());
    }

    private static Move CreateMove(int fromX, int fromY, int toX, int toY)
    {
        return new Move(new Position(fromX, fromY), new Position(toX, toY));
    }

    [Fact]
    public void Name_ReturnsBombPriority()
    {
        Assert.Equal("BombPriority", Strategy.Name);
    }

    [Fact]
    public void ScoreMove_InvalidMove_ReturnsLargeNegative()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 0,
            MatchesProcessed = 0,
            ScoreGained = 0,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        float score = Strategy.ScoreMove(in state, move, preview);

        Assert.Equal(-1000f, score);
    }

    [Fact]
    public void ScoreMove_BombActivation_RankedHigherThanNoBomb()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);

        var noBombPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        var bombPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 1
        };

        float noBombScore = Strategy.ScoreMove(in state, move, noBombPreview);
        float bombScore = Strategy.ScoreMove(in state, move, bombPreview);

        Assert.True(bombScore > noBombScore,
            $"Bomb activation should rank higher ({bombScore}) than no bomb ({noBombScore})");
    }

    [Fact]
    public void ScoreMove_MultipleBombs_ScoresHigherThanSingleBomb()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);

        var singleBombPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 1
        };

        var multiBombPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 3
        };

        float singleScore = Strategy.ScoreMove(in state, move, singleBombPreview);
        float multiScore = Strategy.ScoreMove(in state, move, multiBombPreview);

        Assert.True(multiScore > singleScore,
            $"Multiple bombs ({multiBombPreview.BombsActivated}) should score higher ({multiScore}) than single ({singleScore})");
    }

    [Fact]
    public void ScoreMove_DeepCascade_GetsBonus()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);

        var shallowPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 1,
            BombsActivated = 0
        };

        var deepPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 3,
            BombsActivated = 0
        };

        float shallowScore = Strategy.ScoreMove(in state, move, shallowPreview);
        float deepScore = Strategy.ScoreMove(in state, move, deepPreview);

        Assert.True(deepScore > shallowScore,
            $"Deeper cascade ({deepPreview.MaxCascadeDepth}) should score higher ({deepScore}) than shallow ({shallowPreview.MaxCascadeDepth}) score ({shallowScore})");
    }

    [Fact]
    public void ScoreMove_FourOrMoreTilesCleared_GetsBonus()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);

        var threePreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        var fivePreview = new MovePreview
        {
            Move = move,
            TilesCleared = 5,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        float threeScore = Strategy.ScoreMove(in state, move, threePreview);
        float fiveScore = Strategy.ScoreMove(in state, move, fivePreview);

        // fivePreview should get the 100f bonus for TilesCleared >= 4
        Assert.True(fiveScore > threeScore,
            $"5 tiles cleared should score higher ({fiveScore}) than 3 ({threeScore}) due to potential bomb creation bonus");
    }

    [Fact]
    public void ScoreMove_CascadeDepthTwo_GetsSpecialBonus()
    {
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);

        // Cascade depth < 2: no "likely created special tiles" bonus
        var shallowPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 0,
            MaxCascadeDepth = 1,
            BombsActivated = 0
        };

        // Cascade depth >= 2: gets the 200f bonus
        var deepPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 0,
            MaxCascadeDepth = 2,
            BombsActivated = 0
        };

        float shallowScore = Strategy.ScoreMove(in state, move, shallowPreview);
        float deepScore = Strategy.ScoreMove(in state, move, deepPreview);

        // Difference should include the 200f bonus + cascade increment (75 per depth)
        float difference = deepScore - shallowScore;
        Assert.True(difference >= 200f,
            $"Cascade depth >= 2 should add at least 200 bonus. Actual difference: {difference}");
    }

    [Fact]
    public void ScoreMove_BombDominatesScore()
    {
        // Bomb activation (500 per bomb) should outweigh moderate ScoreGained
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);

        var highScoreNoBombPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 400,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        var lowScoreWithBombPreview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 50,
            MaxCascadeDepth = 0,
            BombsActivated = 1
        };

        float noBombScore = Strategy.ScoreMove(in state, move, highScoreNoBombPreview);
        float bombScore = Strategy.ScoreMove(in state, move, lowScoreWithBombPreview);

        Assert.True(bombScore > noBombScore,
            $"Bomb move (score {bombScore}) should outrank high-score no-bomb move (score {noBombScore})");
    }

    [Fact]
    public void ScoreMove_VerifyFormula()
    {
        // BombPriority formula:
        //   BombsActivated * 500
        //   + (MaxCascadeDepth >= 2 ? 200 : 0)
        //   + (TilesCleared >= 4 ? 100 : 0)
        //   + ScoreGained * 0.5
        //   + MaxCascadeDepth * 75
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 5,
            MatchesProcessed = 1,
            ScoreGained = 200,
            MaxCascadeDepth = 3,
            BombsActivated = 2
        };

        float score = Strategy.ScoreMove(in state, move, preview);

        float expected = 2 * 500f   // bombs: 1000
                       + 200f       // cascade depth >= 2
                       + 100f       // tiles cleared >= 4
                       + 200 * 0.5f // score: 100
                       + 3 * 75f;   // cascade: 225
        // Total: 1000 + 200 + 100 + 100 + 225 = 1625
        Assert.Equal(expected, score);
    }
}

public class SyntheticPlayerStrategyTests
{
    private static GameState CreateDefaultState()
    {
        return new GameState(5, 5, 5, new StubRandom());
    }

    private static Move CreateMove(int fromX, int fromY, int toX, int toY)
    {
        return new Move(new Position(fromX, fromY), new Position(toX, toY));
    }

    [Fact]
    public void Name_IncludesProfileName()
    {
        var profile = PlayerProfile.Casual;
        var strategy = new SyntheticPlayerStrategy(profile, new XorShift64(42));

        Assert.Equal("SyntheticPlayer_Casual", strategy.Name);
    }

    [Theory]
    [InlineData("Novice")]
    [InlineData("Casual")]
    [InlineData("Core")]
    [InlineData("Expert")]
    public void Name_ReflectsProfilePreset(string profileName)
    {
        var profile = profileName switch
        {
            "Novice" => PlayerProfile.Novice,
            "Casual" => PlayerProfile.Casual,
            "Core" => PlayerProfile.Core,
            "Expert" => PlayerProfile.Expert,
            _ => PlayerProfile.Casual
        };
        var strategy = new SyntheticPlayerStrategy(profile, new XorShift64(42));

        Assert.Equal($"SyntheticPlayer_{profileName}", strategy.Name);
    }

    [Fact]
    public void ScoreMove_InvalidMove_ReturnsLargeNegative()
    {
        var strategy = new SyntheticPlayerStrategy(PlayerProfile.Casual, new XorShift64(42));
        var state = CreateDefaultState();
        var move = CreateMove(0, 0, 1, 0);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 0,
            MatchesProcessed = 0,
            ScoreGained = 0,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        float score = strategy.ScoreMove(in state, move, preview);

        Assert.Equal(-1000f, score);
    }

    [Fact]
    public void ScoreMove_ValidMove_ReturnsFiniteValue()
    {
        var strategy = new SyntheticPlayerStrategy(PlayerProfile.Casual, new XorShift64(42));
        var state = CreateDefaultState();
        var move = CreateMove(2, 2, 3, 2);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        float score = strategy.ScoreMove(in state, move, preview);

        Assert.False(float.IsNaN(score), "Score should not be NaN");
        Assert.False(float.IsInfinity(score), "Score should not be infinite");
    }

    [Fact]
    public void ScoreMove_ExpertHasLessNoise()
    {
        // Expert (skillLevel=0.95) should have less noise variation than Novice (skillLevel=0.2)
        // Run multiple scores with same preview and measure variance
        var expertProfile = PlayerProfile.Expert;
        var noviceProfile = PlayerProfile.Novice;
        var state = CreateDefaultState();
        var move = CreateMove(2, 2, 3, 2);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 1,
            BombsActivated = 0
        };

        // Collect scores from multiple invocations (different RNG states)
        var expertScores = new List<float>();
        var noviceScores = new List<float>();

        for (int i = 0; i < 50; i++)
        {
            var expertStrategy = new SyntheticPlayerStrategy(expertProfile, new XorShift64((ulong)(i + 1)));
            var noviceStrategy = new SyntheticPlayerStrategy(noviceProfile, new XorShift64((ulong)(i + 1)));

            expertScores.Add(expertStrategy.ScoreMove(in state, move, preview));
            noviceScores.Add(noviceStrategy.ScoreMove(in state, move, preview));
        }

        float expertVariance = CalculateVariance(expertScores);
        float noviceVariance = CalculateVariance(noviceScores);

        Assert.True(expertVariance < noviceVariance,
            $"Expert variance ({expertVariance:F2}) should be less than novice variance ({noviceVariance:F2})");
    }

    [Fact]
    public void ScoreMove_BombPreference_AffectsScore()
    {
        var state = CreateDefaultState();
        var move = CreateMove(2, 2, 3, 2);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 2
        };

        // Use fixed seed for deterministic comparison
        var highBombProfile = new PlayerProfile
        {
            Name = "HighBomb",
            SkillLevel = 0.5f,
            BombPreference = 2.0f,
            ObjectiveFocus = 0f, // eliminate objective factor
        };
        var lowBombProfile = new PlayerProfile
        {
            Name = "LowBomb",
            SkillLevel = 0.5f,
            BombPreference = 0.1f,
            ObjectiveFocus = 0f,
        };

        var highBombStrategy = new SyntheticPlayerStrategy(highBombProfile, new XorShift64(42));
        var lowBombStrategy = new SyntheticPlayerStrategy(lowBombProfile, new XorShift64(42));

        float highScore = highBombStrategy.ScoreMove(in state, move, preview);
        float lowScore = lowBombStrategy.ScoreMove(in state, move, preview);

        Assert.True(highScore > lowScore,
            $"High bomb preference ({highScore:F2}) should score higher with bomb moves than low ({lowScore:F2})");
    }

    [Fact]
    public void ScoreMove_SkillLevel_AffectsCascadeAwareness()
    {
        var state = CreateDefaultState();
        var move = CreateMove(2, 2, 3, 2);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 5, // deep cascade
            BombsActivated = 0
        };

        var expertProfile = new PlayerProfile
        {
            Name = "Expert",
            SkillLevel = 1.0f,
            BombPreference = 0f,
            ObjectiveFocus = 0f,
        };
        var noviceProfile = new PlayerProfile
        {
            Name = "Novice",
            SkillLevel = 0.0f,
            BombPreference = 0f,
            ObjectiveFocus = 0f,
        };

        // Use same seed so noise is identical
        var expertStrategy = new SyntheticPlayerStrategy(expertProfile, new XorShift64(42));
        var noviceStrategy = new SyntheticPlayerStrategy(noviceProfile, new XorShift64(42));

        float expertScore = expertStrategy.ScoreMove(in state, move, preview);
        float noviceScore = noviceStrategy.ScoreMove(in state, move, preview);

        // Expert should value cascade more (cascade * 40 * skillLevel)
        // and also value ScoreGained more (scoreGained * 0.1 * skillLevel)
        Assert.True(expertScore > noviceScore,
            $"Expert ({expertScore:F2}) should value deep cascades more than novice ({noviceScore:F2})");
    }

    [Fact]
    public void ScoreMove_DeterministicWithSameSeed()
    {
        var state = CreateDefaultState();
        var move = CreateMove(2, 2, 3, 2);
        var preview = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 1,
            BombsActivated = 0
        };

        var strategy1 = new SyntheticPlayerStrategy(PlayerProfile.Casual, new XorShift64(12345));
        var strategy2 = new SyntheticPlayerStrategy(PlayerProfile.Casual, new XorShift64(12345));

        float score1 = strategy1.ScoreMove(in state, move, preview);
        float score2 = strategy2.ScoreMove(in state, move, preview);

        Assert.Equal(score1, score2);
    }

    [Fact]
    public void ScoreMove_LargeMatch_BonusForNovice()
    {
        // Novice gets bigger visual attraction bonus for large matches
        var state = CreateDefaultState();
        var move = CreateMove(2, 2, 3, 2);

        var smallMatch = new MovePreview
        {
            Move = move,
            TilesCleared = 3,
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        var largeMatch = new MovePreview
        {
            Move = move,
            TilesCleared = 5, // >= 4 triggers visual attraction bonus
            MatchesProcessed = 1,
            ScoreGained = 100,
            MaxCascadeDepth = 0,
            BombsActivated = 0
        };

        var strategy = new SyntheticPlayerStrategy(PlayerProfile.Novice, new XorShift64(42));

        float smallScore = strategy.ScoreMove(in state, move, smallMatch);

        // Reset strategy with same seed for fair comparison
        var strategy2 = new SyntheticPlayerStrategy(PlayerProfile.Novice, new XorShift64(42));
        float largeScore = strategy2.ScoreMove(in state, move, largeMatch);

        Assert.True(largeScore > smallScore,
            $"Large match ({largeScore:F2}) should score higher than small match ({smallScore:F2}) for novice");
    }

    [Fact]
    public void PlayerProfile_Presets_HaveExpectedValues()
    {
        var novice = PlayerProfile.Novice;
        Assert.Equal("Novice", novice.Name);
        Assert.Equal(0.2f, novice.SkillLevel);

        var casual = PlayerProfile.Casual;
        Assert.Equal("Casual", casual.Name);
        Assert.Equal(0.5f, casual.SkillLevel);

        var core = PlayerProfile.Core;
        Assert.Equal("Core", core.Name);
        Assert.Equal(0.75f, core.SkillLevel);

        var expert = PlayerProfile.Expert;
        Assert.Equal("Expert", expert.Name);
        Assert.Equal(0.95f, expert.SkillLevel);
    }

    [Fact]
    public void PlayerProfile_DefaultPopulation_HasFourProfiles()
    {
        var population = PlayerProfile.DefaultPopulation;

        Assert.Equal(4, population.Length);
    }

    [Fact]
    public void PlayerProfile_DefaultPopulation_WeightsSumToOne()
    {
        var population = PlayerProfile.DefaultPopulation;

        float totalWeight = population.Sum(p => p.Weight);

        Assert.Equal(1.0f, totalWeight, 3); // tolerance of 0.001
    }

    private static float CalculateVariance(List<float> values)
    {
        float mean = values.Average();
        float sumSquaredDiff = values.Sum(v => (v - mean) * (v - mean));
        return sumSquaredDiff / values.Count;
    }
}

