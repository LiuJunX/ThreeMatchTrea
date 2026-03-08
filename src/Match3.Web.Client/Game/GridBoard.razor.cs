using System;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Match3.Core;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Match3.Web.Client.Services;

namespace Match3.Web.Client.Game;

public partial class GridBoard : IDisposable
{
    [Inject]
    public Match3GameService GameService { get; set; } = default!;

    protected override void OnInitialized()
    {
        GameService.OnChange += OnGameStateChanged;
    }

    public void Dispose()
    {
        GameService.OnChange -= OnGameStateChanged;
    }

    private void OnGameStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void HandlePointerDown(PointerEventArgs e, int x, int y)
    {
        GameService.HandlePointerDown(x, y, e.ClientX, e.ClientY);
    }

    private void HandlePointerUp(PointerEventArgs e)
    {
        GameService.HandlePointerUp(e.ClientX, e.ClientY);
    }

    /// <summary>
    /// Get the emoji icon for a tile visual.
    /// </summary>
    private string GetTileIcon(TileVisual visual)
    {
        // Bomb types take priority
        if (visual.TileType.IsBomb())
        {
            return visual.TileType switch
            {
                ElementType.HorizontalRocket => "↔️",
                ElementType.VerticalRocket => "↕️",
                ElementType.Ufo => "🛸",
                ElementType.Square5x5 => "💣",
                ElementType.ColorBomb => "🌈",
                _ => ""
            };
        }

        // Universal (Rainbow) tile
        if (visual.TileType == ElementType.ColorBomb) return "🌈";

        // Regular tile colors
        if (visual.TileType == ElementType.Item1) return "🔴";
        if (visual.TileType == ElementType.Item2) return "🟢";
        if (visual.TileType == ElementType.Item3) return "🔵";
        if (visual.TileType == ElementType.Item4) return "🟡";
        if (visual.TileType == ElementType.Item5) return "🟣";
        if (visual.TileType == ElementType.Item6) return "🟠";

        return "";
    }

    /// <summary>
    /// Get the emoji icon for a cover type.
    /// </summary>
    private string GetCoverIcon(CoverType coverType)
    {
        return coverType switch
        {
            CoverType.Cage => "🔒",
            CoverType.Chain => "⛓️",
            CoverType.Bubble => "🫧",
            _ => ""
        };
    }
}
