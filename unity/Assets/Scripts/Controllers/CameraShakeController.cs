using Match3.Presentation;
using UnityEngine;

namespace Match3.Unity.Controllers
{
    /// <summary>
    /// Manages camera shake effects triggered by destruction events.
    /// Extracted from GameController to isolate screen shake logic.
    /// </summary>
    internal sealed class CameraShakeController
    {
        private Camera _cachedCamera;
        private float _shakeTimer;
        private float _shakeDuration;
        private float _shakeIntensity;
        private Vector3 _cameraOriginalPos;
        private bool _shakeApplied;
        private int _lastShakeEffectCount;

        /// <summary>
        /// Check effect count and trigger shake on rising edge.
        /// Call from Update() after rendering.
        /// </summary>
        public void UpdateEffectCount(VisualState state)
        {
            int effectCount = CountDestructionEffects(state);
            if (effectCount > _lastShakeEffectCount)
            {
                if (effectCount >= 10)
                    TriggerShake(0.2f, 0.15f);
                else if (effectCount >= 5)
                    TriggerShake(0.1f, 0.08f);
            }
            _lastShakeEffectCount = effectCount;
        }

        /// <summary>
        /// Restore camera to original position. Call at start of Update().
        /// </summary>
        public void Restore()
        {
            if (!_shakeApplied) return;

            var cam = GetCamera();
            if (cam != null)
                cam.transform.position = _cameraOriginalPos;
            _shakeApplied = false;
        }

        /// <summary>
        /// Apply shake offset. Call from LateUpdate().
        /// </summary>
        public void Apply()
        {
            if (_shakeTimer <= 0f) return;

            var cam = GetCamera();
            if (cam == null) return;

            _shakeTimer -= Time.deltaTime;

            if (_shakeTimer <= 0f)
            {
                _shakeTimer = 0f;
                return;
            }

            // Damped random offset
            var decay = _shakeTimer / _shakeDuration;
            var offsetX = UnityEngine.Random.Range(-1f, 1f) * _shakeIntensity * decay;
            var offsetY = UnityEngine.Random.Range(-1f, 1f) * _shakeIntensity * decay;
            cam.transform.position = _cameraOriginalPos + new Vector3(offsetX, offsetY, 0f);
            _shakeApplied = true;
        }

        /// <summary>
        /// Reset shake state. Call from GameController.Reset().
        /// </summary>
        public void Reset()
        {
            Restore();
            _shakeTimer = 0f;
            _lastShakeEffectCount = 0;
        }

        private void TriggerShake(float duration, float intensity)
        {
            // Only upgrade to a stronger shake
            if (_shakeTimer > 0f && intensity <= _shakeIntensity) return;

            _shakeDuration = duration;
            _shakeIntensity = intensity;
            _shakeTimer = duration;

            // Capture original position only if not already shaking
            if (!_shakeApplied)
            {
                var cam = GetCamera();
                if (cam != null)
                    _cameraOriginalPos = cam.transform.position;
            }
        }

        private Camera GetCamera()
        {
            if (_cachedCamera == null)
                _cachedCamera = Camera.main;
            return _cachedCamera;
        }

        private static int CountDestructionEffects(VisualState state)
        {
            int count = 0;
            foreach (var effect in state.Effects)
            {
                if (effect.EffectType == "explosion" || effect.EffectType == "bomb_explosion")
                    count++;
            }
            return count;
        }
    }
}
