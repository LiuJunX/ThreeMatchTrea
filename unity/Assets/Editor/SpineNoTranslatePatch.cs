// Editor-only: strips horizontal translation from a specific Spine SkeletonDataAsset
// so the editor preview reflects the change without entering Play mode.
// Target: SelfCleanerItem_SkeletonData. Removes the X component of every
// TranslateTimeline / TranslateXTimeline in every animation.

#if UNITY_EDITOR
using System.Reflection;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class SpineNoTranslatePatch
{
    private const string TargetAssetName = "SelfCleanerItem_SkeletonData";

    static SpineNoTranslatePatch()
    {
        EditorApplication.delayCall += Apply;
    }

    [MenuItem("Match3/Spine/Reapply NoTranslate Patch")]
    private static void Apply()
    {
        var guids = AssetDatabase.FindAssets(TargetAssetName + " t:SkeletonDataAsset");
        if (guids.Length == 0) return;

        var sda = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (sda == null) return;

        sda.Clear();
        var data = sda.GetSkeletonData(true);
        if (data == null) return;

        int patched = 0;
        foreach (var anim in data.Animations)
        {
            foreach (var t in anim.Timelines)
            {
                var name = t.GetType().Name;
                var frames = GetFrames(t);
                if (frames == null) continue;

                if (name == "TranslateTimeline")
                {
                    // stride 3: time, x, y
                    for (int i = 1; i < frames.Length; i += 3) frames[i] = 0f;
                    patched++;
                }
                else if (name == "TranslateXTimeline")
                {
                    // stride 2: time, x
                    for (int i = 1; i < frames.Length; i += 2) frames[i] = 0f;
                    patched++;
                }
            }
        }

        // Re-initialize any SkeletonAnimation in the open scene that uses this SDA
        var renderers = Object.FindObjectsByType<SkeletonRenderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r.skeletonDataAsset == sda)
            {
                r.Initialize(true);
                r.LateUpdate();
            }
        }

        SceneView.RepaintAll();
        Debug.Log($"[SpineNoTranslatePatch] Stripped X translation from {patched} timelines on '{TargetAssetName}'.");
    }

    private static float[] GetFrames(Timeline t)
    {
        var ty = t.GetType();
        while (ty != null)
        {
            var f = ty.GetField("frames", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) return f.GetValue(t) as float[];
            ty = ty.BaseType;
        }
        return null;
    }
}
#endif
