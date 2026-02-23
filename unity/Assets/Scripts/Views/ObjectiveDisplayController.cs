using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using Match3.Unity.UI;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// World-space objective display and fly-to-objective animation controller.
    /// Creates 3D gem icons above the board and manages tile fly animations.
    /// </summary>
    public sealed class ObjectiveDisplayController : MonoBehaviour
    {
        /// <summary>
        /// Total height occupied by objective display (gap + icon row).
        /// CameraSetup uses this to frame the board + objectives.
        /// </summary>
        public const float TotalHeight = 2.8f;

        private const float GapAboveBoard = 3.2f;
        private const float IconScale = 1.2f;
        private const float IconSpacing = 1.5f;
        private const float BounceTime = 0.2f;
        private const float CompletedAlpha = 0.5f;

        private Match3Bridge _bridge;
        private Board3DView _boardView;
        private EffectManager _effectManager;
        private FlyAnimationConfig _flyConfig;

        private Transform _iconContainer;
        private readonly List<ObjectiveIcon> _icons = new();
        private readonly List<ActiveFly> _activeFlies = new();
        private readonly Dictionary<int, FlyPending> _pendingMergeFlies = new();

        /// <summary>
        /// Tile IDs that Board3DView should skip rendering (they're being flown).
        /// </summary>
        public readonly HashSet<int> HiddenTileIds = new();

        /// <summary>
        /// Grid positions where EffectManager should suppress match_pop/pop effects.
        /// </summary>
        public readonly HashSet<long> SuppressedEffectPositions = new();

        private struct ObjectiveIcon
        {
            public Transform Root;
            public MeshRenderer Renderer;
            public TextMesh CountText;
            public TileType TileType;
            public int CoreCount;   // latest count from Core (authoritative)
            public int Target;
            public int InFlightCount; // number of flies in the air for this icon
            public float BounceTimer;

            /// <summary>Displayed count = CoreCount - InFlightCount.</summary>
            public readonly int DisplayCount => CoreCount - InFlightCount;
        }

        private struct ActiveFly
        {
            public Tile3DView View;
            public Vector3 From;
            public Vector3 To;
            public int ObjectiveIndex;
            public float Delay;
            public float Elapsed;
            public float Duration;
            public int TileId;
            public long SuppressedKey;
        }

        private struct FlyPending
        {
            public int ObjectiveIndex;
            public float FlyDelay;
            public long SuppressedKey;
        }

        public void Initialize(Match3Bridge bridge, Board3DView boardView, EffectManager effectManager)
        {
            _bridge = bridge;
            _boardView = boardView;
            _effectManager = effectManager;
            _flyConfig = new FlyAnimationConfig();

            // Wire effect suppression
            _effectManager.SuppressedPositions = SuppressedEffectPositions;

            // Create icon container (only once)
            if (_iconContainer == null)
            {
                _iconContainer = new GameObject("ObjectiveIcons").transform;
                _iconContainer.SetParent(transform, false);
            }

            // Subscribe to events (unsubscribe first to prevent double-subscribe on restart)
            _bridge.OnObjectiveCollected -= OnObjectiveCollected;
            _bridge.OnObjectivesUpdated -= OnObjectivesUpdated;
            _bridge.OnObjectiveCollected += OnObjectiveCollected;
            _bridge.OnObjectivesUpdated += OnObjectivesUpdated;

            // Build initial icons from current game state
            RebuildIcons();
        }

        private void RebuildIcons()
        {
            // Clear existing
            foreach (var icon in _icons)
            {
                if (icon.Root != null)
                    Destroy(icon.Root.gameObject);
            }
            _icons.Clear();

            var state = _bridge.CurrentState;
            if (state.ObjectiveProgress == null) return;

            // Count active objectives
            int activeCount = 0;
            for (int i = 0; i < state.ObjectiveProgress.Length; i++)
            {
                if (state.ObjectiveProgress[i].IsActive)
                    activeCount++;
            }
            if (activeCount == 0) return;

            // Position: centered above board
            var origin = _bridge.BoardOrigin;
            var cellSize = _bridge.CellSize;
            var boardWidth = _bridge.Width * cellSize;
            var boardHeight = _bridge.Height * cellSize;
            float centerX = origin.x + boardWidth * 0.5f;
            float baseY = origin.y + boardHeight + GapAboveBoard;

            // Calculate horizontal layout
            float totalWidth = (activeCount - 1) * IconSpacing;
            float startX = centerX - totalWidth * 0.5f;

            int slot = 0;
            for (int i = 0; i < state.ObjectiveProgress.Length; i++)
            {
                var p = state.ObjectiveProgress[i];
                if (!p.IsActive) continue;

                var tileType = p.TargetLayer == ObjectiveTargetLayer.Tile
                    ? (TileType)p.ElementType
                    : TileType.None;

                float x = startX + slot * IconSpacing;
                var icon = CreateIcon(i, tileType, p.CurrentCount, p.TargetCount, new Vector3(x, baseY, 0f));
                _icons.Add(icon);
                slot++;
            }
        }

        private ObjectiveIcon CreateIcon(int index, TileType tileType, int current, int target, Vector3 position)
        {
            var go = new GameObject($"Objective_{index}");
            go.transform.SetParent(_iconContainer, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (IconScale * _bridge.CellSize);

            // Gem mesh
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            if (tileType != TileType.None)
            {
                mf.sharedMesh = MeshFactory.GetTileMesh(tileType);
                var isColorType = (tileType & (TileType.Red | TileType.Green | TileType.Blue |
                                               TileType.Yellow | TileType.Purple | TileType.Orange)) != 0;
                mr.sharedMaterial = isColorType
                    ? MeshFactory.GetTileMaterial(tileType)
                    : MeshFactory.GetFallbackMaterial();
            }
            else
            {
                mf.sharedMesh = MeshFactory.GetFallbackMesh();
                mr.sharedMaterial = MeshFactory.GetFallbackMaterial();
            }

            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Count text
            var textGo = new GameObject("Count");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, -0.55f, -0.1f);
            textGo.transform.localScale = Vector3.one * (1f / IconScale); // counteract parent scale

            var textMesh = textGo.AddComponent<TextMesh>();
            textMesh.text = $"{current}/{target}";
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.12f;
            textMesh.anchor = TextAnchor.UpperCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = current >= target ? new Color(0.5f, 1f, 0.5f) : Color.white;

            // Dim completed
            if (current >= target)
                ApplyAlpha(mr, CompletedAlpha);

            return new ObjectiveIcon
            {
                Root = go.transform,
                Renderer = mr,
                CountText = textMesh,
                TileType = tileType,
                CoreCount = current,
                Target = target,
                InFlightCount = 0,
                BounceTimer = -1f
            };
        }

        private void OnObjectivesUpdated(ObjectiveProgress[] objectives)
        {
            for (int i = 0; i < _icons.Count && i < objectives.Length; i++)
            {
                var icon = _icons[i];
                var obj = objectives[i];

                icon.CoreCount = obj.Current;
                icon.Target = obj.Target;

                // Only refresh text when no flies in the air (otherwise OnFlyComplete handles it)
                if (icon.InFlightCount <= 0)
                {
                    icon.InFlightCount = 0;
                    RefreshIconText(ref icon);
                }

                _icons[i] = icon;
            }
        }

        private void OnObjectiveCollected(Match3Bridge.FlyCollectionRequest request)
        {
            // Find icon index for this objective
            int iconIndex = FindIconIndex(request.ObjectiveIndex);
            if (iconIndex < 0) return;

            // Track in-flight count (display updates on arrival, not emission)
            var icon = _icons[iconIndex];
            icon.InFlightCount++;
            _icons[iconIndex] = icon;

            var gridKey = PackGridKey(request.SourceGridPosition.X, request.SourceGridPosition.Y);
            SuppressedEffectPositions.Add(gridKey);

            if (request.MergeTarget == null)
            {
                // 3-connect: steal tile immediately and start flying
                HiddenTileIds.Add(request.TileId);
                var tileView = _boardView.StealTile(request.TileId);
                if (tileView == null) return;

                // Reparent to our container
                tileView.transform.SetParent(_iconContainer, true);

                var from = tileView.transform.position;
                var to = _icons[iconIndex].Root.position;
                var distance = Vector3.Distance(from, to);

                _activeFlies.Add(new ActiveFly
                {
                    View = tileView,
                    From = from,
                    To = to,
                    ObjectiveIndex = iconIndex,
                    Delay = 0f,
                    Elapsed = 0f,
                    Duration = _flyConfig.GetDuration(distance),
                    TileId = request.TileId,
                    SuppressedKey = gridKey
                });
            }
            else
            {
                // Bomb merge: store pending, Board3DView will call TryInterceptRemoval later
                _pendingMergeFlies[request.TileId] = new FlyPending
                {
                    ObjectiveIndex = iconIndex,
                    FlyDelay = request.FlyDelay,
                    SuppressedKey = gridKey
                };
            }
        }

        /// <summary>
        /// Called by Board3DView when removing a tile. Returns true if intercepted.
        /// </summary>
        public bool TryInterceptRemoval(int tileId, Tile3DView tileView)
        {
            if (!_pendingMergeFlies.TryGetValue(tileId, out var pending))
                return false;

            _pendingMergeFlies.Remove(tileId);

            // Reparent
            tileView.transform.SetParent(_iconContainer, true);

            int iconIndex = pending.ObjectiveIndex;
            if (iconIndex < 0 || iconIndex >= _icons.Count)
            {
                // Invalid index, return tile to pool
                _boardView.TilePool.Return(tileView);
                return true;
            }

            var from = tileView.transform.position;
            var to = _icons[iconIndex].Root.position;
            var distance = Vector3.Distance(from, to);

            tileView.gameObject.SetActive(true);

            _activeFlies.Add(new ActiveFly
            {
                View = tileView,
                From = from,
                To = to,
                ObjectiveIndex = iconIndex,
                Delay = pending.FlyDelay,
                Elapsed = 0f,
                Duration = _flyConfig.GetDuration(distance),
                TileId = tileId,
                SuppressedKey = pending.SuppressedKey
            });

            return true;
        }

        /// <summary>
        /// Advance all active fly animations. Called from GameController.Update().
        /// </summary>
        public void UpdateFlies(float deltaTime)
        {
            // Update icon bounces
            for (int i = 0; i < _icons.Count; i++)
            {
                var icon = _icons[i];
                if (icon.BounceTimer < 0f) continue;

                icon.BounceTimer += deltaTime;
                if (icon.BounceTimer < BounceTime)
                {
                    float t = icon.BounceTimer / BounceTime;
                    float squash = Mathf.Sin(t * Mathf.PI) * 0.15f;
                    var baseScale = Vector3.one * (IconScale * _bridge.CellSize);
                    icon.Root.localScale = new Vector3(
                        baseScale.x * (1f + squash),
                        baseScale.y * (1f - squash),
                        baseScale.z * (1f + squash));
                }
                else
                {
                    icon.BounceTimer = -1f;
                    icon.Root.localScale = Vector3.one * (IconScale * _bridge.CellSize);
                }
                _icons[i] = icon;
            }

            // Update flies (iterate backwards for safe removal)
            for (int i = _activeFlies.Count - 1; i >= 0; i--)
            {
                var fly = _activeFlies[i];
                float dt = deltaTime;

                // Handle delay
                if (fly.Delay > 0f)
                {
                    fly.Delay -= dt;
                    if (fly.Delay > 0f)
                    {
                        _activeFlies[i] = fly;
                        continue;
                    }
                    // Delay exhausted, proceed with overflow time
                    dt = -fly.Delay;
                    fly.Delay = 0f;
                }

                fly.Elapsed += dt;

                float popDur = _flyConfig.PopUpDuration;

                if (fly.Elapsed < popDur)
                {
                    // Phase 1: Pop-up — tile stays in place, scales up then holds
                    float pt = fly.Elapsed / popDur;
                    // Quick ease-out scale: overshoot then settle
                    float popScale = Mathf.Lerp(1f, _flyConfig.PopUpScale, Mathf.Sin(pt * Mathf.PI * 0.5f));
                    float uniformScale = popScale * _bridge.CellSize;
                    fly.View.transform.position = new Vector3(fly.From.x, fly.From.y, -0.1f);
                    fly.View.transform.localScale = Vector3.one * uniformScale;
                    fly.View.transform.localEulerAngles = Vector3.zero;

                    _activeFlies[i] = fly;
                }
                else
                {
                    // Phase 2: Fly toward objective
                    float flyElapsed = fly.Elapsed - popDur;
                    float t = Mathf.Clamp01(flyElapsed / fly.Duration);
                    float easedT = _flyConfig.EasingFunction(t);

                    // Position: lerp + arc
                    var pos = Vector3.Lerp(fly.From, fly.To, easedT);
                    pos.y += _flyConfig.ArcHeight * Mathf.Sin(Mathf.PI * t);
                    pos.z = Mathf.Lerp(0f, -0.2f, t);

                    fly.View.transform.position = pos;

                    // Scale: interpolate from pop-up size to icon size
                    float scaleFactor = Mathf.Lerp(_flyConfig.StartScale, _flyConfig.EndScale, easedT);
                    float uniformScale = scaleFactor * _bridge.CellSize;
                    fly.View.transform.localScale = Vector3.one * uniformScale;
                    fly.View.transform.localEulerAngles = Vector3.zero;

                    _activeFlies[i] = fly;

                    // Complete?
                    if (t >= 1f)
                    {
                        OnFlyComplete(fly);
                        _activeFlies.RemoveAt(i);
                    }
                }
            }
        }

        private void OnFlyComplete(ActiveFly fly)
        {
            // Decrement in-flight count → displayed count increases by 1
            if (fly.ObjectiveIndex >= 0 && fly.ObjectiveIndex < _icons.Count)
            {
                var icon = _icons[fly.ObjectiveIndex];
                icon.InFlightCount = Mathf.Max(0, icon.InFlightCount - 1);
                icon.BounceTimer = 0f;
                RefreshIconText(ref icon);
                _icons[fly.ObjectiveIndex] = icon;
            }

            // Cleanup
            HiddenTileIds.Remove(fly.TileId);
            SuppressedEffectPositions.Remove(fly.SuppressedKey);

            // Return tile to pool
            if (fly.View != null)
            {
                fly.View.transform.SetParent(_boardView.TilePool != null
                    ? _boardView.transform : transform, false);
                _boardView.TilePool.Return(fly.View);
            }
        }

        /// <summary>
        /// Clear all state (restart).
        /// </summary>
        public void Clear()
        {
            // Return flying tiles (reparent to board before returning to pool)
            foreach (var fly in _activeFlies)
            {
                if (fly.View != null)
                {
                    fly.View.transform.SetParent(_boardView.transform, false);
                    _boardView.TilePool?.Return(fly.View);
                }
            }
            _activeFlies.Clear();
            _pendingMergeFlies.Clear();
            HiddenTileIds.Clear();
            SuppressedEffectPositions.Clear();

            // Destroy icons
            foreach (var icon in _icons)
            {
                if (icon.Root != null)
                    Destroy(icon.Root.gameObject);
            }
            _icons.Clear();
        }

        /// <summary>
        /// Rebuild icons after restart.
        /// </summary>
        public void Reinitialize()
        {
            Clear();
            RebuildIcons();
        }

        private int FindIconIndex(int objectiveIndex)
        {
            // Icons are created in order of active objectives.
            // Map objectiveIndex (GameState slot) to icon list index.
            var state = _bridge.CurrentState;
            if (state.ObjectiveProgress == null) return -1;

            int slot = 0;
            for (int i = 0; i < state.ObjectiveProgress.Length; i++)
            {
                if (!state.ObjectiveProgress[i].IsActive) continue;
                if (i == objectiveIndex) return slot;
                slot++;
            }
            return -1;
        }

        private static void RefreshIconText(ref ObjectiveIcon icon)
        {
            int displayed = icon.DisplayCount;
            icon.CountText.text = $"{displayed}/{icon.Target}";
            bool completed = displayed >= icon.Target;
            icon.CountText.color = completed ? new Color(0.5f, 1f, 0.5f) : Color.white;
            if (completed)
                ApplyAlpha(icon.Renderer, CompletedAlpha);
        }

        private static void ApplyAlpha(MeshRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            var propBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propBlock);
            var mat = renderer.sharedMaterial;
            var colorProp = Shader.PropertyToID("_BaseColor");
            var colorPropFallback = Shader.PropertyToID("_Color");
            var color = mat.HasProperty(colorProp)
                ? mat.GetColor(colorProp)
                : mat.GetColor(colorPropFallback);
            color.a = alpha;
            propBlock.SetColor(colorProp, color);
            propBlock.SetColor(colorPropFallback, color);
            renderer.SetPropertyBlock(propBlock);
        }

        private static long PackGridKey(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }

        private void OnDestroy()
        {
            if (_bridge != null)
            {
                _bridge.OnObjectiveCollected -= OnObjectiveCollected;
                _bridge.OnObjectivesUpdated -= OnObjectivesUpdated;
            }
            Clear();
        }
    }
}
