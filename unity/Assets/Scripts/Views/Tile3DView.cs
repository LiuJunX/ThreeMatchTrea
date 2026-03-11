using Match3.Core.Models.Enums;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Controllers;
using Match3.Unity.Pools;
using UnityEngine;
using UnityEngine.Rendering;

namespace Match3.Unity.Views
{
    /// <summary>
    /// 3D visual representation of a single tile.
    /// Uses MeshFilter + MeshRenderer instead of SpriteRenderer.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class Tile3DView : MonoBehaviour, IPoolable
    {
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propBlock;
        private MeshRenderer _shadowRenderer;
        private MaterialPropertyBlock _shadowPropBlock;
        private Transform _shadowTransform;

        private OutlineEffect _outline;
        private Material[] _lastMaterials;

        // Cached shadow state so blob tweaks can refresh instantly
        private bool _hasLastShadowState;
        private Vector3 _lastShadowWorldPos;
        private float _lastShadowCellSize;
        private Vector3 _lastShadowTileScale;
        private float _lastShadowTileZ;

        public int TileId { get; private set; }

        private Vector3 _baseScale = Vector3.one;
        private bool _isHighlighted;
        private bool _isHinted;

        // UFO flight animation state (View-local, driven by TileVisual.UfoFlightProgress)
        private bool _ufoFlying;
        private float _ufoSpinAngle;
        private float _ufoTiltX, _ufoTiltZ;
        private float _ufoTiltVelX, _ufoTiltVelZ;
        private Vector3 _ufoPrevWorldPos;
        private Vector3 _ufoSmoothVel; // EMA-smoothed velocity to avoid frame jitter

        // Retarget blend: smooths arc/scale transition when progress jumps on retarget
        private float _ufoArcCurrent;
        private float _ufoArcSnapshot;
        private float _retargetBlend;
        // takeoffBlend: only increases (never snaps back to upright on retarget)
        private float _ufoTakeoffBlend;
        private HintAnimationType _hintType;
        private Vector2 _hintNudgeDir;
        private float _hintTime;
        private bool _wasAnimated;
        private float _bounceTime = -1f;
        private float _highlightTime;

        private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropFallback = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");
        private static readonly int ShadowColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ShadowColorPropFallback = Shader.PropertyToID("_Color");
        private static readonly int ClipYMinProp = Shader.PropertyToID("_ClipYMin");
        private static readonly int ClipYMaxProp = Shader.PropertyToID("_ClipYMax");

        private const float BounceEndTime = 0.15f;

        // Per-tile X tilt (replaces TileContainer3D rotation for clean coordinates)
        private const float BaseTiltX = -10f;
        private static readonly Vector3 BaseTiltEuler = new Vector3(BaseTiltX, 0f, 0f);

        // Scale multiplier: makes tiles fill more of the cell
        private const float TileScaleMultiplier = 1.05f;

        // Blob shadow constants
        private const float BlobShadowZ = 0.08f;

        // Cached sun direction (updated once per frame via UpdateSunDirection)
        private static Vector2 s_sunDir2D = new Vector2(-0.35f, -0.70f).normalized;
        private static int s_sunDirFrame = -1;

        // Runtime-tweakable blob shadow params (defaults tuned for ceramic look).
        private static float s_blobShadowSize = 0.54f;
        private static float s_blobShadowBaseAlpha = 0.11f;
        private static float s_blobShadowOffset = 0.025f;
        private static float s_blobShadowLiftAlphaReduce = 0.55f;
        private static float s_blobShadowLiftSizeIncrease = 0.18f;

        public static void ApplyRenderTuning(RenderTuningSettings settings)
        {
            if (settings == null) return;
            s_blobShadowSize = settings.BlobSize;
            s_blobShadowBaseAlpha = settings.BlobBaseAlpha;
            s_blobShadowOffset = settings.BlobOffset;
            s_blobShadowLiftAlphaReduce = settings.BlobLiftAlphaReduce;
            s_blobShadowLiftSizeIncrease = settings.BlobLiftSizeIncrease;

            // Apply immediately to existing tiles (so blob shadow tweaks feel "live").
            RefreshAllBlobShadows();
        }

        private static void RefreshAllBlobShadows()
        {
            var tiles = Object.FindObjectsOfType<Tile3DView>();
            foreach (var t in tiles)
                t.RefreshBlobShadowNow();
        }

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _propBlock = new MaterialPropertyBlock();
            _outline = gameObject.AddComponent<OutlineEffect>();
            CreateBlobShadow();
        }

        private void CreateBlobShadow()
        {
            var shadowGo = new GameObject("BlobShadow");
            _shadowTransform = shadowGo.transform;
            _shadowTransform.SetParent(transform, false);

            shadowGo.AddComponent<MeshFilter>().sharedMesh = MeshFactory.GetBlobShadowMesh();
            _shadowRenderer = shadowGo.AddComponent<MeshRenderer>();
            _shadowRenderer.sharedMaterial = MeshFactory.GetBlobShadowMaterial();
            _shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _shadowRenderer.receiveShadows = false;
            _shadowPropBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Initialize the tile with ID and type.
        /// </summary>
        public void Setup(int id, ElementType type)
        {
            TileId = id;
            ApplyAppearance(type);

            // 棋子在棋盘上投射并接收阴影
            _meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            _meshRenderer.receiveShadows = true;
        }

        private void ApplyAppearance(ElementType type)
        {
            var targetMesh = type.IsBomb()
                ? MeshFactory.GetBombMesh(type)
                : MeshFactory.GetTileMesh(type);
            ViewHelper.SetMesh(_meshFilter, targetMesh);
            ViewHelper.SetMaterials(_meshRenderer, MeshFactory.GetTileMaterialArray(type), ref _lastMaterials);

            if (_outline != null)
                _outline.OutlineColor = MeshFactory.GetOutlineColor(type);
        }

        /// <summary>
        /// Update tile from visual state.
        /// </summary>
        public void UpdateFromVisual(TileVisual visual, float cellSize, Vector2 origin, int height)
        {
            var isAnimated = visual.IsBeingAnimated;

            // Detect landing: was animated, now idle
            if (_wasAnimated && !isAnimated)
            {
                _bounceTime = 0f;
            }
            _wasAnimated = isAnimated;

            // Position (with Y-flip)
            var worldPos = CoordinateConverter.GridToWorld(visual.Position, cellSize, origin, height);

            // UFO flight: delegate all visuals to dedicated method
            if (visual.UfoFlightProgress >= 0f)
            {
                ApplyUfoFlight(visual, worldPos, cellSize);
                return;
            }

            // Reset UFO state when flight ends
            if (_ufoFlying)
            {
                _ufoFlying = false;
                transform.localEulerAngles = BaseTiltEuler;
                if (_shadowTransform != null)
                    _shadowTransform.gameObject.SetActive(true);
            }

            var pos = new Vector3(worldPos.x, worldPos.y, 0f);
            // NOTE: Idle breathing intentionally disabled (keep tiles perfectly still when idle).

            if (_isHighlighted)
            {
                _highlightTime = (_highlightTime + Time.deltaTime) % 628f; // wrap to avoid float precision loss
                // 选中呼吸：相对棋盘的上下（沿棋盘法线 Z 轴轻微浮动）
                var floatZ = Mathf.Sin(_highlightTime * 5f) * 0.04f * cellSize;
                pos.z += floatZ;
            }

            // Scale (uniform 3D, Z tracks min of X/Y for natural shrink)
            var scaleFactor = cellSize * TileScaleMultiplier;
            var zScale = Mathf.Min(visual.Scale.X, visual.Scale.Y);
            _baseScale = new Vector3(
                visual.Scale.X * scaleFactor,
                visual.Scale.Y * scaleFactor,
                zScale * scaleFactor);

            var finalScale = _baseScale;

            // Landing bounce
            if (_bounceTime >= 0f && _bounceTime < BounceEndTime)
            {
                _bounceTime += Time.deltaTime;
                var t = _bounceTime / BounceEndTime;
                var squash = Mathf.Sin(t * Mathf.PI) * 0.1f;
                finalScale.x *= 1f + squash;
                finalScale.z *= 1f + squash;
                finalScale.y *= 1f - squash;
            }
            else if (_bounceTime >= BounceEndTime)
            {
                _bounceTime = -1f;
            }

            // Hint animation (selection overrides hint)
            if (_isHinted && !_isHighlighted)
            {
                _hintTime += Time.deltaTime;
                if (_hintType == HintAnimationType.BombPulse)
                {
                    var pulse = 1f + Mathf.Sin(_hintTime * 2f * Mathf.PI * 2f) * 0.06f;
                    finalScale *= pulse;
                }
                else if (_hintType == HintAnimationType.SwapNudge)
                {
                    var phase = Mathf.Sin(_hintTime * Mathf.PI * 2f);
                    var pulse = 1f + phase * 0.04f;
                    finalScale *= pulse;
                    var nudge = Mathf.Max(phase, 0f) * 0.12f * cellSize;
                    pos += new Vector3(_hintNudgeDir.x * nudge, _hintNudgeDir.y * nudge, 0f);
                    // Tilt toward movement direction during nudge
                    var tiltAngle = Mathf.Max(phase, 0f) * 6f;
                    var tiltX = -_hintNudgeDir.y * tiltAngle; // vertical: X axis
                    var tiltY = _hintNudgeDir.x * tiltAngle;  // horizontal: Y axis
                    transform.localEulerAngles = new Vector3(BaseTiltX + tiltX, tiltY, 0f);
                }

                // Emission pulse for hint
                _meshRenderer.GetPropertyBlock(_propBlock);
                var mat = _meshRenderer.sharedMaterial;
                var baseColor = mat.HasProperty(ColorProp)
                    ? mat.GetColor(ColorProp)
                    : mat.GetColor(ColorPropFallback);
                var emFreq = _hintType == HintAnimationType.SwapNudge ? 1f : 2f;
                var emissionStrength = Mathf.Lerp(0.1f, 0.15f, (Mathf.Sin(_hintTime * emFreq * Mathf.PI * 2f) + 1f) * 0.5f);
                _propBlock.SetColor(EmissionColorProp, baseColor * emissionStrength);
                _meshRenderer.SetPropertyBlock(_propBlock);
            }

            // Apply final position (after all highlight/hint modifications)
            transform.position = pos;

            // Selection pulse (scale + rotation; _highlightTime already updated above)
            if (_isHighlighted)
            {
                var pulse = 1f + Mathf.Sin(_highlightTime * 8f) * 0.08f;
                finalScale *= pulse;

                // 3D mode: Y-axis rotation
                var rot = transform.localEulerAngles;
                rot.y += 30f * Time.deltaTime;
                transform.localEulerAngles = rot;
            }

            // Animation-driven rotation (color bomb spin etc.)
            if (visual.Rotation != 0f)
            {
                pos.z = -0.5f; // Float above other tiles
                transform.position = pos;
                transform.localEulerAngles = new Vector3(BaseTiltX, visual.Rotation, 0f);
            }

            transform.localScale = finalScale;

            // Update blob shadow
            UpdateBlobShadow(worldPos, cellSize, finalScale, pos.z);

            // Alpha via MaterialPropertyBlock
            if (visual.Alpha < 1f)
            {
                _meshRenderer.GetPropertyBlock(_propBlock);
                var mat = _meshRenderer.sharedMaterial;
                var color = mat.HasProperty(ColorProp)
                    ? mat.GetColor(ColorProp)
                    : mat.GetColor(ColorPropFallback);
                color.a = visual.Alpha;
                _propBlock.SetColor(ColorProp, color);
                _propBlock.SetColor(ColorPropFallback, color);
                _meshRenderer.SetPropertyBlock(_propBlock);
            }
            else if (!_isHinted && !_isHighlighted && !_clipActive)
            {
                _meshRenderer.SetPropertyBlock(null);
            }

            // Visibility
            gameObject.SetActive(visual.IsVisible);

            // Immediate mode: sync appearance from visual state every frame
            ApplyAppearance(visual.TileType);
        }

        /// <summary>
        /// Set hint animation state.
        /// </summary>
        public void SetHinted(bool hinted, HintAnimationType type = HintAnimationType.None, Vector2 nudgeDir = default)
        {
            bool wasHinted = _isHinted;
            _isHinted = hinted;
            _hintType = type;
            _hintNudgeDir = nudgeDir;
            if (!hinted && wasHinted)
            {
                _hintTime = 0f;
                transform.localEulerAngles = BaseTiltEuler;
                // Clear emission
                _meshRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColorProp, Color.black);
                _meshRenderer.SetPropertyBlock(_propBlock);
            }
        }

        /// <summary>
        /// Show/hide per-instance outline (used by hint system).
        /// </summary>
        public void SetOutlined(bool outlined)
        {
            if (_outline != null)
                _outline.SetForceVisible(outlined);
        }

        /// <summary>
        /// Set highlight state for selection feedback.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (_isHighlighted == highlighted) return;
            _isHighlighted = highlighted;

            // Emission glow: on when selected, off when deselected
            _meshRenderer.GetPropertyBlock(_propBlock);
            if (highlighted)
            {
                var mat = _meshRenderer.sharedMaterial;
                var baseColor = mat.HasProperty(ColorProp)
                    ? mat.GetColor(ColorProp)
                    : mat.GetColor(ColorPropFallback);
                _propBlock.SetColor(EmissionColorProp, baseColor * 0.3f);
            }
            else
            {
                _propBlock.SetColor(EmissionColorProp, Color.black);
                _highlightTime = 0f;
                transform.localEulerAngles = BaseTiltEuler;
                transform.localScale = _baseScale;
            }
            _meshRenderer.SetPropertyBlock(_propBlock);
        }

        /// <summary>
        /// Update cached sun direction. Call once per frame from Board3DView.Render.
        /// </summary>
        internal static void UpdateSunDirection()
        {
            int frame = Time.frameCount;
            if (frame == s_sunDirFrame) return;
            s_sunDirFrame = frame;

            var sun = RenderSettings.sun;
            if (sun != null)
            {
                var d = sun.transform.forward;
                var v = new Vector2(d.x, d.y);
                if (v.sqrMagnitude > 0.0001f)
                    s_sunDir2D = v.normalized;
            }
        }

        private void UpdateBlobShadow(Vector3 worldPos, float cellSize, Vector3 tileScale, float tileZ)
        {
            if (_shadowTransform == null) return;

            _hasLastShadowState = true;
            _lastShadowWorldPos = worldPos;
            _lastShadowCellSize = cellSize;
            _lastShadowTileScale = tileScale;
            _lastShadowTileZ = tileZ;

            var dir2 = s_sunDir2D;

            // Lift factor: floating tile → softer, larger shadow
            float lift01 = Mathf.Clamp01(Mathf.Abs(tileZ) / Mathf.Max(cellSize * 0.06f, 0.0001f));
            float alpha = s_blobShadowBaseAlpha * (1f - lift01 * s_blobShadowLiftAlphaReduce);
            float sizeMul = 1f + lift01 * s_blobShadowLiftSizeIncrease;

            float off = s_blobShadowOffset * cellSize;
            _shadowTransform.position = new Vector3(
                worldPos.x + dir2.x * off,
                worldPos.y + dir2.y * off,
                BlobShadowZ);
            _shadowTransform.rotation = Quaternion.identity;

            float shadowWorldSize = cellSize * s_blobShadowSize * sizeMul;
            _shadowTransform.localScale = new Vector3(
                shadowWorldSize / Mathf.Max(Mathf.Abs(tileScale.x), 0.001f),
                shadowWorldSize / Mathf.Max(Mathf.Abs(tileScale.y), 0.001f),
                1f / Mathf.Max(Mathf.Abs(tileScale.z), 0.001f));

            if (_shadowRenderer != null && _shadowPropBlock != null)
            {
                var c = new Color(1f, 1f, 1f, alpha);
                _shadowRenderer.GetPropertyBlock(_shadowPropBlock);
                _shadowPropBlock.SetColor(ShadowColorProp, c);
                _shadowPropBlock.SetColor(ShadowColorPropFallback, c);
                _shadowRenderer.SetPropertyBlock(_shadowPropBlock);
            }
        }

        private void RefreshBlobShadowNow()
        {
            if (!_hasLastShadowState) return;
            UpdateBlobShadow(_lastShadowWorldPos, _lastShadowCellSize, _lastShadowTileScale, _lastShadowTileZ);
        }

        /// <summary>
        /// Re-derive shadow from current transform (for fly animations that bypass UpdateFromVisual).
        /// </summary>
        internal void RefreshShadow(float cellSize)
        {
            UpdateBlobShadow(transform.position, cellSize, transform.localScale, transform.position.z);
        }

        #region UFO Flight

        // Propeller-carries-bomb physics: propeller faces camera, bomb hangs behind
        // Pendulum pivot at propeller — bomb swings with inertia on direction changes
        // Player.cs drives XY: origin 0-35%, smoothstep to target 35-90%, at target 90-100%

        // Gravity angle: 0° = pure down (Y-), 90° = pure into screen (Z+)
        private const float UfoGravityAngle = 70f;
        // Flight arc: Y rise (cells) and scale boost (Z depth)
        private const float UfoArcY = 1.2f;
        private const float UfoArcScale = 2.15f;
        // Propeller spin speed (degrees/sec)
        private const float UfoMaxSpinSpeed = 2700f;

        // Pendulum spring: soft + underdamped = heavy swinging bomb with visible drag
        private const float UfoTiltSpring = 5f;
        private const float UfoTiltDamp = 4f;
        private const float UfoTiltScale = 12f;
        private const float UfoMaxTilt = 50f;

        private void ApplyUfoFlight(TileVisual visual, Vector3 worldPos, float cellSize)
        {
            float progress = visual.UfoFlightProgress; // 0→1
            float dt = Time.deltaTime;

            // Initialize on first frame
            if (!_ufoFlying)
            {
                _ufoFlying = true;
                _ufoSpinAngle = 0f;
                _ufoTiltX = _ufoTiltZ = 0f;
                _ufoTiltVelX = _ufoTiltVelZ = 0f;
                _ufoSmoothVel = Vector3.zero;
                _ufoPrevWorldPos = worldPos;
                _ufoArcCurrent = 0f;
                _ufoArcSnapshot = 0f;
                _retargetBlend = 0f;
                _ufoTakeoffBlend = 0f;
                if (_shadowTransform != null)
                    _shadowTransform.gameObject.SetActive(false);
                // Clear lingering alpha/emission from normal rendering
                _meshRenderer.SetPropertyBlock(null);
            }

            // --- Retarget blend: snapshot current arc when retarget flag is set ---
            if (visual.UfoRetargetFlag)
            {
                visual.UfoRetargetFlag = false;
                _ufoArcSnapshot = _ufoArcCurrent;
                _retargetBlend = 1f;
            }

            // --- 1. Continuous propeller spin ---
            float spinRamp = Mathf.Clamp01(progress / 0.20f);
            _ufoSpinAngle += UfoMaxSpinSpeed * spinRamp * spinRamp * dt;

            // --- 2. Smooth flight arc (continuous, no flat cruise, no pauses) ---
            const float arriveFrac = 0.97f;
            float yOffset, scaleMul;

            // Orientation blend: only increases (never snaps back to upright on retarget).
            // Rate-limited to prevent jump when progress skips from <0.15 to 0.35.
            float rawTakeoff = Mathf.Clamp01(progress / 0.15f);
            rawTakeoff = rawTakeoff * rawTakeoff * (3f - 2f * rawTakeoff);
            float targetBlend = Mathf.Max(_ufoTakeoffBlend, rawTakeoff);
            _ufoTakeoffBlend = Mathf.MoveTowards(_ufoTakeoffBlend, targetBlend, dt * 8f);
            float takeoffBlend = _ufoTakeoffBlend;

            if (progress < arriveFrac)
            {
                // Raw sine arc from progress
                float rawArc = Mathf.Sin(progress / arriveFrac * Mathf.PI);

                // Apply retarget blend: smooth transition from snapshot to new arc
                if (_retargetBlend > 0f)
                {
                    _retargetBlend = Mathf.Max(0f, _retargetBlend - dt / 0.4f);
                    _ufoArcCurrent = Mathf.Lerp(rawArc, _ufoArcSnapshot, _retargetBlend);
                }
                else
                {
                    _ufoArcCurrent = rawArc;
                }

                yOffset = UfoArcY * cellSize * _ufoArcCurrent;
                scaleMul = 1f + (UfoArcScale - 1f) * _ufoArcCurrent;
            }
            else
            {
                // Impact: hold briefly, then shrink to 0.5 (effect covers disappearance)
                _ufoArcCurrent = 0f;
                _retargetBlend = 0f;
                yOffset = 0f;
                float shrinkT = Mathf.Clamp01((progress - 0.98f) / 0.02f);
                scaleMul = Mathf.Lerp(1f, 0.5f, shrinkT * shrinkT);
            }

            // --- 3. Scale + arm length ---
            float s = cellSize * TileScaleMultiplier * scaleMul;
            transform.localScale = new Vector3(s, s, s);
            float armLength = s * 0.5f;

            // --- 4. Propeller anchor position (Y offset + Z depth, blended) ---
            float flightZ = -(armLength + 0.8f) * takeoffBlend;
            var propellerPos = new Vector3(worldPos.x, worldPos.y + yOffset, flightZ);

            // --- 5. Pendulum tilt (bomb swings opposite to movement) ---
            if (dt > 0.0001f)
            {
                Vector3 rawVel = (propellerPos - _ufoPrevWorldPos) / dt;
                _ufoPrevWorldPos = propellerPos;

                const float smoothFactor = 0.15f;
                _ufoSmoothVel = Vector3.Lerp(_ufoSmoothVel, rawVel, smoothFactor);

                // Pendulum: bomb trails behind propeller movement
                // Fade out near arrival so bomb lands precisely on target
                float pendulumFade = Mathf.Clamp01((arriveFrac - progress) * 5f); // fade over last 20%
                float targetX = Mathf.Clamp(_ufoSmoothVel.y * UfoTiltScale, -UfoMaxTilt, UfoMaxTilt) * pendulumFade;
                float targetY = Mathf.Clamp(-_ufoSmoothVel.x * UfoTiltScale, -UfoMaxTilt, UfoMaxTilt) * pendulumFade;

                _ufoTiltVelX += ((targetX - _ufoTiltX) * UfoTiltSpring - _ufoTiltVelX * UfoTiltDamp) * dt;
                _ufoTiltVelZ += ((targetY - _ufoTiltZ) * UfoTiltSpring - _ufoTiltVelZ * UfoTiltDamp) * dt;
                _ufoTiltX += _ufoTiltVelX * dt;
                _ufoTiltZ += _ufoTiltVelZ * dt;
            }

            // --- 6. Rotation: spin → face tilt (blended) → pendulum swing ---
            var spin = Quaternion.Euler(0f, _ufoSpinAngle, 0f);
            // Smoothly tilt from 0° (upright on board) to -GravityAngle (flight orientation)
            var faceCamera = Quaternion.Euler(-UfoGravityAngle * takeoffBlend, 0f, 0f);
            var pendulum = Quaternion.Euler(_ufoTiltX, _ufoTiltZ, 0f);
            var fullRotation = pendulum * faceCamera * spin;
            transform.rotation = fullRotation;

            // --- 7. Pivot at propeller (blended from center to propeller tip) ---
            // At takeoffBlend=0: arm=0 → pivot at center (matches normal rendering)
            // At takeoffBlend=1: arm=full → pivot at propeller tip
            float effectiveArm = armLength * takeoffBlend;
            var propellerLocal = new Vector3(0f, effectiveArm, 0f);
            transform.position = propellerPos - fullRotation * propellerLocal;

            // Visibility
            gameObject.SetActive(visual.IsVisible);
        }

        #endregion

        #region Portal Effects (Shader Clip)

        private bool _clipActive;

        /// <summary>
        /// Set Y-axis clip bounds for hole portal effect.
        /// Pixels outside [clipYMin, clipYMax] are discarded by the shader.
        /// </summary>
        public void SetClipBounds(float clipYMin, float clipYMax)
        {
            _clipActive = true;

            _meshRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat(ClipYMinProp, clipYMin);
            _propBlock.SetFloat(ClipYMaxProp, clipYMax);
            _meshRenderer.SetPropertyBlock(_propBlock);

            // Hide blob shadow while clipping
            if (_shadowTransform != null)
                _shadowTransform.gameObject.SetActive(false);

            // Forward to outline
            if (_outline != null)
                _outline.SetClipBounds(clipYMin, clipYMax);
        }

        /// <summary>
        /// Reset clip bounds to default (no clipping).
        /// </summary>
        public void ResetClipBounds()
        {
            if (!_clipActive) return;
            _clipActive = false;

            _meshRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat(ClipYMinProp, -9999f);
            _propBlock.SetFloat(ClipYMaxProp, 9999f);
            _meshRenderer.SetPropertyBlock(_propBlock);

            // Restore blob shadow
            if (_shadowTransform != null)
                _shadowTransform.gameObject.SetActive(true);

            // Forward to outline
            if (_outline != null)
                _outline.ResetClipBounds();
        }

        #endregion

        #region IPoolable

        public void OnSpawn()
        {
            TileId = -1;
            _baseScale = Vector3.one;
            _isHighlighted = false;
            _isHinted = false;
            _hintType = HintAnimationType.None;
            _hintTime = 0f;
            _wasAnimated = false;
            _bounceTime = -1f;
            _highlightTime = 0f;
            _clipActive = false;
            _ufoFlying = false;
            _ufoSpinAngle = 0f;
            _ufoTiltX = _ufoTiltZ = 0f;
            _ufoTiltVelX = _ufoTiltVelZ = 0f;
            _ufoSmoothVel = Vector3.zero;
            _ufoArcCurrent = 0f;
            _ufoArcSnapshot = 0f;
            _retargetBlend = 0f;
            _ufoTakeoffBlend = 0f;
            _lastMaterials = null;
            transform.localScale = Vector3.one;
            transform.localEulerAngles = BaseTiltEuler;
            if (_shadowTransform != null)
                _shadowTransform.gameObject.SetActive(true);
            _meshRenderer.SetPropertyBlock(null);
            if (_outline != null)
                _outline.ResetState();
        }

        public void OnDespawn()
        {
            TileId = -1;
            gameObject.SetActive(false);
        }

        #endregion
    }
}
