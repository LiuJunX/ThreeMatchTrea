using System.Collections.Generic;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Manages the visual representation of the game board.
    /// Handles tile and projectile view lifecycle and rendering.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private ObjectPool<TileView> _tilePool;
        private ObjectPool<ProjectileView> _projectilePool;
        private readonly Dictionary<long, TileView> _activeTiles = new();
        private readonly Dictionary<long, ProjectileView> _activeProjectiles = new();

        private Match3Bridge _bridge;
        private Transform _tileContainer;
        private Transform _projectileContainer;

        /// <summary>
        /// Number of active tile views.
        /// </summary>
        public int ActiveTileCount => _activeTiles.Count;

        /// <summary>
        /// Number of active projectile views.
        /// </summary>
        public int ActiveProjectileCount => _activeProjectiles.Count;

        /// <summary>
        /// Initialize the board view.
        /// </summary>
        public void Initialize(Match3Bridge bridge)
        {
            _bridge = bridge;

            // Create tile container
            _tileContainer = new GameObject("TileContainer").transform;
            _tileContainer.SetParent(transform, false);

            // Create projectile container
            _projectileContainer = new GameObject("ProjectileContainer").transform;
            _projectileContainer.SetParent(transform, false);

            // Create tile pool
            _tilePool = new ObjectPool<TileView>(
                factory: () => ViewFactory.CreateTileView(_tileContainer),
                parent: _tileContainer,
                initialSize: 64,
                maxSize: 128
            );

            // Create projectile pool
            _projectilePool = new ObjectPool<ProjectileView>(
                factory: () => ViewFactory.CreateProjectileView(_projectileContainer),
                parent: _projectileContainer,
                initialSize: 5,
                maxSize: 20
            );
        }

        /// <summary>
        /// Render the current visual state.
        /// Called every frame by GameController.
        /// </summary>
        public void Render(VisualState state)
        {
            if (state == null) return;

            var cellSize = _bridge.CellSize;
            var origin = _bridge.BoardOrigin;
            var height = _bridge.Height;

            // Track which tiles are still active
            var stillActive = new HashSet<long>();

            // Update existing tiles and create new ones
            foreach (var kvp in state.Tiles)
            {
                var tileId = kvp.Key;
                var visual = kvp.Value;

                if (!visual.IsVisible) continue;

                stillActive.Add(tileId);

                if (!_activeTiles.TryGetValue(tileId, out var tileView))
                {
                    // Create new tile view
                    tileView = _tilePool.Rent();
                    tileView.Setup(tileId, visual.TileType, visual.BombType);
                    _activeTiles[tileId] = tileView;
                }

                // Update tile view from visual state
                tileView.UpdateFromVisual(visual, cellSize, origin, height);
            }

            // Remove tiles that are no longer active
            var toRemove = new List<long>();
            foreach (var kvp in _activeTiles)
            {
                if (!stillActive.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var tileId in toRemove)
            {
                if (_activeTiles.TryGetValue(tileId, out var tileView))
                {
                    _tilePool.Return(tileView);
                    _activeTiles.Remove(tileId);
                }
            }

            // Render projectiles
            RenderProjectiles(state, cellSize, origin, height);
        }

        private void RenderProjectiles(VisualState state, float cellSize, Vector2 origin, int height)
        {
            // Track which projectiles are still active
            var stillActive = new HashSet<long>();

            // Update existing projectiles and create new ones
            foreach (var kvp in state.Projectiles)
            {
                var projectileId = kvp.Key;
                var visual = kvp.Value;

                if (!visual.IsVisible) continue;

                stillActive.Add(projectileId);

                if (!_activeProjectiles.TryGetValue(projectileId, out var projectileView))
                {
                    // Create new projectile view
                    projectileView = _projectilePool.Rent();
                    projectileView.Setup(projectileId);
                    _activeProjectiles[projectileId] = projectileView;
                }

                // Update projectile view from visual state
                projectileView.UpdateFromVisual(visual, cellSize, origin, height);
            }

            // Remove projectiles that are no longer active
            var toRemove = new List<long>();
            foreach (var kvp in _activeProjectiles)
            {
                if (!stillActive.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var projectileId in toRemove)
            {
                if (_activeProjectiles.TryGetValue(projectileId, out var projectileView))
                {
                    _projectilePool.Return(projectileView);
                    _activeProjectiles.Remove(projectileId);
                }
            }
        }

        /// <summary>
        /// Get tile view by ID.
        /// </summary>
        public TileView GetTileView(long tileId)
        {
            _activeTiles.TryGetValue(tileId, out var view);
            return view;
        }

        /// <summary>
        /// Get projectile view by ID.
        /// </summary>
        public ProjectileView GetProjectileView(long projectileId)
        {
            _activeProjectiles.TryGetValue(projectileId, out var view);
            return view;
        }

        /// <summary>
        /// Clear all tiles and projectiles.
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _activeTiles)
            {
                _tilePool.Return(kvp.Value);
            }
            _activeTiles.Clear();

            foreach (var kvp in _activeProjectiles)
            {
                _projectilePool.Return(kvp.Value);
            }
            _activeProjectiles.Clear();
        }

        private void OnDestroy()
        {
            Clear();
            _tilePool?.Clear();
            _projectilePool?.Clear();
        }
    }
}
