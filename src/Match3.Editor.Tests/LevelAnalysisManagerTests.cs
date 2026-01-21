using Xunit;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Editor.Logic;
using static Match3.Editor.Tests.TestStubs;

namespace Match3.Editor.Tests
{
    public class LevelAnalysisManagerTests
    {
        [Fact]
        public void Constructor_ShouldInitializeDefaultValues()
        {
            var levelService = new StubLevelService();
            var fileSystem = new StubFileSystemService();
            var config = new LevelConfig(8, 8);

            using var manager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => config,
                () => "");

            Assert.False(manager.IsAnalyzing);
            Assert.Equal(0f, manager.WinRate);
            Assert.Equal(0f, manager.DeadlockRate);
            Assert.False(manager.IsDeepAnalysis);
            Assert.Null(manager.DeepResult);
            Assert.Null(manager.CurrentAnalysisSnapshot);
        }

        [Fact]
        public void LoadCachedAnalysis_ShouldLoadFromSnapshot_WhenExists()
        {
            var levelService = new StubLevelService
            {
                SnapshotToReturn = new LevelAnalysisSnapshot
                {
                    Basic = new BasicAnalysisData
                    {
                        WinRate = 0.75f,
                        DeadlockRate = 0.05f,
                        DifficultyRating = "中等"
                    }
                }
            };
            var fileSystem = new StubFileSystemService();
            var config = new LevelConfig(8, 8);

            using var manager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => config,
                () => "test/level.json");

            manager.LoadCachedAnalysis();

            Assert.Equal(0.75f, manager.WinRate);
            Assert.Equal(0.05f, manager.DeadlockRate);
            Assert.Contains("中等", manager.DifficultyText);
            Assert.NotNull(manager.CurrentAnalysisSnapshot);
        }

        [Fact]
        public void LoadCachedAnalysis_ShouldShowDefaultText_WhenNoSnapshot()
        {
            var levelService = new StubLevelService { SnapshotToReturn = null };
            var fileSystem = new StubFileSystemService();
            var config = new LevelConfig(8, 8);

            using var manager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => config,
                () => "test/level.json");

            manager.LoadCachedAnalysis();

            Assert.Equal(0f, manager.WinRate);
            Assert.Equal("未分析", manager.DifficultyText);
        }

        [Fact]
        public void LoadCachedAnalysis_ShouldLoadDeepResult_WhenExists()
        {
            var levelService = new StubLevelService
            {
                SnapshotToReturn = new LevelAnalysisSnapshot
                {
                    Basic = new BasicAnalysisData { WinRate = 0.5f },
                    Deep = new DeepAnalysisData
                    {
                        SkillSensitivity = 0.7f,
                        TierWinRates = new() { { "Casual", 0.4f }, { "Skilled", 0.8f } }
                    }
                }
            };
            var fileSystem = new StubFileSystemService();
            var config = new LevelConfig(8, 8);

            using var manager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => config,
                () => "test/level.json");

            manager.LoadCachedAnalysis();

            Assert.NotNull(manager.DeepResult);
        }

        [Fact]
        public void IsDeepAnalysis_ShouldBeSettable()
        {
            var levelService = new StubLevelService();
            var fileSystem = new StubFileSystemService();
            var config = new LevelConfig(8, 8);

            using var manager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => config,
                () => "");

            manager.IsDeepAnalysis = true;
            Assert.True(manager.IsDeepAnalysis);

            manager.IsDeepAnalysis = false;
            Assert.False(manager.IsDeepAnalysis);
        }

        [Fact]
        public void RestartAnalysis_ShouldSetIsAnalyzingTrue()
        {
            var levelService = new StubLevelService();
            var fileSystem = new StubFileSystemService();
            var config = new LevelConfig(8, 8);

            using var manager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => config,
                () => "");

            manager.RestartAnalysis();

            Assert.True(manager.IsAnalyzing);
        }

        [Fact]
        public void RestartAnalysis_ShouldSetProgressTextForBasicAnalysis()
        {
            var levelService = new StubLevelService();
            var fileSystem = new StubFileSystemService();
            var config = new LevelConfig(8, 8);

            using var manager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => config,
                () => "");

            manager.IsDeepAnalysis = false;
            manager.RestartAnalysis();

            Assert.Contains("500", manager.AnalysisProgressText);
        }

        [Fact]
        public void RestartAnalysis_ShouldSetProgressTextForDeepAnalysis()
        {
            var levelService = new StubLevelService();
            var fileSystem = new StubFileSystemService();
            var config = new LevelConfig(8, 8);

            using var manager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => config,
                () => "");

            manager.IsDeepAnalysis = true;
            manager.RestartAnalysis();

            Assert.Contains("Deep", manager.AnalysisProgressText);
        }

        [Fact]
        public void PropertyChanged_ShouldFireOnWinRateChange()
        {
            var levelService = new StubLevelService
            {
                SnapshotToReturn = new LevelAnalysisSnapshot
                {
                    Basic = new BasicAnalysisData { WinRate = 0.8f }
                }
            };
            var fileSystem = new StubFileSystemService();
            var config = new LevelConfig(8, 8);

            using var manager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => config,
                () => "test.json");

            var propertyChangedFired = false;
            manager.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(manager.WinRate))
                    propertyChangedFired = true;
            };

            manager.LoadCachedAnalysis();

            Assert.True(propertyChangedFired);
        }

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            var levelService = new StubLevelService();
            var fileSystem = new StubFileSystemService();
            var config = new LevelConfig(8, 8);

            var manager = new LevelAnalysisManager(
                levelService,
                fileSystem,
                () => config,
                () => "");

            manager.RestartAnalysis();

            var exception = Record.Exception(() => manager.Dispose());
            Assert.Null(exception);
        }
    }
}
