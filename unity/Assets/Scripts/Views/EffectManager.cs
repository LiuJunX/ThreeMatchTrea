using System;
using System.Collections.Generic;
using Match3.Core.Models.Enums;
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
        private bool _initialized;

        private readonly Dictionary<string, Queue<ParticleSystem>> _effectPools = new();
        private readonly Dictionary<int, ActiveEffect> _activeEffects = new();

        // Pre-allocated collections to avoid GC in hot path
        private readonly HashSet<int> _currentHashes = new();
        private readonly List<int> _effectsToRemove = new();

        // Grid position → tile color cache for color-matched effects
        private readonly Dictionary<long, Color> _tileColorCache = new();

        /// <summary>
        /// Grid positions (packed via PackGridKey) where "match_pop"/"pop" effects should be suppressed.
        /// Set by ObjectiveDisplayController to prevent particles for tiles that will fly to objectives.
        /// </summary>
        public HashSet<long> SuppressedPositions { get; set; }

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

            if (_initialized) return;

            // Create effect container
            _effectContainer = new GameObject("EffectContainer").transform;
            _effectContainer.SetParent(transform, false);

            // Pre-warm common effect pools
            PrewarmPool("match_pop", 10);
            PrewarmPool("explosion", 5);
            PrewarmPool("pop", 10);
            PrewarmPool("bomb_shockwave", 3);
            PrewarmPool("bomb_flash", 3);

            _initialized = true;
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

            // Cache tile colors from current visible tiles (before they get removed)
            CacheTileColors(state);

            // Clear pre-allocated collections (no allocation)
            _currentHashes.Clear();
            _effectsToRemove.Clear();

            // Spawn new effects and track existing ones
            foreach (var effect in state.Effects)
            {
                var hash = ComputeEffectHash(effect);
                _currentHashes.Add(hash);

                // Skip if already playing
                if (_activeEffects.ContainsKey(hash))
                    continue;

                // Suppress match_pop/pop for tiles flying to objectives
                if (SuppressedPositions != null && SuppressedPositions.Count > 0
                    && (effect.EffectType == "match_pop" || effect.EffectType == "pop"))
                {
                    var key = PackGridKey(
                        Mathf.RoundToInt(effect.Position.X),
                        Mathf.RoundToInt(effect.Position.Y));
                    if (SuppressedPositions.Contains(key)) continue;
                }

                // Calculate world position (with Y-flip for Unity coordinate system)
                var worldPos = CoordinateConverter.GridToWorld(effect.Position, cellSize, origin, height);

                // Look up cached tile color for this position
                var gridKey = PackGridKey(
                    Mathf.RoundToInt(effect.Position.X),
                    Mathf.RoundToInt(effect.Position.Y));
                _tileColorCache.TryGetValue(gridKey, out var tileColor);

                PlayEffect(effect.EffectType, worldPos, hash, tileColor);
            }

            // Remove effects that are no longer in state
            foreach (var kvp in _activeEffects)
            {
                if (!_currentHashes.Contains(kvp.Key))
                {
                    _effectsToRemove.Add(kvp.Key);
                }
            }

            foreach (var hash in _effectsToRemove)
            {
                if (_activeEffects.TryGetValue(hash, out var active))
                {
                    ReturnToPool(active.EffectType, active.ParticleSystem);
                    _activeEffects.Remove(hash);
                }
            }
        }

        /// <summary>
        /// Play an effect at a position with optional tile color.
        /// </summary>
        public void PlayEffect(string effectType, Vector3 position, int hash, Color tileColor = default)
        {
            var ps = GetFromPool(effectType);
            ps.transform.position = position;

            // Apply tile color to particle start color if available
            if (tileColor != default)
            {
                ApplyEffectColor(ps, effectType, tileColor);
            }

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

        private void CacheTileColors(VisualState state)
        {
            foreach (var kvp in state.Tiles)
            {
                var visual = kvp.Value;
                if (!visual.IsVisible) continue;

                var gridKey = PackGridKey(
                    Mathf.RoundToInt(visual.Position.X),
                    Mathf.RoundToInt(visual.Position.Y));
                _tileColorCache[gridKey] = SpriteFactory.GetTileColor(visual.TileType);
            }
        }

        private static void ApplyEffectColor(ParticleSystem ps, string effectType, Color tileColor)
        {
            var main = ps.main;

            switch (effectType)
            {
                case "match_pop":
                case "pop":
                    // Tint particles with tile color: bright version and slightly dimmer
                    var brightColor = Color.Lerp(tileColor, Color.white, 0.3f);
                    main.startColor = new ParticleSystem.MinMaxGradient(brightColor, tileColor);
                    break;

                case "explosion":
                case "bomb_explosion":
                    // Explosion uses tile color mixed with warm orange
                    var warmColor = Color.Lerp(tileColor, new Color(1f, 0.6f, 0.2f), 0.4f);
                    main.startColor = new ParticleSystem.MinMaxGradient(warmColor, tileColor);
                    break;

                case "bomb_shockwave":
                case "bomb_flash":
                    var flashColor = Color.Lerp(tileColor, Color.white, 0.6f);
                    main.startColor = new ParticleSystem.MinMaxGradient(flashColor, Color.white);
                    break;
            }
        }

        private static long PackGridKey(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }

        private static int ComputeEffectHash(VisualEffect effect)
        {
            unchecked
            {
                int hash = effect.EffectType.GetHashCode();
                hash = hash * 397 + BitConverter.SingleToInt32Bits(effect.Position.X);
                hash = hash * 397 + BitConverter.SingleToInt32Bits(effect.Position.Y);
                hash = hash * 397 + BitConverter.SingleToInt32Bits(effect.Duration);
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
