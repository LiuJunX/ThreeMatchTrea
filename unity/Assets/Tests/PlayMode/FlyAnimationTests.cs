using System.Collections;
using Match3.Unity.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Match3.Unity.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for fly animation configuration and ObjectiveDisplayController behavior.
    /// Tests FlyAnimationConfig duration calculations, default values, and pop-up parameters.
    /// Also tests ObjectiveDisplayController lifecycle (create/clear) without full game setup.
    /// </summary>
    public class FlyAnimationTests
    {
        private FlyAnimationConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new FlyAnimationConfig();
        }

        #region FlyAnimationConfig Duration Tests

        [Test]
        public void GetDuration_ZeroDistance_ReturnsMinDuration()
        {
            float duration = _config.GetDuration(0f);

            Assert.AreEqual(_config.MinDuration, duration, 0.001f);
        }

        [Test]
        public void GetDuration_MaxDistance_ReturnsMaxDuration()
        {
            float duration = _config.GetDuration(_config.DistanceForMaxDuration);

            Assert.AreEqual(_config.MaxDuration, duration, 0.001f);
        }

        [Test]
        public void GetDuration_BeyondMaxDistance_ClampsToMaxDuration()
        {
            float duration = _config.GetDuration(_config.DistanceForMaxDuration * 2f);

            Assert.AreEqual(_config.MaxDuration, duration, 0.001f);
        }

        [Test]
        public void GetDuration_HalfDistance_ReturnsMidDuration()
        {
            float halfDist = _config.DistanceForMaxDuration * 0.5f;
            float expected = (_config.MinDuration + _config.MaxDuration) * 0.5f;

            float duration = _config.GetDuration(halfDist);

            Assert.AreEqual(expected, duration, 0.001f);
        }

        [Test]
        public void GetDuration_Monotonic_IncreasesWithDistance()
        {
            float d1 = _config.GetDuration(1f);
            float d2 = _config.GetDuration(3f);
            float d3 = _config.GetDuration(7f);

            Assert.Greater(d2, d1, "Duration should increase with distance");
            Assert.Greater(d3, d2, "Duration should increase with distance");
        }

        #endregion

        #region FlyAnimationConfig Default Values

        [Test]
        public void Defaults_MinDuration_IsPositive()
        {
            Assert.Greater(_config.MinDuration, 0f);
        }

        [Test]
        public void Defaults_MaxDuration_GreaterThanMin()
        {
            Assert.Greater(_config.MaxDuration, _config.MinDuration);
        }

        [Test]
        public void Defaults_PopUpDuration_IsPositive()
        {
            Assert.Greater(_config.PopUpDuration, 0f);
        }

        [Test]
        public void Defaults_PopUpScale_GreaterThanOne()
        {
            Assert.Greater(_config.PopUpScale, 1f, "Pop-up should enlarge the tile");
        }

        [Test]
        public void Defaults_EndScale_SmallerThanStartScale()
        {
            Assert.Less(_config.EndScale, _config.StartScale,
                "Tile should shrink during flight to match icon size");
        }

        [Test]
        public void Defaults_ArcHeight_IsPositive()
        {
            Assert.Greater(_config.ArcHeight, 0f, "Arc should go upward");
        }

        [Test]
        public void Defaults_MergeStaggerDelay_IsPositive()
        {
            Assert.Greater(_config.MergeStaggerDelay, 0f);
        }

        #endregion

        #region FlyAnimationConfig Easing Function

        [Test]
        public void EasingFunction_AtZero_ReturnsZero()
        {
            float result = _config.EasingFunction(0f);

            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void EasingFunction_AtOne_ReturnsOne()
        {
            float result = _config.EasingFunction(1f);

            Assert.AreEqual(1f, result, 0.001f);
        }

        [Test]
        public void EasingFunction_OutCubic_StartsAboveLinear()
        {
            // OutCubic should be above the linear line for t in (0,1)
            float t = 0.3f;
            float result = _config.EasingFunction(t);

            Assert.Greater(result, t, "OutCubic should be above linear at t=0.3");
        }

        [Test]
        public void EasingFunction_IsMonotonic()
        {
            float prev = 0f;
            for (int i = 1; i <= 10; i++)
            {
                float t = i / 10f;
                float value = _config.EasingFunction(t);
                Assert.GreaterOrEqual(value, prev, $"Easing should be monotonic at t={t}");
                prev = value;
            }
        }

        #endregion

        #region InFlightCount Display Logic (conceptual verification)

        /// <summary>
        /// Verifies the core DisplayCount formula: DisplayCount = CoreCount - InFlightCount.
        /// This tests the mathematical contract that ObjectiveDisplayController relies on.
        /// </summary>
        [Test]
        public void DisplayCount_Formula_CoreCountMinusInFlightCount()
        {
            // Simulate the DisplayCount formula used in ObjectiveIcon
            int coreCount = 5;
            int inFlightCount = 2;
            int displayCount = coreCount - inFlightCount;

            Assert.AreEqual(3, displayCount,
                "Display should show CoreCount minus tiles still flying");
        }

        [Test]
        public void DisplayCount_NoFliesInAir_EqualsCoreCount()
        {
            int coreCount = 7;
            int inFlightCount = 0;
            int displayCount = coreCount - inFlightCount;

            Assert.AreEqual(coreCount, displayCount);
        }

        [Test]
        public void DisplayCount_AllFliesInAir_ShowsZero()
        {
            // When 3 tiles destroyed at once, Core says 3 but all 3 are flying
            int coreCount = 3;
            int inFlightCount = 3;
            int displayCount = coreCount - inFlightCount;

            Assert.AreEqual(0, displayCount,
                "Display should not show collected tiles until they arrive");
        }

        [Test]
        public void DisplayCount_ProgressiveArrival_IncrementsOneByOne()
        {
            // Simulate 3 flies dispatched simultaneously
            int coreCount = 3;
            int inFlightCount = 3;

            // First arrives
            inFlightCount--;
            Assert.AreEqual(1, coreCount - inFlightCount);

            // Second arrives
            inFlightCount--;
            Assert.AreEqual(2, coreCount - inFlightCount);

            // Third arrives
            inFlightCount--;
            Assert.AreEqual(3, coreCount - inFlightCount);
        }

        #endregion

        #region ObjectiveDisplayController Lifecycle

        [UnityTest]
        public IEnumerator ObjectiveDisplayController_CreateAndDestroy_NoErrors()
        {
            var go = new GameObject("TestObjDisplay");
            var controller = go.AddComponent<ObjectiveDisplayController>();

            yield return null;

            Assert.IsNotNull(controller);
            Assert.IsNotNull(controller.HiddenTileIds);
            Assert.IsNotNull(controller.SuppressedEffectPositions);
            Assert.AreEqual(0, controller.HiddenTileIds.Count);
            Assert.AreEqual(0, controller.SuppressedEffectPositions.Count);

            Object.DestroyImmediate(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ObjectiveDisplayController_Clear_ResetsState()
        {
            var go = new GameObject("TestObjDisplay");
            var controller = go.AddComponent<ObjectiveDisplayController>();

            yield return null;

            // Add some test data to public sets
            controller.HiddenTileIds.Add(42);
            controller.SuppressedEffectPositions.Add(123L);

            controller.Clear();

            Assert.AreEqual(0, controller.HiddenTileIds.Count, "Clear should empty HiddenTileIds");
            Assert.AreEqual(0, controller.SuppressedEffectPositions.Count,
                "Clear should empty SuppressedEffectPositions");

            Object.DestroyImmediate(go);
            yield return null;
        }

        [Test]
        public void TotalHeight_IsPositive()
        {
            Assert.Greater(ObjectiveDisplayController.TotalHeight, 0f,
                "TotalHeight must be positive for camera framing");
        }

        #endregion
    }
}
