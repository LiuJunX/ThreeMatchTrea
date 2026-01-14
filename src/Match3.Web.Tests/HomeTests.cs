using Bunit;
using Xunit;
using Match3.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;
using Match3.Core;
using Match3.Core.Models.Grid;
using Match3.Web.Services;
using Microsoft.AspNetCore.Components.Web;

using System.IO;

namespace Match3.Web.Tests;

public class HomeTests : TestContext, IDisposable
{
    private readonly string _tempDir;

    public HomeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Match3Tests_Home_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);

        Services.AddLogging();
        Services.AddScoped<Match3GameService>();
        Services.AddScoped<Match3.Editor.Interfaces.IJsonService, Match3.Web.Services.EditorAdapters.SystemTextJsonService>();
        Services.AddScoped<ScenarioLibraryService>(_ => new ScenarioLibraryService(_tempDir));
    }

    public new void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
        base.Dispose();
    }

    [Fact]
    public void GameBoard_Should_Render_Correctly()
    {
        // Act: Render Home component
        var cut = RenderComponent<Home>();

        // Assert: Verify title exists
        cut.Find("h1").MarkupMatches("<h1>Match3 Blast</h1>");

        // Assert: Verify game board generation (wait for async init)
        cut.WaitForAssertion(() => 
        {
            var board = cut.Find(".board-container");
            Assert.NotNull(board);
        });

        // Verify status message
        var status = cut.Find("[data-testid='status-message']");
        Assert.Contains("Ready", status.TextContent);
        
        // Verify tiles are rendered (8x8 board, so at least some tiles should exist)
        var tiles = cut.FindAll(".tile");
        Assert.True(tiles.Count > 0, "Should have tiles rendered");
    }
    
    [Fact]
    public void ClickTile_Should_SelectIt()
    {
        // Act
        var cut = RenderComponent<Home>();
        
        // Wait for board to load
        cut.WaitForAssertion(() => cut.Find(".tile"));
        
        // Find first tile and click
        var firstTile = cut.Find(".tile");
        var down = new PointerEventArgs { ClientX = 0, ClientY = 0 };
        firstTile.TriggerEvent("onpointerdown", down);
        var board = cut.Find(".board-container");
        var up = new PointerEventArgs { ClientX = 0, ClientY = 0 };
        board.TriggerEvent("onpointerup", up);

        // Manually trigger frame update to process the TapIntent
        // (bUnit's async Task.Delay doesn't work properly in test context)
        var gameService = Services.GetRequiredService<Match3GameService>();
        gameService.ManualUpdate();

        // Force re-render after engine state update
        cut.Render();

        // Assert: Verify status becomes "Select destination"
        cut.WaitForAssertion(() =>
        {
            var status = cut.Find("[data-testid='status-message']");
            Assert.Contains("Select destination", status.TextContent);
        });
        
        // Verify a tile has 'selected' class
        // Note: Grid tiles are rendered in State.Grid order, not position order.
        // After clicking, we need to find the tile that has the 'selected' class.
        // Use WaitForAssertion to ensure GridBoard has re-rendered (it uses InvokeAsync).
        cut.WaitForAssertion(() =>
        {
            var selectedTile = cut.Find(".tile.selected");
            Assert.NotNull(selectedTile);
        });
    }
}
