using System.Threading.Tasks;
using Xunit;
using Match3.Editor.Logic;
using Match3.Editor.ViewModels;
using static Match3.Editor.Tests.TestStubs;

namespace Match3.Editor.Tests
{
    public class ScenarioFileManagerTests
    {
        private ScenarioFileManager CreateManager(
            StubPlatformService? platform = null,
            StubScenarioService? scenarioService = null,
            StubFileSystemService? fileSystem = null,
            bool isDirty = false,
            string scenarioName = "TestScenario",
            string exportedJson = "{}")
        {
            platform ??= new StubPlatformService();
            scenarioService ??= new StubScenarioService();
            fileSystem ??= new StubFileSystemService();

            var currentScenarioName = scenarioName;

            return new ScenarioFileManager(
                platform,
                scenarioService,
                fileSystem,
                () => isDirty,
                dirty => { },
                () => currentScenarioName,
                name => currentScenarioName = name,
                () => exportedJson,
                (json, keepScenarioMode) => { });
        }

        [Fact]
        public void Constructor_ShouldInitializeEmptyState()
        {
            var manager = CreateManager();

            Assert.Null(manager.RootFolderNode);
            Assert.Equal("", manager.CurrentFilePath);
            Assert.Empty(manager.ExpandedPaths);
            Assert.Empty(manager.SearchResults);
        }

        [Fact]
        public void RefreshScenarioList_ShouldBuildTree()
        {
            var scenarioService = new StubScenarioService();
            var manager = CreateManager(scenarioService: scenarioService);

            manager.RefreshScenarioList();

            Assert.NotNull(manager.RootFolderNode);
            Assert.Equal("scenarios", manager.RootFolderNode!.Name);
        }

        [Fact]
        public void SetRootFolder_ShouldUpdateRootNode()
        {
            var manager = CreateManager();
            var node = new ScenarioFolderNode(
                "custom", "custom/path",
                System.Array.Empty<ScenarioFolderNode>(),
                System.Array.Empty<ScenarioFileEntry>());

            manager.SetRootFolder(node);

            Assert.Equal("custom", manager.RootFolderNode!.Name);
        }

        [Fact]
        public async Task LoadScenarioAsync_ShouldLoadScenario_WhenNotDirty()
        {
            var scenarioService = new StubScenarioService { JsonToReturn = "{\"Operations\":[]}" };
            var manager = CreateManager(scenarioService: scenarioService, isDirty: false);

            await manager.LoadScenarioAsync("test/scenario.json");

            Assert.Equal("test/scenario.json", manager.CurrentFilePath);
        }

        [Fact]
        public async Task LoadScenarioAsync_ShouldAskToSave_WhenDirty()
        {
            var platform = new StubPlatformService { ConfirmResult = false };
            var scenarioService = new StubScenarioService();

            var currentIsDirty = true;
            var manager = new ScenarioFileManager(
                platform,
                scenarioService,
                new StubFileSystemService(),
                () => currentIsDirty,
                dirty => currentIsDirty = dirty,
                () => "Test",
                name => { },
                () => "{}",
                (json, _) => { });

            manager.CurrentFilePath = "existing.json";
            await manager.LoadScenarioAsync("new.json");

            Assert.True(platform.ConfirmCallCount > 0);
        }

        [Fact]
        public async Task LoadScenarioAsync_ShouldShowError_OnException()
        {
            var platform = new StubPlatformService();
            var scenarioService = new StubScenarioService { ThrowOnRead = true };
            var manager = CreateManager(platform: platform, scenarioService: scenarioService);

            await manager.LoadScenarioAsync("test.json");

            Assert.Single(platform.AlertMessages);
            Assert.Contains("Error", platform.AlertMessages[0]);
        }

        [Fact]
        public async Task SaveScenarioAsync_ShouldWriteJson()
        {
            var scenarioService = new StubScenarioService();
            var manager = CreateManager(
                scenarioService: scenarioService,
                scenarioName: "MyScenario",
                exportedJson: "{\"Operations\":[]}");
            manager.CurrentFilePath = "test/MyScenario.json";

            await manager.SaveScenarioAsync();

            Assert.Equal("test/MyScenario.json", scenarioService.LastWrittenPath);
            Assert.Equal("{\"Operations\":[]}", scenarioService.LastWrittenJson);
        }

        [Fact]
        public async Task SaveScenarioAsync_ShouldDoNothing_WhenNoPath()
        {
            var scenarioService = new StubScenarioService();
            var manager = CreateManager(scenarioService: scenarioService);

            await manager.SaveScenarioAsync();

            Assert.Null(scenarioService.LastWrittenPath);
        }

        [Fact]
        public async Task SaveScenarioAsync_ShouldRename_WhenNameChanged()
        {
            var scenarioService = new StubScenarioService();
            var currentName = "NewName";
            var manager = new ScenarioFileManager(
                new StubPlatformService(),
                scenarioService,
                new StubFileSystemService(),
                () => false,
                dirty => { },
                () => currentName,
                name => currentName = name,
                () => "{}",
                (json, _) => { });

            manager.CurrentFilePath = "test/OldName.json";
            await manager.SaveScenarioAsync();

            Assert.Equal("test/OldName.json", scenarioService.LastRenamedPath);
            Assert.Equal("NewName", scenarioService.LastRenamedNewName);
        }

        [Fact]
        public async Task CreateNewScenarioAsync_ShouldCallService()
        {
            var scenarioService = new StubScenarioService();
            var manager = CreateManager(scenarioService: scenarioService);

            await manager.CreateNewScenarioAsync("folder");

            Assert.NotNull(scenarioService.LastCreatedPath);
        }

        [Fact]
        public async Task DuplicateScenarioAsync_ShouldCallService()
        {
            var scenarioService = new StubScenarioService();
            var manager = CreateManager(scenarioService: scenarioService);

            await manager.DuplicateScenarioAsync("test/original.json");

            Assert.Equal("test/original.json", scenarioService.LastDuplicatedPath);
        }

        [Fact]
        public async Task DeleteFileAsync_ShouldDelete_WhenConfirmed()
        {
            var platform = new StubPlatformService { ConfirmResult = true };
            var scenarioService = new StubScenarioService();
            var manager = CreateManager(platform: platform, scenarioService: scenarioService);

            await manager.DeleteFileAsync("test/scenario.json", isFolder: false);

            Assert.Equal("test/scenario.json", scenarioService.LastDeletedPath);
        }

        [Fact]
        public async Task DeleteFileAsync_ShouldNotDelete_WhenNotConfirmed()
        {
            var platform = new StubPlatformService { ConfirmResult = false };
            var scenarioService = new StubScenarioService();
            var manager = CreateManager(platform: platform, scenarioService: scenarioService);

            await manager.DeleteFileAsync("test/scenario.json", isFolder: false);

            Assert.Null(scenarioService.LastDeletedPath);
        }

        [Fact]
        public async Task DeleteFileAsync_ShouldDeleteFolder_WhenIsFolder()
        {
            var platform = new StubPlatformService { ConfirmResult = true };
            var scenarioService = new StubScenarioService();
            var manager = CreateManager(platform: platform, scenarioService: scenarioService);

            await manager.DeleteFileAsync("test/folder", isFolder: true);

            Assert.Equal("test/folder", scenarioService.LastDeletedPath);
        }

        [Fact]
        public void PropertyChanged_ShouldFireOnRefresh()
        {
            var manager = CreateManager();
            var propertyChangedFired = false;

            manager.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(manager.RootFolderNode))
                    propertyChangedFired = true;
            };

            manager.RefreshScenarioList();

            Assert.True(propertyChangedFired);
        }
    }
}
