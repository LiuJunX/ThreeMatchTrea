using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Match3.Unity.Editor
{
    /// <summary>
    /// Copies config/ to Resources/config before build.
    /// Also generates a manifest for runtime file listing.
    /// </summary>
    public class BuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            SyncConfigToResources();
        }

        [MenuItem("Match3/Sync Config to Resources")]
        public static void SyncConfigToResources()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var sourceConfig = Path.Combine(projectRoot, "config");
            var targetConfig = Path.Combine(Application.dataPath, "Resources", "config");

            if (!Directory.Exists(sourceConfig))
            {
                Debug.LogError($"[BuildPreprocessor] Config source not found: {sourceConfig}");
                return;
            }

            // Ensure Resources/config exists
            Directory.CreateDirectory(targetConfig);

            // Remove old config
            if (Directory.Exists(targetConfig))
            {
                Directory.Delete(targetConfig, recursive: true);
            }

            // Copy recursively, renaming .json to .json.txt for Resources.Load<TextAsset>
            var manifest = new List<string>();
            CopyDirectory(sourceConfig, targetConfig, sourceConfig, manifest);

            // Write manifest file (lists all config paths relative to config/)
            var manifestPath = Path.Combine(targetConfig, "manifest.txt");
            File.WriteAllText(manifestPath, string.Join("\n", manifest));

            AssetDatabase.Refresh();
            Debug.Log($"[BuildPreprocessor] Config synced to Resources ({manifest.Count} files)");
        }

        [MenuItem("Match3/Sync Config to StreamingAssets")]
        public static void CopyConfigToStreamingAssets()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var sourceConfig = Path.Combine(projectRoot, "config");
            var targetConfig = Path.Combine(Application.streamingAssetsPath, "config");

            if (!Directory.Exists(sourceConfig))
            {
                Debug.LogError($"[BuildPreprocessor] Config source not found: {sourceConfig}");
                return;
            }

            if (!Directory.Exists(Application.streamingAssetsPath))
                Directory.CreateDirectory(Application.streamingAssetsPath);

            if (Directory.Exists(targetConfig))
                Directory.Delete(targetConfig, recursive: true);

            CopyDirectoryRaw(sourceConfig, targetConfig);

            AssetDatabase.Refresh();
            Debug.Log($"[BuildPreprocessor] Config copied to StreamingAssets ({CountFiles(targetConfig)} files)");
        }

        private static void CopyDirectory(string source, string target, string rootSource, List<string> manifest)
        {
            Directory.CreateDirectory(target);

            foreach (var file in Directory.GetFiles(source))
            {
                var fileName = Path.GetFileName(file);

                // Skip schema files in builds
                if (fileName.EndsWith(".schema.json"))
                    continue;

                var relativePath = file.Substring(rootSource.Length + 1).Replace('\\', '/');

                if (fileName.EndsWith(".json"))
                {
                    // Rename .json to .json.txt so Unity treats it as TextAsset
                    var targetFile = Path.Combine(target, fileName + ".txt");
                    File.Copy(file, targetFile, overwrite: true);
                    manifest.Add(relativePath);
                }
                else
                {
                    File.Copy(file, Path.Combine(target, fileName), overwrite: true);
                }
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                var dirName = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(target, dirName), rootSource, manifest);
            }
        }

        private static void CopyDirectoryRaw(string source, string target)
        {
            Directory.CreateDirectory(target);

            foreach (var file in Directory.GetFiles(source))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(target, fileName), overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                var dirName = Path.GetFileName(dir);
                CopyDirectoryRaw(dir, Path.Combine(target, dirName));
            }
        }

        private static int CountFiles(string path)
        {
            if (!Directory.Exists(path)) return 0;
            int count = Directory.GetFiles(path).Length;
            foreach (var dir in Directory.GetDirectories(path))
            {
                count += CountFiles(dir);
            }
            return count;
        }
    }
}
