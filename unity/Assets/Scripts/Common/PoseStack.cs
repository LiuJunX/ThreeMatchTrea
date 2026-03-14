using UnityEngine;

namespace Match3.Unity.Common
{
    /// <summary>
    /// Composable transform channels. One PoseStack per animated object.
    /// Each channel holds an independent position / rotation / scale delta,
    /// owned by exactly one animation system. Compose() merges all channels
    /// and writes to the target Transform once per frame.
    ///
    /// Composition rules (all in parent-local space):
    ///   Position — additive.
    ///   Rotation — multiplicative; channel 0 = outermost (parent-like),
    ///              channel N = innermost (child-like / local).
    ///   Scale    — component-wise multiplicative.
    /// </summary>
    public sealed class PoseStack
    {
        /// <summary>
        /// A single pose delta. Default is identity (zero offset, no rotation, unit scale).
        /// </summary>
        public struct Channel
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;

            public static Channel Identity => new Channel
            {
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,
            };
        }

        private readonly Transform _target;
        private readonly Channel[] _channels;

        /// <summary>Composed local position (valid after Compose).</summary>
        public Vector3 Position { get; private set; }

        /// <summary>Composed local rotation (valid after Compose).</summary>
        public Quaternion Rotation { get; private set; }

        /// <summary>Composed local scale (valid after Compose).</summary>
        public Vector3 Scale { get; private set; }

        public int ChannelCount => _channels.Length;

        public PoseStack(Transform target, int channelCount)
        {
            _target = target;
            _channels = new Channel[channelCount];
            for (int i = 0; i < channelCount; i++)
                _channels[i] = Channel.Identity;
        }

        /// <summary>
        /// Ref access to a channel. Each channel should be owned by exactly one system.
        /// </summary>
        public ref Channel this[int index] => ref _channels[index];

        /// <summary>
        /// Merge all channels and apply to the target Transform.
        /// Call once at the end of your update loop.
        /// </summary>
        public void Compose()
        {
            var pos = Vector3.zero;
            var rot = Quaternion.identity;
            var scl = Vector3.one;

            for (int i = 0; i < _channels.Length; i++)
            {
                ref var ch = ref _channels[i];
                pos.x += ch.position.x;
                pos.y += ch.position.y;
                pos.z += ch.position.z;
                rot *= ch.rotation;
                scl.x *= ch.scale.x;
                scl.y *= ch.scale.y;
                scl.z *= ch.scale.z;
            }

            Position = pos;
            Rotation = rot;
            Scale = scl;

            _target.localPosition = pos;
            _target.localRotation = rot;
            _target.localScale = scl;
        }

        /// <summary>Reset one channel to identity.</summary>
        public void ResetChannel(int index)
        {
            _channels[index] = Channel.Identity;
        }

        /// <summary>Reset all channels to identity.</summary>
        public void ResetAll()
        {
            for (int i = 0; i < _channels.Length; i++)
                _channels[i] = Channel.Identity;
        }
    }
}
