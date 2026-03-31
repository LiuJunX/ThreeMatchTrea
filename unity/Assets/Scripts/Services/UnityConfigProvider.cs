using System;
using System.IO;
using System.Linq;
using Match3.Core.Config;
using UnityEngine;

namespace Match3.Unity.Services
{
    /// <summary>
    /// Unity-specific configuration provider.
    /// In Editor: loads from project root config/ directory via File.IO.
    /// In Build: loads from Resources/config via Resources.Load.
    /// </summary>
    public static class UnityConfigProvider
    {
        private static FileConfigProvider _instance;
        private static bool _initialized;

        public static IConfigProvider Instance
        {
            get
            {
                EnsureInitialized();
                return _instance;
            }
        }

        public static void Initialize()
        {
            if (_initialized) return;

#if UNITY_EDITOR
            InitializeFromFileSystem();
#else
            InitializeFromResources();
#endif
            _initialized = true;
        }

        public static void Reload()
        {
            _instance?.ClearCache();
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }

        private static void InitializeFromFileSystem()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var configRoot = Path.Combine(projectRoot, "config");

            if (!Directory.Exists(configRoot))
            {
                Debug.LogError($"[Config] Config not found: {configRoot}");
                return;
            }

            Debug.Log($"[Config] Loading from filesystem: {configRoot}");

            _instance = new FileConfigProvider(
                configRoot,
                readFile: path =>
                {
                    var p = path.Replace('/', Path.DirectorySeparatorChar);
                    return File.ReadAllText(p);
                },
                listFiles: path =>
                {
                    var p = path.Replace('/', Path.DirectorySeparatorChar);
                    if (!Directory.Exists(p)) return Array.Empty<string>();
                    return Directory.GetFiles(p)
                        .Select(Path.GetFileName)
                        .ToArray();
                }
            );
        }

        private static void InitializeFromResources()
        {
            Debug.Log("[Config] Loading from Resources");

            // Load manifest to know which files exist
            var manifestAsset = Resources.Load<TextAsset>("config/manifest");
            if (manifestAsset == null)
            {
                Debug.LogError("[Config] Resources/config/manifest.txt not found! Run 'Match3 > Sync Config to Resources' before building.");
                return;
            }

            var manifestLines = manifestAsset.text
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToArray();

            Debug.Log($"[Config] Manifest: {manifestLines.Length} files");

            _instance = new FileConfigProvider(
                "config",
                readFile: path =>
                {
                    // path is like "config/game/match3.json"
                    // Resources.Load needs "config/game/match3.json" without extension
                    var resourcePath = path.Replace('\\', '/');
                    if (resourcePath.EndsWith(".json"))
                        resourcePath = resourcePath + ".txt";

                    // Remove .txt extension for Resources.Load
                    var loadPath = resourcePath;
                    if (loadPath.EndsWith(".txt"))
                        loadPath = loadPath.Substring(0, loadPath.Length - 4);

                    var asset = Resources.Load<TextAsset>(loadPath);
                    if (asset == null)
                        throw new FileNotFoundException($"Config resource not found: {loadPath}");

                    return asset.text;
                },
                listFiles: path =>
                {
                    // path is like "config/levels"
                    var prefix = path.Replace('\\', '/');
                    if (!prefix.EndsWith("/")) prefix += "/";

                    return manifestLines
                        .Where(f => f.StartsWith(prefix.Substring("config/".Length)))
                        .Where(f =>
                        {
                            // Only direct children, not nested
                            var rest = f.Substring(prefix.Substring("config/".Length).Length);
                            return !rest.Contains("/");
                        })
                        .Select(f => f.Substring(f.LastIndexOf('/') + 1))
                        .ToArray();
                }
            );
        }
    }
}
