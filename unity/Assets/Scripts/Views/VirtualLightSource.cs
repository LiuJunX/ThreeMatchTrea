using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Attach this component to visual effects (like explosions or projectiles)
    /// to make them contribute to the BoardLightingController's adaptive damping system.
    /// </summary>
    public class VirtualLightSource : MonoBehaviour, IDynamicBrightnessSource
    {
        [Tooltip("Maximum intensity this effect contributes to the lighting system.")]
        [SerializeField] private float _intensity = 2.0f;

        [Tooltip("Multiplier for how much this light affects the scene damping. Higher values mean stronger darkening effect.")]
        [SerializeField] private float _weight = 1.0f;

        [Tooltip("Optional curve to animate intensity over time. If not set, uses constant Intensity.")]
        [SerializeField] private AnimationCurve _intensityCurve;

        [Tooltip("How long the curve lasts (if using curve).")]
        [SerializeField] private float _duration = 1.0f;

        private float _time;
        private BoardLightingController _controller;

        public void Configure(float intensity, float weight, float duration)
        {
            _intensity = intensity;
            _weight = weight;
            _duration = duration;
            // Create a default bell-curve if none exists
            if (_intensityCurve == null || _intensityCurve.length == 0)
            {
                _intensityCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.2f, 1f),
                    new Keyframe(1f, 0f));
            }
        }

        public float CurrentIntensity
        {
            get
            {
                if (!enabled || !gameObject.activeInHierarchy) return 0f;

                float value = _intensity;
                if (_intensityCurve != null && _intensityCurve.length > 0)
                {
                    float t = Mathf.Clamp01(_time / _duration);
                    value *= _intensityCurve.Evaluate(t);
                }
                return value * _weight;
            }
        }

        private void OnEnable()
        {
            _time = 0f;
            if (_controller == null)
            {
                // Try to find the controller in the scene via Board3DView
                var boardView = FindObjectOfType<Board3DView>();
                if (boardView != null)
                {
                    _controller = boardView.GetLightingController();
                }
            }

            if (_controller != null)
            {
                _controller.RegisterVirtualSource(this);
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.UnregisterVirtualSource(this);
            }
        }

        private void Update()
        {
            if (_intensityCurve != null && _intensityCurve.length > 0)
            {
                _time += Time.deltaTime;
            }
        }
    }
}
