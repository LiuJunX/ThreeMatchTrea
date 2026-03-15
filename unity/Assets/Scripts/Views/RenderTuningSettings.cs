using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Runtime-tweakable render tuning knobs for 3D/2D presentation.
    /// Intentionally kept in Unity layer (no Core dependencies).
    /// </summary>
    [CreateAssetMenu(menuName = "Match3/Render Tuning Settings", fileName = "RenderTuningSettings")]
    public sealed class RenderTuningSettings : ScriptableObject
    {
        [Header("Lighting")]
        [Range(0f, 3f)] public float KeyIntensity = 1.0f;
        [Range(0f, 1f)] public float KeyShadowStrength = 0.8f;
        [Range(0f, 2f)] public float FillIntensity = 0.18f;

        [Header("Environment Reflections (Fresnel)")]
        [Range(0f, 2f)] public float ReflectionIntensity = 1.08f;
        public Color ReflectionSky = new(0.42f, 0.40f, 0.37f);
        public Color ReflectionSide = new(0.30f, 0.29f, 0.27f);
        public Color ReflectionGround = new(0.18f, 0.17f, 0.16f);

        [Header("Tile Material (3D)")]
        [Range(0f, 1f)] public float Metallic = 0.05f;
        [Range(0f, 1f)] public float Smoothness = 0.62f;
        [Range(0f, 1f)] public float ClearCoatMask = 0.07f;
        [Range(0f, 1f)] public float ClearCoatSmoothness = 0.75f;

        [Header("Bloom")]
        public bool BloomEnabled = true;
        [Range(0f, 2f)] public float BloomThreshold = 0.9f;
        [Range(0f, 3f)] public float BloomIntensity = 0.4f;
        [Range(0f, 1f)] public float BloomScatter = 0.7f;

        [Header("Blob Shadow (3D contact shadow)")]
        [Range(0.1f, 1.2f)] public float BlobSize = 0.54f;
        [Range(0f, 0.5f)] public float BlobBaseAlpha = 0.11f;
        [Range(0f, 0.2f)] public float BlobOffset = 0.025f;
        [Range(0f, 1f)] public float BlobLiftAlphaReduce = 0.55f;
        [Range(0f, 1f)] public float BlobLiftSizeIncrease = 0.18f;
    }
}

