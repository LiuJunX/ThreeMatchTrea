using Bunit;
using Xunit;
using Match3.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;
using Match3.Core;
using Match3.Core.Structs;
using Match3.Web.Services;
using Microsoft.AspNetCore.Components.Web;

namespace Match3.Web.Tests;

public class HomeTests : TestContext
{
    public HomeTests()
    {
        Services.AddLogging();
        Services.AddScoped<Match3GameService>();
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
        
        // Assert: Verify status becomes "Select destination"
        cut.WaitForAssertion(() => 
        {
            var status = cut.Find("[data-testid='status-message']");
            Assert.Contains("Select destination", status.TextContent);
        });
        
        // Verify tile has 'selected' class
        // Note: we need to re-query or check if bUnit updates the reference. 
        // bUnit elements are live, but let's re-find to be safe and check class attribute.
        firstTile = cut.Find(".tile"); // Get the first one again, it should be the same one we clicked if layout didn't shift
        Assert.Contains("selected", firstTile.ClassName);
    }
}
