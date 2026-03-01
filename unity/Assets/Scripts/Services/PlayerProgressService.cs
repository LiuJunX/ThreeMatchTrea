using System;
using System.Collections.Generic;
using System.IO;
using Match3.Core.Progress;
using UnityEngine;

namespace Match3.Unity.Services
{
    /// <summary>
    /// JSON-file backed progress storage.
    /// Caches in memory; Load() reads disk only once.
    /// </summary>
    public sealed class PlayerProgressService : IProgressStorage
    {
        private readonly string _filePath;
        private PlayerProgress _cached;

        public PlayerProgressService()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "player_progress.json");
        }

        public PlayerProgress Load()
        {
            if (_cached != null) return _cached;

            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var proxy = JsonUtility.FromJson<ProgressProxy>(json);
                    _cached = proxy?.ToPlayerProgress();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayerProgressService] Failed to load progress: {ex.Message}");
                }
            }

            _cached ??= new PlayerProgress();
            return _cached;
        }

        public void Save(PlayerProgress progress)
        {
            _cached = progress;
            try
            {
                var proxy = ProgressProxy.FromPlayerProgress(progress);
                var json = JsonUtility.ToJson(proxy, true);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerProgressService] Failed to save progress: {ex.Message}");
            }
        }

        /// <summary>
        /// JsonUtility-compatible proxy for PlayerProgress.
        /// Converts Dictionary/HashSet to serializable lists.
        /// </summary>
        [Serializable]
        private class ProgressProxy
        {
            public List<string> bestStarsKeys = new();
            public List<int> bestStarsValues = new();
            public List<string> unlockedLevels = new();

            public PlayerProgress ToPlayerProgress()
            {
                var p = new PlayerProgress();
                var count = Mathf.Min(bestStarsKeys.Count, bestStarsValues.Count);
                for (int i = 0; i < count; i++)
                    p.BestStars[bestStarsKeys[i]] = bestStarsValues[i];
                foreach (var id in unlockedLevels)
                    p.UnlockedLevels.Add(id);
                return p;
            }

            public static ProgressProxy FromPlayerProgress(PlayerProgress p)
            {
                var proxy = new ProgressProxy();
                foreach (var kvp in p.BestStars)
                {
                    proxy.bestStarsKeys.Add(kvp.Key);
                    proxy.bestStarsValues.Add(kvp.Value);
                }
                foreach (var id in p.UnlockedLevels)
                    proxy.unlockedLevels.Add(id);
                return proxy;
            }
        }
    }
}
