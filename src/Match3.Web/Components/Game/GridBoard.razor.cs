using System;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Match3.Core;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Match3.Web.Services;

namespace Match3.Web.Components.Game;

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

    private void HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == " " || e.Code == "Space")
        {
            GameService.TogglePause();
        }
    }

    /// <summary>
    /// Get the emoji icon for a tile visual.
    /// </summary>
    private string GetTileIcon(TileVisual visual)
    {
        // Bomb types take priority
        if (visual.BombType != BombType.None)
        {
            return visual.BombType switch
            {
                BombType.Horizontal => "↔️",
                BombType.Vertical => "↕️",
                BombType.Ufo => "🛸",
                BombType.Square5x5 => "💣",
                BombType.Color => "🌈",
                _ => ""
            };
        }

        // Rainbow tile
        if (visual.TileType.HasFlag(TileType.Rainbow)) return "🌈";

        // Regular tile colors
        if (visual.TileType.HasFlag(TileType.Red)) return "🔴";
        if (visual.TileType.HasFlag(TileType.Green)) return "🟢";
        if (visual.TileType.HasFlag(TileType.Blue)) return "🔵";
        if (visual.TileType.HasFlag(TileType.Yellow)) return "🟡";
        if (visual.TileType.HasFlag(TileType.Purple)) return "🟣";
        if (visual.TileType.HasFlag(TileType.Orange)) return "🟠";

        return "";
    }
}
