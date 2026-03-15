using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Match3.Unity.Pools;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Attach to the same GameObject as Board3DView (or anywhere in the scene).
    /// While playing, you can tweak the referenced RenderTuningSettings asset in Inspector
    /// and see changes applied live.
    /// </summary>
    public sealed class RenderTuningController : MonoBehaviour
    {
        [SerializeField] private RenderTuningSettings _settings;

        private Board3DView _board3D;
        private int _lastHash;
        private bool _forceApply;

        private Volume _volume;
        private Bloom _bloom;

        private void Awake()
        {
            _board3D = GetComponent<Board3DView>();
            if (_board3D == null)
                _board3D = FindAnyBoard3DView();
        }

        private static Board3DView FindAnyBoard3DView()
        {
            // Compatibility across Unity versions
            return Object.FindObjectOfType<Board3DView>();
        }

        internal void SetSettings(RenderTuningSettings settings)
        {
            _settings = settings;
        }

        [ContextMenu("Refresh Now")]
        public void ForceRefresh()
        {
            _forceApply = true;
        }

        private void Update()
        {
            if (_settings == null)
            {
                // Auto-wire settings at runtime; no manual drag/drop required.
                _settings = RenderTuningRuntime.Settings;
                if (_settings == null) return;
                _forceApply = true;
            }

            // Cheap change detection to avoid applying every frame.
            int hash = ComputeHash(_settings);
            if (!_forceApply && hash == _lastHash) return;
            _lastHash = hash;
            _forceApply = false;

            // Apply to systems (safe no-ops if not present).
            if (_board3D != null)
                _board3D.ApplyRenderTuning(_settings);

            MeshFactory.ApplyRenderTuning(_settings);
            Tile3DView.ApplyRenderTuning(_settings);

            ApplyBloom(_settings);
        }

        private void EnsureVolume()
        {
            if (_volume != null) return;

            var go = new GameObject("~PostProcessVolume");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);

            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _bloom = profile.Add<Bloom>();
            _volume.sharedProfile = profile;

            // Enable post-processing on main camera
            var cam = Camera.main;
            if (cam != null)
            {
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null)
                    camData.renderPostProcessing = true;
            }
        }

        private void ApplyBloom(RenderTuningSettings s)
        {
            EnsureVolume();

            _bloom.active = s.BloomEnabled;
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = s.BloomThreshold;
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = s.BloomIntensity;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = s.BloomScatter;
        }

        private static int ComputeHash(RenderTuningSettings s)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + s.KeyIntensity.GetHashCode();
                h = h * 31 + s.KeyShadowStrength.GetHashCode();
                h = h * 31 + s.FillIntensity.GetHashCode();

                h = h * 31 + s.ReflectionIntensity.GetHashCode();
                h = h * 31 + s.ReflectionSky.GetHashCode();
                h = h * 31 + s.ReflectionSide.GetHashCode();
                h = h * 31 + s.ReflectionGround.GetHashCode();

                h = h * 31 + s.Metallic.GetHashCode();
                h = h * 31 + s.Smoothness.GetHashCode();
                h = h * 31 + s.ClearCoatMask.GetHashCode();
                h = h * 31 + s.ClearCoatSmoothness.GetHashCode();

                h = h * 31 + s.BloomEnabled.GetHashCode();
                h = h * 31 + s.BloomThreshold.GetHashCode();
                h = h * 31 + s.BloomIntensity.GetHashCode();
                h = h * 31 + s.BloomScatter.GetHashCode();

                h = h * 31 + s.BlobSize.GetHashCode();
                h = h * 31 + s.BlobBaseAlpha.GetHashCode();
                h = h * 31 + s.BlobOffset.GetHashCode();
                h = h * 31 + s.BlobLiftAlphaReduce.GetHashCode();
                h = h * 31 + s.BlobLiftSizeIncrease.GetHashCode();
                return h;
            }
        }
    }
}

