using UnityEngine;
using UnityEngine.Rendering;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Manages the reflection cubemap used for Fresnel highlights in 3D mode.
    /// Extracted from Board3DView to isolate environment reflection setup.
    /// </summary>
    internal static class ReflectionCubemapManager
    {
        private static Cubemap _cubemap;

        /// <summary>
        /// Setup reflection cubemap with default colors.
        /// </summary>
        public static void Setup()
        {
            UpdateColors(
                sky: new Color(0.42f, 0.40f, 0.37f),
                side: new Color(0.30f, 0.29f, 0.27f),
                ground: new Color(0.18f, 0.17f, 0.16f));
            RenderSettings.reflectionIntensity = 1.08f;
        }

        /// <summary>
        /// Update cubemap face colors and apply.
        /// Creates the cubemap if it doesn't exist yet.
        /// </summary>
        public static void UpdateColors(Color sky, Color side, Color ground)
        {
            const int size = 16;
            _cubemap ??= new Cubemap(size, TextureFormat.RGBA32, false);

            int s = _cubemap.width;
            var pixels = new Color[s * s];

            Fill(pixels, side);
            _cubemap.SetPixels(pixels, CubemapFace.PositiveX);
            _cubemap.SetPixels(pixels, CubemapFace.NegativeX);
            _cubemap.SetPixels(pixels, CubemapFace.PositiveZ);
            _cubemap.SetPixels(pixels, CubemapFace.NegativeZ);

            Fill(pixels, sky);
            _cubemap.SetPixels(pixels, CubemapFace.PositiveY);

            Fill(pixels, ground);
            _cubemap.SetPixels(pixels, CubemapFace.NegativeY);

            _cubemap.Apply();

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = _cubemap;
        }

        /// <summary>
        /// Set reflection intensity.
        /// </summary>
        public static void SetIntensity(float intensity)
        {
            RenderSettings.reflectionIntensity = intensity;
        }

        private static void Fill(Color[] pixels, Color c)
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = c;
        }
    }
}
