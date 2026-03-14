using Match3.Unity.Common;
using NUnit.Framework;
using UnityEngine;

namespace Match3.Unity.Tests
{
    /// <summary>
    /// Tests for PoseStack — composable transform channels.
    /// Verifies composition rules: position additive, rotation multiplicative
    /// (ch0 outermost → chN innermost), scale component-wise multiplicative.
    /// </summary>
    public class PoseStackTests
    {
        private GameObject _go;
        private Transform _transform;

        private const float Epsilon = 0.001f;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("PoseStackTestObject");
            _transform = _go.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        #region Identity

        [Test]
        public void Compose_AllIdentity_TransformIsIdentity()
        {
            var stack = new PoseStack(_transform, 3);
            stack.Compose();

            AssertVector3(Vector3.zero, _transform.localPosition, "position");
            AssertQuaternion(Quaternion.identity, _transform.localRotation, "rotation");
            AssertVector3(Vector3.one, _transform.localScale, "scale");
        }

        [Test]
        public void Compose_ZeroChannels_TransformIsIdentity()
        {
            var stack = new PoseStack(_transform, 0);
            stack.Compose();

            AssertVector3(Vector3.zero, _transform.localPosition, "position");
            AssertQuaternion(Quaternion.identity, _transform.localRotation, "rotation");
            AssertVector3(Vector3.one, _transform.localScale, "scale");
        }

        #endregion

        #region Position (additive)

        [Test]
        public void Compose_SinglePosition_AppliedDirectly()
        {
            var stack = new PoseStack(_transform, 1);
            stack[0].position = new Vector3(1f, 2f, 3f);
            stack.Compose();

            AssertVector3(new Vector3(1f, 2f, 3f), _transform.localPosition, "single position");
        }

        [Test]
        public void Compose_MultiplePositions_Additive()
        {
            var stack = new PoseStack(_transform, 3);
            stack[0].position = new Vector3(1f, 0f, 0f);
            stack[1].position = new Vector3(0f, 2f, 0f);
            stack[2].position = new Vector3(0f, 0f, 3f);
            stack.Compose();

            AssertVector3(new Vector3(1f, 2f, 3f), _transform.localPosition, "additive position");
        }

        [Test]
        public void Compose_PositionAddition_IsCommutative()
        {
            // Order of position channels doesn't matter (addition is commutative)
            var stackA = new PoseStack(_transform, 2);
            stackA[0].position = new Vector3(1f, 2f, 0f);
            stackA[1].position = new Vector3(3f, 4f, 0f);
            stackA.Compose();
            var posA = _transform.localPosition;

            var stackB = new PoseStack(_transform, 2);
            stackB[0].position = new Vector3(3f, 4f, 0f);
            stackB[1].position = new Vector3(1f, 2f, 0f);
            stackB.Compose();
            var posB = _transform.localPosition;

            AssertVector3(posA, posB, "position addition should be commutative");
        }

        #endregion

        #region Rotation (multiplicative, ch0 outermost)

        [Test]
        public void Compose_SingleRotation_AppliedDirectly()
        {
            var stack = new PoseStack(_transform, 1);
            var rot = Quaternion.Euler(30f, 0f, 0f);
            stack[0].rotation = rot;
            stack.Compose();

            AssertQuaternion(rot, _transform.localRotation, "single rotation");
        }

        [Test]
        public void Compose_RotationOrder_Ch0OutermostChNInnermost()
        {
            // ch0 = outer (parent-like), ch1 = inner (child-like, local)
            // Composed rotation = ch0 * ch1
            // Applied to point: ch0 * (ch1 * point) → ch1 acts first (local), ch0 wraps (world)
            var tilt = Quaternion.Euler(-10f, 0f, 0f);
            var spin = Quaternion.Euler(0f, 90f, 0f);

            var stack = new PoseStack(_transform, 2);
            stack[0].rotation = tilt; // outer
            stack[1].rotation = spin; // inner (local)
            stack.Compose();

            // Expected: tilt * spin
            var expected = tilt * spin;
            AssertQuaternion(expected, _transform.localRotation, "tilt(outer) * spin(inner)");
        }

        [Test]
        public void Compose_RotationOrder_IsNotCommutative()
        {
            // Swapping channel order should produce different results
            var rotX = Quaternion.Euler(45f, 0f, 0f);
            var rotY = Quaternion.Euler(0f, 45f, 0f);

            var stackA = new PoseStack(_transform, 2);
            stackA[0].rotation = rotX;
            stackA[1].rotation = rotY;
            stackA.Compose();
            var resultA = _transform.localRotation;

            var stackB = new PoseStack(_transform, 2);
            stackB[0].rotation = rotY;
            stackB[1].rotation = rotX;
            stackB.Compose();
            var resultB = _transform.localRotation;

            // rotX * rotY != rotY * rotX (non-commutative)
            var dot = Mathf.Abs(Quaternion.Dot(resultA, resultB));
            Assert.Less(dot, 0.999f, "Rotation composition should be non-commutative for non-parallel axes");
        }

        [Test]
        public void Compose_IdentityRotationChannels_DoNotAffectResult()
        {
            // Identity channels in between should not change the result
            var rot = Quaternion.Euler(30f, 60f, 0f);

            var stack = new PoseStack(_transform, 4);
            // ch0 = identity, ch1 = rot, ch2 = identity, ch3 = identity
            stack[1].rotation = rot;
            stack.Compose();

            AssertQuaternion(rot, _transform.localRotation, "identity channels should be transparent");
        }

        [Test]
        public void Compose_TiltThenSpin_TiltStaysWorldSpace()
        {
            // This is the actual tile use case:
            // ch0 = BaseTilt(-10° X), ch1 = SelectionSpin(Y)
            // The tilt should stay in world X regardless of spin angle
            var tilt = Quaternion.Euler(-10f, 0f, 0f);
            var spin90 = Quaternion.Euler(0f, 90f, 0f);
            var spin180 = Quaternion.Euler(0f, 180f, 0f);

            // At spin=90°
            var stack = new PoseStack(_transform, 2);
            stack[0].rotation = tilt;
            stack[1].rotation = spin90;
            stack.Compose();
            var forward90 = _transform.localRotation * Vector3.forward;

            // At spin=180°
            stack[1].rotation = spin180;
            stack.Compose();
            var forward180 = _transform.localRotation * Vector3.forward;

            // Both should have the same Y component (tilt stays in world X,
            // so the forward vector's Y depends only on tilt, not spin)
            Assert.AreEqual(forward90.y, forward180.y, Epsilon,
                "Y component of forward should be constant (tilt in world space)");
        }

        #endregion

        #region Scale (component-wise multiplicative)

        [Test]
        public void Compose_SingleScale_AppliedDirectly()
        {
            var stack = new PoseStack(_transform, 1);
            stack[0].scale = new Vector3(2f, 3f, 4f);
            stack.Compose();

            AssertVector3(new Vector3(2f, 3f, 4f), _transform.localScale, "single scale");
        }

        [Test]
        public void Compose_MultipleScales_Multiplicative()
        {
            var stack = new PoseStack(_transform, 2);
            stack[0].scale = new Vector3(2f, 3f, 1f);
            stack[1].scale = new Vector3(1f, 1f, 4f);
            stack.Compose();

            AssertVector3(new Vector3(2f, 3f, 4f), _transform.localScale, "multiplicative scale");
        }

        [Test]
        public void Compose_ScaleMultiplication_IsCommutative()
        {
            var stackA = new PoseStack(_transform, 2);
            stackA[0].scale = new Vector3(2f, 3f, 0.5f);
            stackA[1].scale = new Vector3(0.5f, 2f, 4f);
            stackA.Compose();
            var scaleA = _transform.localScale;

            var stackB = new PoseStack(_transform, 2);
            stackB[0].scale = new Vector3(0.5f, 2f, 4f);
            stackB[1].scale = new Vector3(2f, 3f, 0.5f);
            stackB.Compose();
            var scaleB = _transform.localScale;

            AssertVector3(scaleA, scaleB, "scale multiplication should be commutative");
        }

        [Test]
        public void Compose_BounceSquash_PreservesVolume()
        {
            // Squash/stretch: X/Z expand, Y compress — volume roughly preserved
            var stack = new PoseStack(_transform, 2);
            stack[0].scale = new Vector3(1f, 1f, 1f); // base
            float squash = 0.1f;
            stack[1].scale = new Vector3(1f + squash, 1f - squash, 1f + squash);
            stack.Compose();

            var s = _transform.localScale;
            float volume = s.x * s.y * s.z;
            // (1.1)(0.9)(1.1) = 1.089 ≈ 1.0
            Assert.AreEqual(1f, volume, 0.1f, "Squash/stretch should roughly preserve volume");
        }

        #endregion

        #region Mixed channels

        [Test]
        public void Compose_AllChannelTypes_ComposeCorrectly()
        {
            // Simulate the tile channel layout:
            // ch0 = Grid (pos + scale), ch1 = Effect (pos), ch2 = BaseTilt (rot),
            // ch3 = Dynamic (rot), ch4 = FxScale (scale)
            var stack = new PoseStack(_transform, 5);

            stack[0].position = new Vector3(3f, 5f, 0f);   // grid pos
            stack[0].scale = new Vector3(0.8f, 0.8f, 0.8f); // base scale

            stack[1].position = new Vector3(0f, 0f, 0.1f);  // selection float Z

            stack[2].rotation = Quaternion.Euler(-10f, 0f, 0f); // base tilt

            stack[3].rotation = Quaternion.Euler(0f, 45f, 0f);  // selection spin

            stack[4].scale = new Vector3(1.08f, 0.92f, 1.08f);  // bounce squash

            stack.Compose();

            // Position: (3, 5, 0) + (0, 0, 0.1) = (3, 5, 0.1)
            AssertVector3(new Vector3(3f, 5f, 0.1f), _transform.localPosition, "mixed pos");

            // Rotation: tilt * spin
            var expectedRot = Quaternion.Euler(-10f, 0f, 0f) * Quaternion.Euler(0f, 45f, 0f);
            AssertQuaternion(expectedRot, _transform.localRotation, "mixed rot");

            // Scale: (0.8, 0.8, 0.8) * (1.08, 0.92, 1.08) = (0.864, 0.736, 0.864)
            AssertVector3(new Vector3(0.864f, 0.736f, 0.864f), _transform.localScale, "mixed scale");
        }

        #endregion

        #region Composed properties

        [Test]
        public void ComposedProperties_MatchTransform()
        {
            var stack = new PoseStack(_transform, 2);
            stack[0].position = new Vector3(1f, 2f, 3f);
            stack[0].rotation = Quaternion.Euler(10f, 20f, 30f);
            stack[0].scale = new Vector3(2f, 2f, 2f);
            stack[1].position = new Vector3(4f, 5f, 6f);
            stack.Compose();

            AssertVector3(stack.Position, _transform.localPosition, "Position property");
            AssertQuaternion(stack.Rotation, _transform.localRotation, "Rotation property");
            AssertVector3(stack.Scale, _transform.localScale, "Scale property");
        }

        #endregion

        #region Reset

        [Test]
        public void ResetChannel_RestoresToIdentity()
        {
            var stack = new PoseStack(_transform, 2);
            stack[0].position = new Vector3(1f, 2f, 3f);
            stack[0].rotation = Quaternion.Euler(45f, 0f, 0f);
            stack[0].scale = new Vector3(2f, 2f, 2f);
            stack[1].position = new Vector3(10f, 0f, 0f);

            stack.ResetChannel(0);
            stack.Compose();

            // Only ch1 should contribute
            AssertVector3(new Vector3(10f, 0f, 0f), _transform.localPosition, "after reset ch0");
            AssertQuaternion(Quaternion.identity, _transform.localRotation, "rotation after reset");
            AssertVector3(Vector3.one, _transform.localScale, "scale after reset");
        }

        [Test]
        public void ResetAll_RestoresAllToIdentity()
        {
            var stack = new PoseStack(_transform, 3);
            stack[0].position = new Vector3(1f, 2f, 3f);
            stack[1].rotation = Quaternion.Euler(45f, 45f, 0f);
            stack[2].scale = new Vector3(3f, 3f, 3f);

            stack.ResetAll();
            stack.Compose();

            AssertVector3(Vector3.zero, _transform.localPosition, "pos after reset all");
            AssertQuaternion(Quaternion.identity, _transform.localRotation, "rot after reset all");
            AssertVector3(Vector3.one, _transform.localScale, "scale after reset all");
        }

        [Test]
        public void ResetChannel_ThenSetAgain_Works()
        {
            var stack = new PoseStack(_transform, 1);
            stack[0].position = new Vector3(5f, 0f, 0f);
            stack.Compose();
            AssertVector3(new Vector3(5f, 0f, 0f), _transform.localPosition, "before reset");

            stack.ResetChannel(0);
            stack[0].position = new Vector3(0f, 10f, 0f);
            stack.Compose();
            AssertVector3(new Vector3(0f, 10f, 0f), _transform.localPosition, "after reset + set");
        }

        #endregion

        #region Per-frame overwrite pattern

        [Test]
        public void OverwriteEachFrame_NoAccumulation()
        {
            // Simulate the tile pattern: write channels every frame, compose once
            var stack = new PoseStack(_transform, 3);
            stack[1].rotation = Quaternion.Euler(-10f, 0f, 0f); // constant tilt

            // Frame 1
            stack[0].position = new Vector3(3f, 5f, 0f);
            stack[2].rotation = Quaternion.Euler(0f, 10f, 0f);
            stack.Compose();
            var pos1 = _transform.localPosition;

            // Frame 2: overwrite (no reset needed since we fully overwrite)
            stack[0].position = new Vector3(4f, 6f, 0f);
            stack[2].rotation = Quaternion.Euler(0f, 20f, 0f);
            stack.Compose();

            // Should reflect frame 2 values, not accumulate
            AssertVector3(new Vector3(4f, 6f, 0f), _transform.localPosition, "frame 2 pos");
            Assert.AreNotEqual(pos1.x, _transform.localPosition.x, "should not accumulate");
        }

        #endregion

        #region Ref access

        [Test]
        public void RefIndexer_ModifiesInPlace()
        {
            var stack = new PoseStack(_transform, 1);

            // Get ref and modify
            ref var ch = ref stack[0];
            ch.position = new Vector3(7f, 8f, 9f);
            stack.Compose();

            AssertVector3(new Vector3(7f, 8f, 9f), _transform.localPosition, "ref modification");
        }

        [Test]
        public void ChannelCount_MatchesConstructor()
        {
            var stack = new PoseStack(_transform, 5);
            Assert.AreEqual(5, stack.ChannelCount);
        }

        #endregion

        #region Helpers

        private static void AssertVector3(Vector3 expected, Vector3 actual, string label)
        {
            Assert.AreEqual(expected.x, actual.x, Epsilon, $"{label}.x");
            Assert.AreEqual(expected.y, actual.y, Epsilon, $"{label}.y");
            Assert.AreEqual(expected.z, actual.z, Epsilon, $"{label}.z");
        }

        private static void AssertQuaternion(Quaternion expected, Quaternion actual, string label)
        {
            // Quaternions q and -q represent the same rotation
            float dot = Mathf.Abs(Quaternion.Dot(expected, actual));
            Assert.Greater(dot, 1f - Epsilon, $"{label}: quaternions differ (dot={dot})");
        }

        #endregion
    }
}
