using System.Collections.Generic;
using Match3.Core.Models.Grid;
using Match3.Core.Systems.Physics;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using Match3.Unity.Services;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// 3D board view. Renders tiles as 3D meshes.
    /// Implements IBoardView for swappable 2D/3D rendering.
    /// </summary>
    public sealed class Board3DView : MonoBehaviour, IBoardView
    {
        private ObjectPool<Tile3DView> _tilePool;
        private ObjectPool<Projectile3DView> _projectilePool;
        private readonly Dictionary<int, Tile3DView> _activeTiles = new();
        private readonly Dictionary<int, Projectile3DView> _activeProjectiles = new();

        // Pre-allocated collections to avoid GC in hot path
        private readonly HashSet<int> _activeTileIds = new();
        private readonly HashSet<int> _activeProjectileIds = new();
        private readonly List<int> _tilesToRemove = new();
        private readonly List<int> _projectilesToRemove = new();

        private Match3Bridge _bridge;
        private Transform _tileContainer;
        private Transform _projectileContainer;
        private BoardLightingController _lightingController;
        private GameObject _boardFloor;
        private GameObject _boardVignette;
        private bool _viewInitialized;
        private int _highlightedTileId = -1;

        private ObjectiveDisplayController _objectiveDisplay;

        // Per-column hole zone info for portal visual effects (shader clip)
        private struct HoleZone
        {
            public int EntryY;         // first hole row (grid coords, Y-down)
            public int ExitY;          // last hole row
            public float HoleTopWorldY; // world Y of top edge of entry cell
            public float HoleBotWorldY; // world Y of bottom edge of exit cell
            public float MidGridY;      // midpoint in grid Y for side detection
        }
        private readonly Dictionary<int, HoleZone> _columnHoleZones = new();

        public int ActiveTileCount => _activeTiles.Count;

        /// <summary>
        /// Set by GameController to enable fly-to-objective interception.
        /// </summary>
        internal ObjectiveDisplayController ObjectiveDisplay
        {
            set => _objectiveDisplay = value;
        }

        /// <summary>
        /// Expose pool so ObjectiveDisplayController can return tiles after flying.
        /// </summary>
        internal ObjectPool<Tile3DView> TilePool => _tilePool;

        public void Initialize(Match3Bridge bridge)
        {
            _bridge = bridge;

            if (!_viewInitialized)
            {
                // Auto-boot render tuning (no manual scene wiring required).
                // If a RenderTuningSettings asset exists, it will be applied immediately.
                RenderTuningRuntime.EnsureController();

                // Setup lighting controller (owns key/fill/rim/selection lights)
                var lightingGo = new GameObject("BoardLighting");
                lightingGo.transform.SetParent(transform, false);
                _lightingController = lightingGo.AddComponent<BoardLightingController>();
                _lightingController.Initialize();

                // Setup environment (ambient, reflections, camera background)
                SetupEnvironment();

                _tileContainer = new GameObject("TileContainer3D").transform;
                _tileContainer.SetParent(transform, false);
                // Per-tile tilt is applied individually in Tile3DView (BaseTiltX),
                // so the container stays axis-aligned for clean coordinates.


                _projectileContainer = new GameObject("ProjectileContainer3D").transform;
                _projectileContainer.SetParent(transform, false);

                var (tileInitial, tileMax) = GetPoolSize("tiles", 64, 128);
                var (projInitial, projMax) = GetPoolSize("projectiles", 5, 20);

                _tilePool = new ObjectPool<Tile3DView>(
                    factory: () => CreateTile3DView(_tileContainer),
                    parent: _tileContainer,
                    initialSize: tileInitial,
                    maxSize: tileMax
                );

                _projectilePool = new ObjectPool<Projectile3DView>(
                    factory: () => CreateProjectile3DView(_projectileContainer),
                    parent: _projectileContainer,
                    initialSize: projInitial,
                    maxSize: projMax
                );

                _viewInitialized = true;
            }

            // Always rebuild floor and vignette (board size/shape may change between levels)
            RebuildBoardFloor();
            RebuildBoardVignette();
            BuildHoleZones();
        }

        private void RebuildBoardFloor()
        {
            if (_boardFloor != null)
            {
                Destroy(_boardFloor);
                _boardFloor = null;
            }

            var width = _bridge.Width;
            var height = _bridge.Height;
            var cellSize = _bridge.CellSize;
            var origin = _bridge.BoardOrigin;

            // Use actual grid layout from level config (supports holes/cutouts)
            var layout = _bridge.GridLayout;
            Mesh mesh;
            if (layout != null)
                mesh = BoardMeshBuilder.Build(layout, cellSize, origin, height);
            else
                mesh = BoardMeshBuilder.BuildRectangular(width, height, cellSize, origin);

            _boardFloor = new GameObject("BoardFloor");
            _boardFloor.transform.SetParent(transform, false);
            // Push behind tiles so it doesn't z-fight
            _boardFloor.transform.localPosition = new Vector3(0f, 0f, 0.4f);

            _boardFloor.AddComponent<MeshFilter>().mesh = mesh;
            var floorRenderer = _boardFloor.AddComponent<MeshRenderer>();
            floorRenderer.materials = BoardMeshBuilder.GetBoardMaterials();
            floorRenderer.receiveShadows = true; // 棋盘接收棋子投影
        }

        private void SetupEnvironment()
        {
            // Ambient Light: bright warm, casual game style
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.75f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.59f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.42f, 0.38f);

            // Reflection source: solid warm-white cubemap for clean Fresnel highlights.
            // No real skybox needed — camera still uses solid color background.
            ReflectionCubemapManager.Setup();

            // Background: garden green (confirmed from mockup)
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(120f / 255f, 160f / 255f, 90f / 255f);
            }
        }

        public void ApplyRenderTuning(RenderTuningSettings settings)
        {
            if (settings == null) return;

            if (_lightingController != null)
                _lightingController.SetBaseIntensities(settings.KeyIntensity, settings.KeyShadowStrength, settings.FillIntensity);

            // Reflections (Fresnel)
            ReflectionCubemapManager.UpdateColors(settings.ReflectionSky, settings.ReflectionSide, settings.ReflectionGround);
            ReflectionCubemapManager.SetIntensity(settings.ReflectionIntensity);
        }

        // Board vignette (subtle inner shadow) caches
        private Mesh _instancedVignetteMesh;
        private static Material _vignetteMaterial;
        private static Texture2D _vignetteTexture;

        private void RebuildBoardVignette()
        {
            if (_boardVignette != null)
            {
                Destroy(_boardVignette);
                _boardVignette = null;
            }

            // Destroy previous mesh if any
            if (_instancedVignetteMesh != null)
            {
                Destroy(_instancedVignetteMesh);
                _instancedVignetteMesh = null;
            }

            _boardVignette = new GameObject("BoardVignette");
            if (_boardFloor != null)
            {
                _boardVignette.transform.SetParent(_boardFloor.transform, false);
                // slightly above floor (local z = -0.005 => world 0.095 if floor is 0.1)
                _boardVignette.transform.localPosition = new Vector3(0f, 0f, -0.005f);
            }
            else
            {
                _boardVignette.transform.SetParent(transform, false);
                _boardVignette.transform.localPosition = new Vector3(0f, 0f, 0.095f);
            }

            var mf = _boardVignette.AddComponent<MeshFilter>();

            // Build mesh matching the grid layout (clipping empty corners)
            var layout = _bridge.GridLayout;
            if (layout == null)
            {
                // Fallback to full rectangle if no layout provided
                layout = new bool[_bridge.Height, _bridge.Width];
                for (int r = 0; r < _bridge.Height; r++)
                    for (int c = 0; c < _bridge.Width; c++)
                        layout[r, c] = true;
            }

            _instancedVignetteMesh = BoardMeshBuilder.BuildFlatMesh(layout, _bridge.CellSize, _bridge.BoardOrigin, _bridge.Height);
            _instancedVignetteMesh.name = "BoardVignetteMesh";
            mf.sharedMesh = _instancedVignetteMesh;

            var mr = _boardVignette.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetOrCreateVignetteMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static Material GetOrCreateVignetteMaterial()
        {
            if (_vignetteMaterial != null) return _vignetteMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Transparent");

            _vignetteMaterial = new Material(shader) { name = "BoardVignette" };

            // Transparent surface
            if (_vignetteMaterial.HasProperty("_Surface"))
                _vignetteMaterial.SetFloat("_Surface", 1f);
            if (_vignetteMaterial.HasProperty("_Blend"))
                _vignetteMaterial.SetFloat("_Blend", 0f);
            if (_vignetteMaterial.HasProperty("_SrcBlend"))
                _vignetteMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (_vignetteMaterial.HasProperty("_DstBlend"))
                _vignetteMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (_vignetteMaterial.HasProperty("_ZWrite"))
                _vignetteMaterial.SetFloat("_ZWrite", 0f);

            _vignetteMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _vignetteMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            // Color: white * black texture => black; alpha controls strength.
            var c = new Color(1f, 1f, 1f, 0.18f);
            if (_vignetteMaterial.HasProperty("_BaseColor"))
                _vignetteMaterial.SetColor("_BaseColor", c);
            else if (_vignetteMaterial.HasProperty("_Color"))
                _vignetteMaterial.SetColor("_Color", c);

            _vignetteMaterial.mainTexture = GetOrCreateVignetteTexture();
            return _vignetteMaterial;
        }

        private static Texture2D GetOrCreateVignetteTexture()
        {
            if (_vignetteTexture != null) return _vignetteTexture;

            const int size = 256;
            _vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BoardVignetteTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            // Square-ish vignette: stronger near edges, subtle in the center.
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                float vy = Mathf.Abs(v - 0.5f) * 2f; // 0..1

                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float vx = Mathf.Abs(u - 0.5f) * 2f; // 0..1

                    float d = Mathf.Max(vx, vy); // square distance to edge
                    float a = Mathf.InverseLerp(0.55f, 1.0f, d);
                    a = Mathf.Clamp01(a);
                    a *= a; // softer center

                    _vignetteTexture.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }

            _vignetteTexture.Apply();
            return _vignetteTexture;
        }

        public void Render(VisualState state, float dt)
        {
            if (state == null) return;

            var cellSize = _bridge.CellSize;
            var origin = _bridge.BoardOrigin;
            var height = _bridge.Height;

            // Cache sun direction once per frame for all blob shadows
            Tile3DView.UpdateSunDirection();

            _activeTileIds.Clear();
            _tilesToRemove.Clear();

            // Update existing tiles and create new ones
            foreach (var kvp in state.Tiles)
            {
                var tileId = kvp.Key;
                var visual = kvp.Value;

                if (!visual.IsVisible) continue;

                // Skip tiles that are flying to objectives (ObjectiveDisplayController owns them)
                if (_objectiveDisplay != null && _objectiveDisplay.HiddenTileIds.Contains(tileId))
                {
                    _activeTileIds.Add(tileId); // prevent removal
                    continue;
                }

                _activeTileIds.Add(tileId);

                if (!_activeTiles.TryGetValue(tileId, out var tileView))
                {
                    tileView = _tilePool.Rent();
                    tileView.Setup(tileId, visual.TileType);
                    _activeTiles[tileId] = tileView;
                }

                tileView.UpdateFromVisual(visual, cellSize, origin, height, dt);
            }

            // Remove tiles that are no longer active
            foreach (var kvp in _activeTiles)
            {
                if (!_activeTileIds.Contains(kvp.Key))
                {
                    _tilesToRemove.Add(kvp.Key);
                }
            }

            foreach (var tileId in _tilesToRemove)
            {
                if (_activeTiles.TryGetValue(tileId, out var tileView))
                {
                    // Let ObjectiveDisplayController intercept (bomb merge scenario)
                    if (_objectiveDisplay != null && _objectiveDisplay.TryInterceptRemoval(tileId, tileView))
                    {
                        _activeTiles.Remove(tileId);
                        // ObjectiveDisplayController now owns this tileView
                    }
                    else
                    {
                        _tilePool.Return(tileView);
                        _activeTiles.Remove(tileId);
                    }
                }
            }

            // Update selection highlight
            UpdateSelectionHighlight();

            // Portal visual effects for tiles falling through holes
            UpdatePortalEffects(cellSize, origin, height);

            // Render projectiles
            RenderProjectiles(state, cellSize, origin, height);
        }

        private static readonly int LightColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int LightColorPropFallback = Shader.PropertyToID("_Color");

        private void UpdateSelectionHighlight()
        {
            var selectedPos = _bridge.CurrentState.SelectedPosition;
            int selectedTileId = selectedPos != Position.Invalid
                ? _bridge.GetTileIdAt(selectedPos)
                : -1;

            var selectionLight = _lightingController != null ? _lightingController.SelectionLight : null;

            // Update light position every frame (tile may be falling)
            if (selectedTileId == _highlightedTileId)
            {
                if (selectedTileId >= 0 && selectionLight != null
                    && _activeTiles.TryGetValue(selectedTileId, out var current))
                {
                    var pos = current.transform.position;
                    selectionLight.transform.position = new Vector3(pos.x, pos.y, pos.z - 1f);
                }
                return;
            }

            if (_highlightedTileId >= 0 && _activeTiles.TryGetValue(_highlightedTileId, out var prev))
                prev.SetHighlighted(false);

            if (selectedTileId >= 0 && _activeTiles.TryGetValue(selectedTileId, out var next))
            {
                next.SetHighlighted(true);

                // Move selection light to tile and match its color
                if (selectionLight != null)
                {
                    var tilePos = next.transform.position;
                    selectionLight.transform.position = new Vector3(tilePos.x, tilePos.y, tilePos.z - 1f);
                    selectionLight.color = Color.white;
                    selectionLight.enabled = true;
                }
            }
            else if (selectionLight != null)
            {
                selectionLight.enabled = false;
            }

            _highlightedTileId = selectedTileId;
        }

        private void RenderProjectiles(VisualState state, float cellSize, Vector2 origin, int height)
        {
            _activeProjectileIds.Clear();
            _projectilesToRemove.Clear();

            foreach (var kvp in state.Projectiles)
            {
                var projectileId = kvp.Key;
                var visual = kvp.Value;

                if (!visual.IsVisible) continue;

                _activeProjectileIds.Add(projectileId);

                if (!_activeProjectiles.TryGetValue(projectileId, out var projView))
                {
                    projView = _projectilePool.Rent();
                    projView.Setup(projectileId, visual.Type, visual.ColorIndex);
                    _activeProjectiles[projectileId] = projView;
                }

                projView.UpdateFromVisual(visual, cellSize, origin, height);
            }

            foreach (var kvp in _activeProjectiles)
            {
                if (!_activeProjectileIds.Contains(kvp.Key))
                {
                    _projectilesToRemove.Add(kvp.Key);
                }
            }

            foreach (var projectileId in _projectilesToRemove)
            {
                if (_activeProjectiles.TryGetValue(projectileId, out var projView))
                {
                    _projectilePool.Return(projView);
                    _activeProjectiles.Remove(projectileId);
                }
            }
        }

        /// <summary>
        /// Try get tile view by ID.
        /// </summary>
        public bool TryGetTileView(int tileId, out Tile3DView view)
        {
            return _activeTiles.TryGetValue(tileId, out view);
        }

        /// <summary>
        /// Get the lighting controller for hint light management.
        /// </summary>
        public BoardLightingController GetLightingController() => _lightingController;

        /// <summary>
        /// Remove a tile from active tracking and return its view.
        /// Used by ObjectiveDisplayController for direct-fly (3-connect) scenario.
        /// </summary>
        internal Tile3DView StealTile(int tileId)
        {
            if (_activeTiles.TryGetValue(tileId, out var tileView))
            {
                _activeTiles.Remove(tileId);
                return tileView;
            }
            return null;
        }

        public void Clear()
        {
            _highlightedTileId = -1;
            if (_lightingController != null && _lightingController.SelectionLight != null)
                _lightingController.SelectionLight.enabled = false;

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

        private static Tile3DView CreateTile3DView(Transform parent)
        {
            var go = new GameObject("Tile3D");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            var tileView = go.AddComponent<Tile3DView>();
            return tileView;
        }

        private static (int initial, int max) GetPoolSize(string poolName, int defaultInitial, int defaultMax)
        {
            try
            {
                var config = UnityConfigProvider.Instance.GetGameConfig();
                if (config.PoolSizes != null && config.PoolSizes.TryGetValue(poolName, out var poolConfig))
                {
                    return (poolConfig.Initial, poolConfig.Max);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Board3DView] Failed to load pool config for '{poolName}': {ex.Message}");
            }
            return (defaultInitial, defaultMax);
        }

        private static Projectile3DView CreateProjectile3DView(Transform parent)
        {
            var go = new GameObject("Projectile3D");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            var projView = go.AddComponent<Projectile3DView>();
            return projView;
        }

        /// <summary>
        /// Scan each column for contiguous hole zones and precompute world-space clip boundaries.
        /// </summary>
        private void BuildHoleZones()
        {
            _columnHoleZones.Clear();

            var state = _bridge.CurrentState;
            var cellSize = _bridge.CellSize;
            var origin = _bridge.BoardOrigin;
            var height = _bridge.Height;

            for (int x = 0; x < state.Width; x++)
            {
                // Find first hole in column
                for (int y = 0; y < state.Height; y++)
                {
                    if (state.IsHole(x, y))
                    {
                        int exitY = GravityTargetResolver.FindHoleZoneExit(in state, x, y);

                        // World Y of cell center: origin.y + (height-1-gridY) * cellSize + cellSize/2
                        float entryCenter = origin.y + (height - 1 - y) * cellSize + cellSize * 0.5f;
                        float exitCenter  = origin.y + (height - 1 - exitY) * cellSize + cellSize * 0.5f;

                        _columnHoleZones[x] = new HoleZone
                        {
                            EntryY = y,
                            ExitY = exitY,
                            HoleTopWorldY = entryCenter + cellSize * 0.5f,  // top edge of first hole cell
                            HoleBotWorldY = exitCenter  - cellSize * 0.5f,  // bottom edge of last hole cell
                            MidGridY = (y + exitY + 1) * 0.5f
                        };
                        break; // one hole zone per column for now
                    }
                }
            }
        }

        /// <summary>
        /// Apply portal visual effects to tiles falling through hole zones.
        /// Uses shader Y-axis clipping — tile shape is preserved (no scale distortion).
        /// The shader discards pixels outside [ClipYMin, ClipYMax] in world space.
        /// </summary>
        private void UpdatePortalEffects(float cellSize, Vector2 origin, int height)
        {
            if (_columnHoleZones.Count == 0) return;

            foreach (var kvp in _activeTiles)
            {
                var tileView = kvp.Value;
                if (!tileView.gameObject.activeSelf) continue;

                var worldPos = tileView.transform.position;

                // Tiles elevated above the board (z < 0) are performing special animations
                // (e.g. color bomb spin) — skip portal clipping so they remain fully visible
                if (worldPos.z < -0.1f)
                {
                    tileView.ResetClipBounds();
                    continue;
                }

                var gridPos = CoordinateConverter.WorldToGridFloat(worldPos, cellSize, origin, height);
                int col = Mathf.RoundToInt(gridPos.X);

                if (!_columnHoleZones.TryGetValue(col, out var zone))
                {
                    tileView.ResetClipBounds();
                    continue;
                }

                float tileGridY = gridPos.Y;

                // Only apply clip when tile is near the hole zone (±1.5 cells)
                if (tileGridY < zone.EntryY - 1.5f || tileGridY > zone.ExitY + 1.5f)
                {
                    tileView.ResetClipBounds();
                    continue;
                }

                // Upper side: show only above hole top edge
                // Lower side: show only below hole bottom edge
                if (tileGridY < zone.MidGridY)
                    tileView.SetClipBounds(zone.HoleTopWorldY, 9999f);
                else
                    tileView.SetClipBounds(-9999f, zone.HoleBotWorldY);
            }
        }

        private void OnDestroy()
        {
            Clear();
            _tilePool?.Clear();
            _projectilePool?.Clear();

            // Lighting controller cleans up its own lights via its own OnDestroy
            if (_lightingController != null)
            {
                Destroy(_lightingController.gameObject);
                _lightingController = null;
            }
            if (_boardFloor != null)
            {
                Destroy(_boardFloor);
                _boardFloor = null;
            }
            if (_boardVignette != null)
            {
                Destroy(_boardVignette);
                _boardVignette = null;
            }
            if (_instancedVignetteMesh != null)
            {
                Destroy(_instancedVignetteMesh);
                _instancedVignetteMesh = null;
            }
        }
    }
}
