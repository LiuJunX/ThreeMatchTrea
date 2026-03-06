using System.Collections.Generic;
using System.Text;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using Match3.Unity.Views;
using UnityEngine;

namespace Match3.Unity.Controllers
{
    /// <summary>
    /// Pipeline diagnostics: validates data consistency across Core → VisualState → View.
    /// Toggle with F9 in Editor. When a mismatch is detected, logs the exact layer where
    /// data diverges, so the user can tell the AI "bug is in layer X".
    ///
    /// Usage: attach to the same GameObject as GameController, or let GameBootstrap add it.
    /// </summary>
    public sealed class PipelineDiagnostics : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Validate every N frames (1 = every frame)")]
        [SerializeField] private int _validateInterval = 30;

        [Tooltip("Enable diagnostics (toggle with F9)")]
        [SerializeField] private bool _enabled;

        private Match3Bridge _bridge;
        private GameController _gameController;
        private int _frameCounter;

        // Reusable collections to avoid GC
        private readonly StringBuilder _sb = new();
        private readonly List<string> _mismatches = new();

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public void Initialize(Match3Bridge bridge, GameController gameController)
        {
            _bridge = bridge;
            _gameController = gameController;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F9))
            {
                _enabled = !_enabled;
                Debug.Log($"[PipelineDiag] {(_enabled ? "ON" : "OFF")}");
            }
#endif
            if (!_enabled || _bridge == null) return;

            _frameCounter++;
            if (_frameCounter % _validateInterval != 0) return;

            Validate();
        }

        /// <summary>
        /// Run a full pipeline validation. Can also be called manually from console.
        /// </summary>
        public void Validate()
        {
            _mismatches.Clear();

            var visualState = _bridge.VisualState;
            if (visualState == null) return;

            var gameState = _bridge.CurrentState;
            if (gameState.Grid == null) return;

            // Checkpoint 1: GameState ↔ VisualState
            ValidateCoreToVisualState(ref gameState, visualState);

            // Checkpoint 2: VisualState ↔ View (renderer)
            ValidateVisualStateToView(visualState);

            if (_mismatches.Count > 0)
            {
                _sb.Clear();
                _sb.AppendLine($"[PipelineDiag] {_mismatches.Count} mismatch(es) detected:");
                foreach (var m in _mismatches)
                    _sb.AppendLine(m);
                Debug.LogWarning(_sb.ToString());
            }
        }

        private void ValidateCoreToVisualState(ref GameState gameState, VisualState visualState)
        {
            // Build a lookup of Core tile IDs → types
            for (int y = 0; y < gameState.Height; y++)
            {
                for (int x = 0; x < gameState.Width; x++)
                {
                    var tile = gameState.GetTile(x, y);
                    if (tile.Type == TileType.None || tile.Type == TileType.Wall) continue;

                    var visual = visualState.GetTile(tile.Id);
                    if (visual == null)
                    {
                        // Tile in Core but not in VisualState — might be in animation, skip
                        continue;
                    }

                    if (visual.TileType != tile.Type)
                    {
                        _mismatches.Add(
                            $"  [Core→VisualState] Tile {tile.Id} at ({x},{y}): " +
                            $"Core={tile.Type}, VisualState={visual.TileType}");
                    }

                    if (visual.BombType != tile.Bomb)
                    {
                        _mismatches.Add(
                            $"  [Core→VisualState] Tile {tile.Id} at ({x},{y}): " +
                            $"Core.Bomb={tile.Bomb}, VisualState.Bomb={visual.BombType}");
                    }
                }
            }
        }

        private void ValidateVisualStateToView(VisualState visualState)
        {
            var boardView = _gameController.BoardView;
            if (boardView == null) return;

            // Check based on concrete type
            if (boardView is Board3DView board3D)
                ValidateView3D(visualState, board3D);
            else if (boardView is BoardView board2D)
                ValidateView2D(visualState, board2D);
        }

        private void ValidateView3D(VisualState visualState, Board3DView board3D)
        {
            foreach (var kvp in visualState.Tiles)
            {
                var visual = kvp.Value;
                if (!visual.IsVisible) continue;

                // Get expected mesh/material from factory
                var expectedMesh = visual.BombType != BombType.None
                    ? MeshFactory.GetBombMesh(visual.BombType)
                    : MeshFactory.GetTileMesh(visual.TileType);

                var expectedMats = MeshFactory.GetTileMaterialArray(visual.TileType, visual.BombType);

                // Access the tile's renderer via the board
                var tileGo = board3D.GetTileGameObject(kvp.Key);
                if (tileGo == null) continue;

                var meshFilter = tileGo.GetComponent<MeshFilter>();
                var meshRenderer = tileGo.GetComponent<MeshRenderer>();
                if (meshFilter == null || meshRenderer == null) continue;

                if (meshFilter.sharedMesh != expectedMesh)
                {
                    _mismatches.Add(
                        $"  [VisualState→View] Tile {kvp.Key}: " +
                        $"expected mesh for {visual.TileType}/{visual.BombType}, got different mesh");
                }

                var currentMats = meshRenderer.sharedMaterials;
                if (currentMats.Length != expectedMats.Length ||
                    (currentMats.Length > 0 && currentMats[0] != expectedMats[0]))
                {
                    _mismatches.Add(
                        $"  [VisualState→View] Tile {kvp.Key}: " +
                        $"expected material for {visual.TileType}/{visual.BombType}, got different material");
                }
            }
        }

        private void ValidateView2D(VisualState visualState, BoardView board2D)
        {
            foreach (var kvp in visualState.Tiles)
            {
                var visual = kvp.Value;
                if (!visual.IsVisible) continue;

                var tileView = board2D.GetTileView(kvp.Key);
                if (tileView == null) continue;

                var renderer = tileView.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;

                var expectedSprite = SpriteFactory.GetTileSprite(visual.TileType);
                if (renderer.sprite != expectedSprite)
                {
                    _mismatches.Add(
                        $"  [VisualState→View] Tile {kvp.Key}: " +
                        $"expected sprite for {visual.TileType}, got different sprite");
                }
            }
        }
    }
}
