using Match3.Unity.Controllers;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Shared animation math for 2D (<see cref="TileView"/>) and 3D (<see cref="Tile3DView"/>).
    /// All methods are pure calculations — no side effects, no state mutation.
    /// </summary>
    public static class TileAnimationHelper
    {
        /// <summary>Duration of the landing-bounce effect in seconds.</summary>
        public const float BounceEndTime = 0.28f;

        /// <summary>
        /// Advance the landing-bounce timer and compute the squash factor.
        /// Two-phase: initial squash + damped rebound for a springy feel.
        /// </summary>
        /// <param name="bounceTime">Current bounce timer (negative = inactive).</param>
        /// <param name="dt">Delta time for this frame.</param>
        /// <returns>
        /// <c>newBounceTime</c>: updated timer (set to -1 when finished).
        /// <c>squashFactor</c>: 0-based factor to apply (positive = wider/shorter).
        /// </returns>
        public static (float newBounceTime, float squashFactor, float yOffset) CalculateBounceSquash(float bounceTime, float dt)
        {
            if (bounceTime >= 0f && bounceTime < BounceEndTime)
            {
                bounceTime += dt;
                var t = bounceTime / BounceEndTime;
                // Damped sine: strong first squash, smaller rebound
                var envelope = (1f - t) * (1f - t); // quadratic decay
                var squash = Mathf.Sin(t * Mathf.PI * 2.5f) * 0.05f * envelope;
                // Small Y hop: tile briefly pops up past target then settles
                var hop = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 1.5f)) * 0.03f * envelope;
                return (bounceTime, squash, hop);
            }

            if (bounceTime >= BounceEndTime)
            {
                return (-1f, 0f, 0f);
            }

            // Inactive (bounceTime < 0)
            return (bounceTime, 0f, 0f);
        }

        /// <summary>
        /// Advance the hint timer and compute the scale pulse factor for the active hint type.
        /// </summary>
        /// <param name="hintTime">Current hint timer.</param>
        /// <param name="dt">Delta time for this frame.</param>
        /// <param name="hintType">Active hint animation type.</param>
        /// <returns>
        /// <c>newHintTime</c>: advanced timer.
        /// <c>pulseFactor</c>: multiplicative scale factor (1.0 = no change).
        /// <c>phase</c>: raw sine phase for SwapNudge (used by callers for position/tilt).
        /// </returns>
        public static (float newHintTime, float pulseFactor, float phase) CalculateHintPulse(
            float hintTime, float dt, HintAnimationType hintType)
        {
            hintTime += dt;

            if (hintType == HintAnimationType.BombPulse)
            {
                var pulse = 1f + Mathf.Sin(hintTime * 2f * Mathf.PI * 2f) * 0.06f;
                return (hintTime, pulse, 0f);
            }

            if (hintType == HintAnimationType.SwapNudge)
            {
                var phase = Mathf.Sin(hintTime * Mathf.PI * 2f);
                var pulse = 1f + phase * 0.04f;
                return (hintTime, pulse, phase);
            }

            return (hintTime, 1f, 0f);
        }

        /// <summary>
        /// Compute the nudge offset distance for a SwapNudge hint.
        /// </summary>
        /// <param name="phase">Sine phase from <see cref="CalculateHintPulse"/>.</param>
        /// <param name="cellSize">Cell size in world units.</param>
        /// <returns>Nudge distance in world units (zero when phase is negative).</returns>
        public static float CalculateSwapNudgeOffset(float phase, float cellSize)
        {
            return Mathf.Max(phase, 0f) * 0.12f * cellSize;
        }

        /// <summary>
        /// Compute the tilt angle magnitude for a SwapNudge hint.
        /// </summary>
        /// <param name="phase">Sine phase from <see cref="CalculateHintPulse"/>.</param>
        /// <returns>Tilt angle in degrees (zero when phase is negative).</returns>
        public static float CalculateSwapNudgeTiltAngle(float phase)
        {
            return Mathf.Max(phase, 0f) * 6f;
        }

        /// <summary>
        /// Compute the selection-highlight scale pulse factor.
        /// </summary>
        /// <param name="highlightTime">Current highlight timer (already advanced by caller).</param>
        /// <returns>Multiplicative scale factor (1.0 = no change).</returns>
        public static float CalculateSelectionPulse(float highlightTime)
        {
            return 1f + Mathf.Sin(highlightTime * 8f) * 0.08f;
        }
    }
}
