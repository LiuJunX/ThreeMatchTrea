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
        private const float IconTiltX = 25f;

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
            public ElementType ElementType;
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
            public bool HasMergeTarget;
            public float CurrentRotationY;
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

                var elementType = p.TargetLayer == ObjectiveTargetLayer.Tile
                    ? (ElementType)p.ElementType
                    : ElementType.None;
                var obstacleType = p.TargetLayer == ObjectiveTargetLayer.Obstacle
                    ? (ObstacleType)p.ElementType
                    : ObstacleType.None;
                var groundType = p.TargetLayer == ObjectiveTargetLayer.Ground
                    ? (GroundType)p.ElementType
                    : GroundType.None;

                float x = startX + slot * IconSpacing;
                var icon = CreateIcon(i, elementType, obstacleType, groundType, p.CurrentCount, p.TargetCount, new Vector3(x, baseY, 0f));
                _icons.Add(icon);
                slot++;
            }
        }

        private ObjectiveIcon CreateIcon(int index, ElementType elementType, ObstacleType obstacleType, GroundType groundType, int current, int target, Vector3 position)
        {
            var go = new GameObject($"Objective_{index}");
            go.transform.SetParent(_iconContainer, false);
            go.transform.position = position;
            go.transform.localEulerAngles = new Vector3(IconTiltX, 0f, 0f);
            go.transform.localScale = Vector3.one * (IconScale * _bridge.CellSize);

            // Mesh + material
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            if (obstacleType != ObstacleType.None)
            {
                mf.sharedMesh = MeshFactory.GetObstacleMesh(obstacleType);
                var mats = MeshFactory.GetObstacleMaterials(obstacleType);
                if (mats != null && mats.Length > 0)
                    mr.sharedMaterials = mats;
                else
                    mr.sharedMaterial = MeshFactory.GetFallbackMaterial();

                // Tint to stage-1 color (the "about to break" look)
                var propBlock = new MaterialPropertyBlock();
                var stage1Color = new Color(0.55f, 0.35f, 0.15f);
                propBlock.SetColor("_BaseColor", stage1Color);
                propBlock.SetColor("_Color", stage1Color);
                mr.SetPropertyBlock(propBlock);
            }
            else if (groundType != GroundType.None)
            {
                mf.sharedMesh = MeshFactory.GetGroundMesh(groundType);
                var mats = MeshFactory.GetGroundMaterials(groundType);
                if (mats != null && mats.Length > 0)
                    mr.sharedMaterials = mats;
                else
                    mr.sharedMaterial = MeshFactory.GetFallbackMaterial();
            }
            else if (elementType != ElementType.None)
            {
                mf.sharedMesh = MeshFactory.GetTileMesh(elementType);
                var isColorType = elementType >= ElementType.Item1 && elementType <= ElementType.Item6;
                mr.sharedMaterial = isColorType
                    ? MeshFactory.GetTileMaterial(elementType)
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
                ElementType = elementType,
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
            if (iconIndex < 0 || iconIndex >= _icons.Count) return;

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
                    SuppressedKey = gridKey,
                    HasMergeTarget = false,
                    CurrentRotationY = 0f
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
                SuppressedKey = pending.SuppressedKey,
                HasMergeTarget = true,
                CurrentRotationY = 0f
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
                    // Counter-scale text so it stays stable during bounce
                    float invScale = 1f / IconScale;
                    icon.CountText.transform.localScale = new Vector3(
                        invScale / (1f + squash),
                        invScale / (1f - squash),
                        invScale / (1f + squash));
                }
                else
                {
                    icon.BounceTimer = -1f;
                    icon.Root.localScale = Vector3.one * (IconScale * _bridge.CellSize);
                    icon.CountText.transform.localScale = Vector3.one * (1f / IconScale);
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

                if (fly.HasMergeTarget)
                    UpdateFlyModeB(ref fly, dt);
                else
                    UpdateFlyModeA(ref fly, dt);

                fly.View.RefreshShadow(_bridge.CellSize);
                _activeFlies[i] = fly;

                // Check completion
                bool completed = fly.HasMergeTarget
                    ? fly.Elapsed >= fly.Duration
                    : fly.Elapsed >= _flyConfig.JumpUpDuration + _flyConfig.HoverDuration + fly.Duration;

                if (completed)
                {
                    OnFlyComplete(fly);
                    _activeFlies.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Mode A: 3-connect — jump up + hover spin + straight fly.
        /// </summary>
        private void UpdateFlyModeA(ref ActiveFly fly, float dt)
        {
            float cellSize = _bridge.CellSize;
            float jumpDur = _flyConfig.JumpUpDuration;
            float hoverDur = _flyConfig.HoverDuration;
            float elapsed = fly.Elapsed;

            // Uniform rotation across jump+hover: 180° total
            float totalSpinDur = jumpDur + hoverDur;
            fly.CurrentRotationY = 180f * Mathf.Clamp01(elapsed / totalSpinDur);

            if (elapsed < jumpDur)
            {
                // Phase 1: Jump up
                float t = elapsed / jumpDur;
                float easedT = _flyConfig.EasingFunction(t);
                float y = fly.From.y + _flyConfig.JumpHeight * cellSize * easedT;
                float scale = Mathf.Lerp(_flyConfig.StartScale, _flyConfig.PopUpScale, easedT);

                fly.View.transform.position = new Vector3(fly.From.x, y, _flyConfig.PopUpZOffset);
                fly.View.transform.localScale = Vector3.one * (scale * cellSize);
                fly.View.transform.localEulerAngles = new Vector3(0f, fly.CurrentRotationY, 0f);
            }
            else if (elapsed < jumpDur + hoverDur)
            {
                // Phase 2: Hover at apex
                float y = fly.From.y + _flyConfig.JumpHeight * cellSize;

                fly.View.transform.position = new Vector3(fly.From.x, y, _flyConfig.PopUpZOffset);
                fly.View.transform.localScale = Vector3.one * (_flyConfig.PopUpScale * cellSize);
                fly.View.transform.localEulerAngles = new Vector3(0f, fly.CurrentRotationY, 0f);
            }
            else
            {
                // Phase 3: Straight fly to target
                float flyElapsed = elapsed - jumpDur - hoverDur;
                float t = Mathf.Clamp01(flyElapsed / fly.Duration);
                float easedT = _flyConfig.EasingFunction(t);

                var flyFrom = new Vector3(fly.From.x,
                    fly.From.y + _flyConfig.JumpHeight * cellSize,
                    _flyConfig.PopUpZOffset);
                var pos = Vector3.Lerp(flyFrom, fly.To, easedT);
                float scale = Mathf.Lerp(_flyConfig.PopUpScale, _flyConfig.EndScale, easedT);
                float tiltX = Mathf.Lerp(0f, IconTiltX, easedT);

                fly.View.transform.position = pos;
                fly.View.transform.localScale = Vector3.one * (scale * cellSize);
                fly.View.transform.localRotation = Quaternion.Euler(tiltX, 0f, 0f) * Quaternion.Euler(0f, fly.CurrentRotationY, 0f);
            }
        }

        /// <summary>
        /// Mode B: Bomb merge — direct fly with synchronized rotation.
        /// </summary>
        private void UpdateFlyModeB(ref ActiveFly fly, float dt)
        {
            float cellSize = _bridge.CellSize;
            float t = Mathf.Clamp01(fly.Elapsed / fly.Duration);
            float easedT = _flyConfig.EasingFunction(t);

            var pos = Vector3.Lerp(fly.From, fly.To, easedT);
            float scale = Mathf.Lerp(_flyConfig.StartScale, _flyConfig.EndScale, easedT);

            // Rotation synced to duration: exactly one full turn
            fly.CurrentRotationY += (360f / fly.Duration) * dt;
            float tiltX = Mathf.Lerp(0f, IconTiltX, easedT);
            fly.View.transform.position = pos;
            fly.View.transform.localScale = Vector3.one * (scale * cellSize);
            fly.View.transform.localRotation = Quaternion.Euler(tiltX, 0f, 0f) * Quaternion.Euler(0f, fly.CurrentRotationY, 0f);
        }

        private void OnFlyComplete(ActiveFly fly)
        {
            // Snap to final state: match icon rotation and scale for perfect overlap
            if (fly.View != null)
            {
                fly.View.transform.position = fly.To;
                fly.View.transform.localEulerAngles = new Vector3(IconTiltX, 0f, 0f);
                fly.View.transform.localScale = Vector3.one * (_flyConfig.EndScale * _bridge.CellSize);
            }

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

        private static readonly int s_colorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int s_colorPropFallback = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock s_alphaPropBlock;

        private static void ApplyAlpha(MeshRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            s_alphaPropBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(s_alphaPropBlock);
            var mat = renderer.sharedMaterial;
            var color = mat.HasProperty(s_colorProp)
                ? mat.GetColor(s_colorProp)
                : mat.GetColor(s_colorPropFallback);
            color.a = alpha;
            s_alphaPropBlock.SetColor(s_colorProp, color);
            s_alphaPropBlock.SetColor(s_colorPropFallback, color);
            renderer.SetPropertyBlock(s_alphaPropBlock);
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
