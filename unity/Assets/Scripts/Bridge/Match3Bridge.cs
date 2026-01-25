using Match3.Core.Choreography;
using Match3.Core.DependencyInjection;
using Match3.Core.Models.Grid;
using Match3.Presentation;
using UnityEngine;

namespace Match3.Unity.Bridge
{
    /// <summary>
    /// Bridge between Match3 Core DLL and Unity.
    /// Manages GameSession, Choreographer, and Player.
    /// </summary>
    public sealed class Match3Bridge : MonoBehaviour
    {
        [Header("Board Configuration")]
        [SerializeField] private int _width = 8;
        [SerializeField] private int _height = 8;
        [SerializeField] private int _seed = 0;

        [Header("Visual Configuration")]
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private Vector2 _boardOrigin = Vector2.zero;

        private IGameServiceFactory _factory;
        private GameSession _session;
        private Choreographer _choreographer;
        private Player _player;

        private bool _initialized;

        /// <summary>
        /// Cell size in world units.
        /// </summary>
        public float CellSize => _cellSize;

        /// <summary>
        /// Board origin in world space.
        /// </summary>
        public Vector2 BoardOrigin => _boardOrigin;

        /// <summary>
        /// Board width in cells.
        /// </summary>
        public int Width => _width;

        /// <summary>
        /// Board height in cells.
        /// </summary>
        public int Height => _height;

        /// <summary>
        /// Current visual state for rendering.
        /// </summary>
        public VisualState VisualState => _player?.VisualState;

        /// <summary>
        /// Whether there are active animations.
        /// </summary>
        public bool HasActiveAnimations => _player?.HasActiveAnimations ?? false;

        /// <summary>
        /// Whether the bridge is initialized.
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Current game state reference.
        /// </summary>
        public GameState CurrentState => _session.Engine.State;

        /// <summary>
        /// Initialize the bridge with default or serialized parameters.
        /// </summary>
        public void Initialize()
        {
            Initialize(_width, _height, _seed != 0 ? _seed : System.Environment.TickCount);
        }

        /// <summary>
        /// Initialize the bridge with explicit parameters.
        /// </summary>
        public void Initialize(int width, int height, int seed)
        {
            if (_initialized)
            {
                Cleanup();
            }

            _width = width;
            _height = height;
            _seed = seed;

            // Create factory with default services
            _factory = new GameServiceBuilder()
                .UseDefaultServices()
                .Build();

            // Create game session
            var config = new GameServiceConfiguration
            {
                Width = width,
                Height = height,
                RngSeed = seed,
                EnableEventCollection = true
            };
            _session = _factory.CreateGameSession(config);

            // Create choreographer and player
            _choreographer = new Choreographer();
            _player = new Player();

            // Sync initial state
            var state = _session.Engine.State;
            _player.SyncFromGameState(in state);

            _initialized = true;

            Debug.Log($"Match3Bridge initialized: {width}x{height}, seed={seed}");
        }

        /// <summary>
        /// Update the bridge by one frame.
        /// Processes simulation, choreography, and animations.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_initialized) return;

            // Tick the simulation engine
            _session.Engine.Tick(deltaTime);

            // Drain events and convert to render commands
            var events = _session.DrainEvents();
            if (events.Count > 0)
            {
                var commands = _choreographer.Choreograph(events, _player.CurrentTime);
                _player.Append(commands);
            }

            // Tick the animation player
            _player.Tick(deltaTime);

            // Sync falling tiles from game state (physics-driven positions)
            if (!HasActiveAnimations)
            {
                var state = _session.Engine.State;
                _player.VisualState.SyncFallingTilesFromGameState(in state);
            }
        }

        /// <summary>
        /// Apply a move from position A to position B.
        /// </summary>
        public bool ApplyMove(Position from, Position to)
        {
            if (!_initialized) return false;

            // Check if animations are still playing
            if (HasActiveAnimations)
            {
                Debug.Log("Cannot apply move while animations are playing");
                return false;
            }

            // Check if positions are adjacent
            if (!AreAdjacent(from, to))
            {
                Debug.Log($"Positions are not adjacent: {from} -> {to}");
                return false;
            }

            // Get tiles at positions
            var state = _session.Engine.State;
            var tileA = state.GetTile(from.X, from.Y);
            var tileB = state.GetTile(to.X, to.Y);

            if (tileA.Type == Core.Models.Enums.TileType.None ||
                tileB.Type == Core.Models.Enums.TileType.None)
            {
                return false;
            }

            // Apply swap through simulation engine
            return _session.Engine.ApplyMove(from, to);
        }

        /// <summary>
        /// Check if the game is idle (no active moves or animations).
        /// </summary>
        public bool IsIdle()
        {
            return _initialized && !HasActiveAnimations && _session.Engine.IsStable();
        }

        /// <summary>
        /// Handle a tap at the specified grid position.
        /// Delegates to Core's SimulationEngine.HandleTap for selection/bomb activation logic.
        /// </summary>
        public void HandleTap(Position pos)
        {
            if (!_initialized) return;
            if (HasActiveAnimations) return;

            _session.Engine.HandleTap(pos);
        }

        /// <summary>
        /// Get tile ID at grid position.
        /// Returns -1 if no tile at position.
        /// </summary>
        public long GetTileIdAt(Position pos)
        {
            if (!_initialized) return -1;

            var state = _session.Engine.State;
            if (pos.X < 0 || pos.X >= state.Width || pos.Y < 0 || pos.Y >= state.Height)
                return -1;

            var tile = state.GetTile(pos.X, pos.Y);
            return tile.Type != Core.Models.Enums.TileType.None ? tile.Id : -1;
        }

        private static bool AreAdjacent(Position a, Position b)
        {
            int dx = System.Math.Abs(a.X - b.X);
            int dy = System.Math.Abs(a.Y - b.Y);
            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        }

        private void Cleanup()
        {
            _session?.Dispose();
            _session = null;
            _player = null;
            _choreographer = null;
            _initialized = false;
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
