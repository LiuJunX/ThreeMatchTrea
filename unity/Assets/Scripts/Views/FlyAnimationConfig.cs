using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Configuration for tile-to-objective fly animations.
    /// Supports two modes: Mode A (3-connect: jump+hover+fly) and Mode B (bomb merge: direct fly).
    /// </summary>
    public sealed class FlyAnimationConfig
    {
        /// <summary>Minimum flight duration (close distance).</summary>
        public float MinDuration = 0.45f;

        /// <summary>Maximum flight duration (far distance).</summary>
        public float MaxDuration = 0.8f;

        /// <summary>Distance at which MaxDuration is reached.</summary>
        public float DistanceForMaxDuration = 10f;

        /// <summary>Scale at flight start (board size).</summary>
        public float StartScale = 1.0f;

        /// <summary>Scale at flight end (icon size).</summary>
        public float EndScale = 1.2f;

        /// <summary>Scale multiplier during pop-up / jump phase (Mode A).</summary>
        public float PopUpScale = 1.5f;

        /// <summary>Mode A: jump-up duration (seconds).</summary>
        public float JumpUpDuration = 0.2f;

        /// <summary>Mode A: jump height in grid cells (multiplied by CellSize).</summary>
        public float JumpHeight = 1.0f;

        /// <summary>Mode A: hover duration at jump apex (seconds).</summary>
        public float HoverDuration = 0.15f;

        /// <summary>Spin speed during fly phase (degrees per second).</summary>
        public float FlySpinSpeed = 270f;

        /// <summary>Z offset during pop-up and fly phase (negative = toward camera).</summary>
        public float PopUpZOffset = -0.2f;

        /// <summary>Easing function (default: OutCubic).</summary>
        public System.Func<float, float> EasingFunction = t => 1f - Mathf.Pow(1f - t, 3f);

        /// <summary>Stagger delay between successive merge flies (seconds).</summary>
        public float MergeStaggerDelay = 0.08f;

        /// <summary>Calculate flight duration based on distance.</summary>
        public float GetDuration(float distance) =>
            Mathf.Lerp(MinDuration, MaxDuration, Mathf.Clamp01(distance / DistanceForMaxDuration));
    }
}
