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
        private bool _ufoSpinSettling;     // true once spin begins settling to 0°
        private float _ufoSpinSettleAngle; // captured angle at settle start (shortest path to 0)
        private float _ufoTiltX, _ufoTiltZ;
        private float _ufoTiltVelX, _ufoTiltVelZ;
        private Vector3 _ufoPrevWorldPos;
        private Vector3 _ufoSmoothVel; // EMA-smoothed velocity to avoid frame jitter
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

        private const float BounceEndTime = 0.15f;

        // Scale multiplier: makes tiles fill more of the cell
        private const float TileScaleMultiplier = 1.05f;

        // Blob shadow constants
        private const float BlobShadowZ = 0.08f;

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
            ViewHelper.SetMaterials(_meshRenderer, MeshFactory.GetTileMaterialArray(type));

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
                transform.localEulerAngles = Vector3.zero;
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
                    transform.localEulerAngles = new Vector3(tiltX, tiltY, 0f);
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
            else if (!_isHinted && !_isHighlighted)
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
                transform.localEulerAngles = Vector3.zero;
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
                transform.localEulerAngles = Vector3.zero;
                transform.localScale = _baseScale;
            }
            _meshRenderer.SetPropertyBlock(_propBlock);
        }

        private void UpdateBlobShadow(Vector3 worldPos, float cellSize, Vector3 tileScale, float tileZ)
        {
            if (_shadowTransform == null) return;

            _hasLastShadowState = true;
            _lastShadowWorldPos = worldPos;
            _lastShadowCellSize = cellSize;
            _lastShadowTileScale = tileScale;
            _lastShadowTileZ = tileZ;

            // Directional offset along sun direction
            Vector2 dir2 = new Vector2(-0.35f, -0.70f).normalized;
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                var d = sun.transform.forward;
                var v = new Vector2(d.x, d.y);
                if (v.sqrMagnitude > 0.0001f)
                    dir2 = v.normalized;
            }

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

        #region UFO Flight

        // Spin-up phase before flight begins
        private const float UfoSpinUpTime = 0.25f;

        // Parabolic arc: peak Z offset towards camera (negative Z = closer to player)
        private const float UfoArcPeakZ = -0.5f;
        // Peak scale boost at arc apex (closer to camera = bigger)
        private const float UfoArcPeakScale = 1.5f;
        // Landing shrink: last fraction of flight phase where scale goes to 0
        private const float UfoLandFrac = 0.12f;

        // Physics spring constants for tilt — tuned for "heavy ball dragged by propeller"
        private const float UfoTiltSpring = 12f;   // soft spring → slow to respond
        private const float UfoTiltDamp = 14f;      // overdamped → no oscillation
        private const float UfoTiltScale = 4f;      // gentle lean per unit velocity
        private const float UfoMaxTilt = 18f;       // subtle, not acrobatic

        private void ApplyUfoFlight(TileVisual visual, Vector3 worldPos, float cellSize)
        {
            float progress = visual.UfoFlightProgress;
            float duration = visual.UfoFlightDuration;
            float elapsed = progress * duration;
            float dt = Time.deltaTime;

            // Initialize on first frame
            if (!_ufoFlying)
            {
                _ufoFlying = true;
                _ufoSpinAngle = 0f;
                _ufoSpinSettling = false;
                _ufoTiltX = _ufoTiltZ = 0f;
                _ufoTiltVelX = _ufoTiltVelZ = 0f;
                _ufoSmoothVel = Vector3.zero;
                _ufoPrevWorldPos = worldPos;
                if (_shadowTransform != null)
                    _shadowTransform.gameObject.SetActive(false);
            }

            float spinEnd = UfoSpinUpTime;

            // --- 1. Y-axis spin: spin-up then settle to 0° (face camera) ---
            if (elapsed < spinEnd)
            {
                float t = elapsed / spinEnd;
                float spinSpeed = Mathf.Lerp(0f, 1200f, t * t);
                _ufoSpinAngle += spinSpeed * dt;
            }
            else if (!_ufoSpinSettling)
            {
                // Begin settling spin to 0° on first frame after spin-up
                _ufoSpinSettling = true;
                _ufoSpinSettleAngle = _ufoSpinAngle % 360f;
                if (_ufoSpinSettleAngle > 180f) _ufoSpinSettleAngle -= 360f;
                if (_ufoSpinSettleAngle < -180f) _ufoSpinSettleAngle += 360f;
            }

            // After spin-up: flight phase uses a single parabolic arc
            // flightT: 0 = just left origin, 1 = arrived at target
            float flightDuration = Mathf.Max(duration - spinEnd, 0.01f);
            float flightT = Mathf.Clamp01((elapsed - spinEnd) / flightDuration);

            // Settle spin during early flight (first 30% of flight)
            if (elapsed >= spinEnd)
            {
                float settleT = Mathf.Clamp01(flightT / 0.3f);
                float eased = settleT * settleT * (3f - 2f * settleT);
                _ufoSpinAngle = Mathf.Lerp(_ufoSpinSettleAngle, 0f, eased);
            }

            // --- 2. Parabolic Z arc (gravity pulls INTO screen = Z+) ---
            // UFO launches towards camera (Z-), gravity pulls back to board (Z+)
            // arc: 4*t*(1-t) peaks at t=0.5 with value 1
            float arc = 4f * flightT * (1f - flightT);
            float zOffset;
            float depthScale;

            if (elapsed < spinEnd)
            {
                // Spin-up: slight scale increase, no Z offset
                float t = elapsed / spinEnd;
                zOffset = 0f;
                depthScale = Mathf.Lerp(1f, 1.1f, t);
            }
            else
            {
                // Parabolic Z arc towards camera
                zOffset = UfoArcPeakZ * arc * cellSize;

                // Scale follows arc (closer = bigger), with landing shrink at the end
                float arcScale = 1f + (UfoArcPeakScale - 1f) * arc;

                // Landing: shrink to 0 in the final fraction
                float landStart = 1f - UfoLandFrac;
                if (flightT > landStart)
                {
                    float landT = (flightT - landStart) / UfoLandFrac;
                    depthScale = Mathf.Lerp(arcScale, 0f, landT * landT);
                }
                else
                {
                    depthScale = arcScale;
                }
            }

            float s = cellSize * TileScaleMultiplier * depthScale;
            transform.localScale = new Vector3(s, s, s);

            // --- 3. Position (before pivot adjustment) ---
            var pos = new Vector3(worldPos.x, worldPos.y, zOffset);

            // --- 4. Tilt (spring-damped, propeller leads / ball trails) ---
            if (dt > 0.0001f)
            {
                Vector3 rawVel = (pos - _ufoPrevWorldPos) / dt;
                _ufoPrevWorldPos = pos;

                // Exponential moving average to smooth out frame jitter
                const float smoothFactor = 0.15f;
                _ufoSmoothVel = Vector3.Lerp(_ufoSmoothVel, rawVel, smoothFactor);

                // Target tilt: lean into movement direction
                float targetTiltX = Mathf.Clamp(-_ufoSmoothVel.y * UfoTiltScale, -UfoMaxTilt, UfoMaxTilt);
                float targetTiltZ = Mathf.Clamp(_ufoSmoothVel.x * UfoTiltScale, -UfoMaxTilt, UfoMaxTilt);

                // Spring-damper integration (overdamped → sluggish, heavy feel)
                _ufoTiltVelX += ((targetTiltX - _ufoTiltX) * UfoTiltSpring - _ufoTiltVelX * UfoTiltDamp) * dt;
                _ufoTiltVelZ += ((targetTiltZ - _ufoTiltZ) * UfoTiltSpring - _ufoTiltVelZ * UfoTiltDamp) * dt;
                _ufoTiltX += _ufoTiltVelX * dt;
                _ufoTiltZ += _ufoTiltVelZ * dt;
            }

            // --- 5. Apply rotation with pivot at model top (propeller stays, bomb swings) ---
            var rotation = Quaternion.Euler(_ufoTiltX, _ufoSpinAngle, _ufoTiltZ);
            float halfH = s * 0.5f;
            var pivotUp = new Vector3(0f, halfH, 0f);
            transform.position = pos + pivotUp - rotation * pivotUp;
            transform.localEulerAngles = new Vector3(_ufoTiltX, _ufoSpinAngle, _ufoTiltZ);

            // Visibility
            gameObject.SetActive(visual.IsVisible);
        }

        #endregion

        #region Portal Effects

        private bool _portalActive;

        /// <summary>
        /// Set portal clip scale for hole zone entry/exit effect.
        /// </summary>
        /// <param name="scaleY">0~1 visible ratio.</param>
        /// <param name="anchorTop">True = shrink from bottom (entering), false = grow from bottom (exiting).</param>
        /// <param name="cellSize">Cell size for offset calculation.</param>
        public void SetPortalScale(float scaleY, bool anchorTop, float cellSize)
        {
            _portalActive = true;
            scaleY = Mathf.Clamp01(scaleY);
            var s = transform.localScale;
            float fullY = _baseScale.y;
            if (fullY < 0.001f) fullY = cellSize * TileScaleMultiplier;
            s.y = fullY * scaleY;
            transform.localScale = s;

            // Offset position to anchor the visible part at top or bottom
            float offset = fullY * (1f - scaleY) * 0.5f;
            var pos = transform.position;
            if (anchorTop)
                pos.y += offset; // keep top edge, shrink downward
            else
                pos.y -= offset; // keep bottom edge, grow upward
            transform.position = pos;
        }

        /// <summary>
        /// Reset portal scale effect to normal rendering.
        /// </summary>
        public void ResetPortalScale()
        {
            if (!_portalActive) return;
            _portalActive = false;
            transform.localScale = _baseScale;
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
            _portalActive = false;
            _ufoFlying = false;
            _ufoSpinAngle = 0f;
            _ufoSpinSettling = false;
            _ufoSpinSettleAngle = 0f;
            _ufoTiltX = _ufoTiltZ = 0f;
            _ufoTiltVelX = _ufoTiltVelZ = 0f;
            _ufoSmoothVel = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.localEulerAngles = Vector3.zero;
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
