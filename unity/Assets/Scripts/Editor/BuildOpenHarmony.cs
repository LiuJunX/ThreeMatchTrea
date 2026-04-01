using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Match3.Unity.Editor
{
    public static class BuildOpenHarmony
    {
        [MenuItem("Match3/Check OpenHarmony Config")]
        public static void CheckConfig()
        {
            Debug.Log("[OH-Config] === OpenHarmony Preferences ===");

            // Brute-force check common key patterns
            var keys = new[]
            {
                "OpenHarmonySdkRoot", "OpenHarmonyNdkRoot", "OpenHarmonyNdkRootR21",
                "OpenHarmonyNodePath", "OpenHarmonyNodeJsPath", "NodejsPath",
                "OpenHarmonySdk", "OpenHarmonyHome", "OhSdkRoot", "OhSdkHome",
                "OH_SDK_HOME", "SdkRoot", "NdkRoot",
                "OpenHarmonyJdkPath", "OpenHarmonyJdkRoot", "JdkPath",
                "OpenHarmonyGradlePath", "OpenHarmonyHvigorPath",
                "OhNodePath", "OhNodeJsPath", "NodeJsPath"
            };

            foreach (var key in keys)
            {
                var val = EditorPrefs.GetString(key, "");
                if (!string.IsNullOrEmpty(val))
                    Debug.Log($"[OH-Config] EditorPrefs[{key}] = {val}");
            }

            // Use reflection to find ALL OpenHarmony-related static properties/methods
            var types = new[] {
                typeof(EditorUserBuildSettings),
                typeof(PlayerSettings)
            };

            foreach (var t in types)
            {
                // Properties
                foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic))
                {
                    var name = p.Name.ToLower();
                    if (name.Contains("openharmony") || name.Contains("node") || name.Contains("sdk") || name.Contains("jdk") || name.Contains("ndk"))
                    {
                        try
                        {
                            var val = p.GetValue(null);
                            Debug.Log($"[OH-Config] {t.Name}.{p.Name} = {val}");
                        }
                        catch { }
                    }
                }
            }

            // Also check the OpenHarmony editor extensions type
            try
            {
                var asm = System.Reflection.Assembly.Load("UnityEditor.OpenHarmony.Extensions");
                foreach (var type in asm.GetExportedTypes())
                {
                    foreach (var p in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                    {
                        var name = p.Name.ToLower();
                        if (name.Contains("sdk") || name.Contains("node") || name.Contains("jdk") || name.Contains("path") || name.Contains("root"))
                        {
                            try
                            {
                                var val = p.GetValue(null);
                                Debug.Log($"[OH-Config] {type.Name}.{p.Name} = {val}");
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[OH-Config] Extensions assembly: {e.Message}");
            }

            Debug.Log($"[OH-Config] ActiveBuildTarget = {EditorUserBuildSettings.activeBuildTarget}");
            Debug.Log("[OH-Config] === End ===");
        }

        [MenuItem("Match3/Setup OpenHarmony SDK")]
        public static void SetupSdk()
        {
            var sdkPath = "C:/Users/liujun/AppData/Local/OpenHarmony/Sdk/20";
            var nodePath = "C:/Program Files/Huawei/DevEco Studio/tools/node";

            // Set via EditorPrefs
            EditorPrefs.SetString("OpenHarmonySdkRoot", sdkPath);
            EditorPrefs.SetString("OpenHarmonyNdkRoot", sdkPath + "/native");

            // Set via OpenHarmonyExternalToolsSettings reflection
            try
            {
                var asm = System.Reflection.Assembly.Load("UnityEditor.OpenHarmony.Extensions");
                var settingsType = asm.GetType("UnityEditor.OpenHarmony.OpenHarmonyExternalToolsSettings");
                if (settingsType != null)
                {
                    var sdkProp = settingsType.GetProperty("sdkRootPath",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var nodeProp = settingsType.GetProperty("nodejsRootPath",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    if (sdkProp != null && sdkProp.CanWrite)
                    {
                        sdkProp.SetValue(null, sdkPath);
                        Debug.Log($"[OH-Config] Set sdkRootPath = {sdkPath}");
                    }
                    else
                        Debug.Log($"[OH-Config] sdkRootPath is {(sdkProp == null ? "null" : "read-only")}");

                    if (nodeProp != null && nodeProp.CanWrite)
                    {
                        nodeProp.SetValue(null, nodePath);
                        Debug.Log($"[OH-Config] Set nodejsRootPath = {nodePath}");
                    }
                    else
                        Debug.Log($"[OH-Config] nodejsRootPath is {(nodeProp == null ? "null" : "read-only")}");

                    // List all properties for debugging
                    foreach (var p in settingsType.GetProperties(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                    {
                        try
                        {
                            Debug.Log($"[OH-Config] {settingsType.Name}.{p.Name} (CanWrite={p.CanWrite}) = {p.GetValue(null)}");
                        }
                        catch { }
                    }
                }
                else
                    Debug.LogError("[OH-Config] OpenHarmonyExternalToolsSettings type not found");
            }
            catch (Exception e)
            {
                Debug.LogError($"[OH-Config] Reflection error: {e.Message}");
            }

            Debug.Log("[OH-Config] Setup complete.");
        }

        [MenuItem("Match3/Ensure Shaders Included")]
        public static void EnsureShadersIncluded()
        {
            var shaderNames = new[]
            {
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Particles/Unlit",
                "Unlit/Transparent",
                "Unlit/Color",
                "Sprites/Default",
                "Particles/Standard Unlit",
            };

            var dir = "Assets/Resources/Shaders";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Shaders");
            }

            int created = 0;
            foreach (var name in shaderNames)
            {
                var shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning($"[Shaders] Not found: {name}");
                    continue;
                }

                var safeName = name.Replace("/", "_").Replace(" ", "_");
                var path = $"{dir}/{safeName}.mat";

                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                    continue;

                var mat = new Material(shader) { name = safeName };
                AssetDatabase.CreateAsset(mat, path);
                created++;
                Debug.Log($"[Shaders] Created material: {path}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Shaders] Done. Created {created} materials in Resources/Shaders/");
        }

        [MenuItem("Match3/Build OpenHarmony")]
        public static void Build()
        {
            // Defer to next editor update so the MCP call can return immediately
            EditorApplication.delayCall += BuildInternal;
            Debug.Log("[BuildOpenHarmony] Build scheduled, starting soon...");
        }

        private static void BuildInternal()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var outputDir = Path.Combine(projectRoot, "builds", "OpenHarmony");
            var outputPath = Path.Combine(outputDir, "Match3Explore.hap");

            // Clean output directory
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            var scenes = new[] { "Assets/Scenes/Main.scene" };

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.OpenHarmony,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildOpenHarmony] Building to: {outputPath}");

            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildOpenHarmony] Build succeeded! Output: {outputPath}");
                Debug.Log($"[BuildOpenHarmony] Total size: {report.summary.totalSize / (1024 * 1024):F1} MB");
                Debug.Log($"[BuildOpenHarmony] Total time: {report.summary.totalTime}");
            }
            else
            {
                Debug.LogError($"[BuildOpenHarmony] Build failed: {report.summary.result}");
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Warning)
                            Debug.LogError($"  {msg.content}");
                    }
                }
            }
        }
    }
}
