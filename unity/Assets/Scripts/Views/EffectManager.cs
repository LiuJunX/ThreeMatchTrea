using System.Collections.Generic;
using Match3.Presentation;
using Match3.Unity.Bridge;
using Match3.Unity.Pools;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Manages particle effects for the game.
    /// </summary>
    public sealed class EffectManager : MonoBehaviour
    {
        private Match3Bridge _bridge;
        private Transform _effectContainer;

        private readonly Dictionary<string, Queue<ParticleSystem>> _effectPools = new();
        private readonly Dictionary<int, ActiveEffect> _activeEffects = new();

        private struct ActiveEffect
        {
            public ParticleSystem ParticleSystem;
            public string EffectType;
        }

        /// <summary>
        /// Initialize the effect manager.
        /// </summary>
        public void Initialize(Match3Bridge bridge)
        {
            _bridge = bridge;

            // Create effect container
            _effectContainer = new GameObject("EffectContainer").transform;
            _effectContainer.SetParent(transform, false);

            // Pre-warm common effect pools
            PrewarmPool("match_pop", 10);
            PrewarmPool("explosion", 5);
            PrewarmPool("pop", 10);
        }

        /// <summary>
        /// Update effects from visual state.
        /// Syncs with VisualState's effect list - particles are removed when
        /// VisualState removes the corresponding effect.
        /// </summary>
        public void UpdateEffects(VisualState state)
        {
            if (state == null) return;

            var cellSize = _bridge.CellSize;
            var origin = _bridge.BoardOrigin;
            var height = _bridge.Height;

            // Track which effects are still in state
            var currentHashes = new HashSet<int>();

            // Spawn new effects and track existing ones
            foreach (var effect in state.Effects)
            {
                var hash = ComputeEffectHash(effect);
                currentHashes.Add(hash);

                // Skip if already playing
                if (_activeEffects.ContainsKey(hash))
                    continue;

                // Calculate world position (with Y-flip for Unity coordinate system)
                var worldPos = CoordinateConverter.GridToWorld(effect.Position, cellSize, origin, height);
                PlayEffect(effect.EffectType, worldPos, hash);
            }

            // Remove effects that are no longer in state
            var toRemove = new List<int>();
            foreach (var kvp in _activeEffects)
            {
                if (!currentHashes.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var hash in toRemove)
            {
                if (_activeEffects.TryGetValue(hash, out var active))
                {
                    ReturnToPool(active.EffectType, active.ParticleSystem);
                    _activeEffects.Remove(hash);
                }
            }
        }

        /// <summary>
        /// Play an effect at a position.
        /// </summary>
        public void PlayEffect(string effectType, Vector3 position, int hash)
        {
            var ps = GetFromPool(effectType);
            ps.transform.position = position;
            ps.gameObject.SetActive(true);
            ps.Play();

            _activeEffects[hash] = new ActiveEffect
            {
                ParticleSystem = ps,
                EffectType = effectType
            };
        }

        /// <summary>
        /// Clear all active effects.
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _activeEffects)
            {
                if (kvp.Value.ParticleSystem != null)
                {
                    kvp.Value.ParticleSystem.Stop();
                    ReturnToPool(kvp.Value.EffectType, kvp.Value.ParticleSystem);
                }
            }
            _activeEffects.Clear();
        }

        private void PrewarmPool(string effectType, int count)
        {
            if (!_effectPools.ContainsKey(effectType))
            {
                _effectPools[effectType] = new Queue<ParticleSystem>();
            }

            var pool = _effectPools[effectType];
            for (int i = 0; i < count; i++)
            {
                var ps = ViewFactory.CreateEffect(_effectContainer, effectType);
                ps.gameObject.SetActive(false);
                pool.Enqueue(ps);
            }
        }

        private ParticleSystem GetFromPool(string effectType)
        {
            if (!_effectPools.TryGetValue(effectType, out var pool))
            {
                pool = new Queue<ParticleSystem>();
                _effectPools[effectType] = pool;
            }

            if (pool.Count > 0)
            {
                return pool.Dequeue();
            }

            return ViewFactory.CreateEffect(_effectContainer, effectType);
        }

        private void ReturnToPool(string effectType, ParticleSystem ps)
        {
            if (ps == null) return;

            ps.Stop();
            ps.Clear();
            ps.gameObject.SetActive(false);

            if (!_effectPools.TryGetValue(effectType, out var pool))
            {
                pool = new Queue<ParticleSystem>();
                _effectPools[effectType] = pool;
            }

            if (pool.Count < 20) // Max pool size per type
            {
                pool.Enqueue(ps);
            }
            else
            {
                Destroy(ps.gameObject);
            }
        }

        private static int ComputeEffectHash(VisualEffect effect)
        {
            unchecked
            {
                int hash = effect.EffectType.GetHashCode();
                hash = hash * 31 + effect.Position.X.GetHashCode();
                hash = hash * 31 + effect.Position.Y.GetHashCode();
                hash = hash * 31 + effect.Duration.GetHashCode();
                return hash;
            }
        }

        private void OnDestroy()
        {
            Clear();
            foreach (var pool in _effectPools.Values)
            {
                while (pool.Count > 0)
                {
                    var ps = pool.Dequeue();
                    if (ps != null)
                    {
                        Destroy(ps.gameObject);
                    }
                }
            }
            _effectPools.Clear();
        }
    }
}
