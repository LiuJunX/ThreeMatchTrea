using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Configuration for tile-to-objective fly animations.
    /// </summary>
    public sealed class FlyAnimationConfig
    {
        /// <summary>Minimum flight duration (close distance).</summary>
        public float MinDuration = 0.45f;

        /// <summary>Maximum flight duration (far distance).</summary>
        public float MaxDuration = 0.8f;

        /// <summary>Distance at which MaxDuration is reached.</summary>
        public float DistanceForMaxDuration = 10f;

        /// <summary>Arc height above straight-line path.</summary>
        public float ArcHeight = 1.0f;

        /// <summary>Scale at flight start (board size).</summary>
        public float StartScale = 1.0f;

        /// <summary>Scale at flight end (icon size).</summary>
        public float EndScale = 0.7f;

        /// <summary>Brief pause at start: tile pops up then holds before flying (Gardenscapes-style).</summary>
        public float PopUpDuration = 0.15f;

        /// <summary>Scale multiplier during pop-up phase (e.g. 1.3 = 30% bigger).</summary>
        public float PopUpScale = 1.3f;

        /// <summary>Easing function (default: OutCubic).</summary>
        public System.Func<float, float> EasingFunction = t => 1f - Mathf.Pow(1f - t, 3f);

        /// <summary>Stagger delay between successive merge flies (seconds).</summary>
        public float MergeStaggerDelay = 0.08f;

        /// <summary>Calculate flight duration based on distance.</summary>
        public float GetDuration(float distance) =>
            Mathf.Lerp(MinDuration, MaxDuration, Mathf.Clamp01(distance / DistanceForMaxDuration));
    }
}
