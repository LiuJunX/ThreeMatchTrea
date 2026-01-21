using System.Threading.Tasks;
using Xunit;
using Match3.Editor.Logic;
using Match3.Editor.ViewModels;
using static Match3.Editor.Tests.TestStubs;

namespace Match3.Editor.Tests
{
    public class LevelFileManagerTests
    {
        private LevelFileManager CreateManager(
            StubPlatformService? platform = null,
            StubLevelService? levelService = null,
            StubFileSystemService? fileSystem = null,
            bool isDirty = false,
            string exportedJson = "{}")
        {
            platform ??= new StubPlatformService();
            levelService ??= new StubLevelService();
            fileSystem ??= new StubFileSystemService();

            return new LevelFileManager(
                platform,
                levelService,
                fileSystem,
                () => isDirty,
                dirty => { },
                () => exportedJson,
                (json, keepScenarioMode) => { });
        }

        [Fact]
        public void Constructor_ShouldInitializeEmptyState()
        {
            var manager = CreateManager();

            Assert.Null(manager.RootLevelFolderNode);
            Assert.Equal("", manager.CurrentLevelFilePath);
            Assert.Empty(manager.LevelExpandedPaths);
        }

        [Fact]
        public void RefreshLevelList_ShouldBuildTree()
        {
            var levelService = new StubLevelService();
            var manager = CreateManager(levelService: levelService);

            manager.RefreshLevelList();

            Assert.NotNull(manager.RootLevelFolderNode);
            Assert.Equal("levels", manager.RootLevelFolderNode!.Name);
        }

        [Fact]
        public void SetRootLevelFolder_ShouldUpdateRootNode()
        {
            var manager = CreateManager();
            var node = new ScenarioFolderNode(
                "custom", "custom/path",
                System.Array.Empty<ScenarioFolderNode>(),
                System.Array.Empty<ScenarioFileEntry>());

            manager.SetRootLevelFolder(node);

            Assert.Equal("custom", manager.RootLevelFolderNode!.Name);
        }

        [Fact]
        public async Task LoadLevelAsync_ShouldLoadLevel_WhenNotDirty()
        {
            var levelService = new StubLevelService { JsonToReturn = "{\"Width\":8}" };
            var manager = CreateManager(levelService: levelService, isDirty: false);

            await manager.LoadLevelAsync("test/level.json");

            Assert.Equal("test/level.json", manager.CurrentLevelFilePath);
        }

        [Fact]
        public async Task LoadLevelAsync_ShouldAskToSave_WhenDirty()
        {
            var platform = new StubPlatformService { ConfirmResult = false };
            var levelService = new StubLevelService();

            var currentIsDirty = true;
            var manager = new LevelFileManager(
                platform,
                levelService,
                new StubFileSystemService(),
                () => currentIsDirty,
                dirty => currentIsDirty = dirty,
                () => "{}",
                (json, _) => { });

            manager.CurrentLevelFilePath = "existing.json";
            await manager.LoadLevelAsync("new.json");

            Assert.True(platform.ConfirmCallCount > 0);
        }

        [Fact]
        public async Task LoadLevelAsync_ShouldShowError_OnException()
        {
            var platform = new StubPlatformService();
            var levelService = new StubLevelService { ThrowOnRead = true };
            var manager = CreateManager(platform: platform, levelService: levelService);

            await manager.LoadLevelAsync("test.json");

            Assert.Single(platform.AlertMessages);
            Assert.Contains("Error", platform.AlertMessages[0]);
        }

        [Fact]
        public async Task SaveLevelAsync_ShouldWriteJson()
        {
            var levelService = new StubLevelService();
            var manager = CreateManager(levelService: levelService, exportedJson: "{\"Width\":10}");
            manager.CurrentLevelFilePath = "test/level.json";

            await manager.SaveLevelAsync();

            Assert.Equal("test/level.json", levelService.LastWrittenPath);
            Assert.Equal("{\"Width\":10}", levelService.LastWrittenJson);
        }

        [Fact]
        public async Task SaveLevelAsync_ShouldDoNothing_WhenNoPath()
        {
            var levelService = new StubLevelService();
            var manager = CreateManager(levelService: levelService);

            await manager.SaveLevelAsync();

            Assert.Null(levelService.LastWrittenPath);
        }

        [Fact]
        public async Task CreateNewLevelAsync_ShouldCreateAndSetPath()
        {
            var levelService = new StubLevelService();
            var manager = CreateManager(levelService: levelService, exportedJson: "{}");

            await manager.CreateNewLevelAsync("folder");

            Assert.NotNull(levelService.LastCreatedPath);
            Assert.Equal(levelService.LastCreatedPath, manager.CurrentLevelFilePath);
        }

        [Fact]
        public async Task DuplicateLevelAsync_ShouldCallService()
        {
            var levelService = new StubLevelService();
            var manager = CreateManager(levelService: levelService);

            await manager.DuplicateLevelAsync("test/original.json");

            Assert.Equal("test/original.json", levelService.LastDuplicatedPath);
        }

        [Fact]
        public async Task DeleteLevelFileAsync_ShouldDelete_WhenConfirmed()
        {
            var platform = new StubPlatformService { ConfirmResult = true };
            var levelService = new StubLevelService();
            var manager = CreateManager(platform: platform, levelService: levelService);

            await manager.DeleteLevelFileAsync("test/level.json", isFolder: false);

            Assert.Equal("test/level.json", levelService.LastDeletedPath);
        }

        [Fact]
        public async Task DeleteLevelFileAsync_ShouldNotDelete_WhenNotConfirmed()
        {
            var platform = new StubPlatformService { ConfirmResult = false };
            var levelService = new StubLevelService();
            var manager = CreateManager(platform: platform, levelService: levelService);

            await manager.DeleteLevelFileAsync("test/level.json", isFolder: false);

            Assert.Null(levelService.LastDeletedPath);
        }

        [Fact]
        public void PropertyChanged_ShouldFireOnRefresh()
        {
            var manager = CreateManager();
            var propertyChangedFired = false;

            manager.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(manager.RootLevelFolderNode))
                    propertyChangedFired = true;
            };

            manager.RefreshLevelList();

            Assert.True(propertyChangedFired);
        }
    }
}
