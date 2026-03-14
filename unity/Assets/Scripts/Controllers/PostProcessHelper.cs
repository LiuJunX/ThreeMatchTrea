using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Match3.Unity.Controllers
{
    /// <summary>
    /// Sets up post-processing via reflection to avoid direct URP assembly reference
    /// (which causes ObjectPool name collision with Match3.Unity.Pools.ObjectPool).
    /// </summary>
    public static class PostProcessHelper
    {
        public static void SetupBloom()
        {
            var volumeGo = new GameObject("PostProcessVolume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // Add Bloom via reflection (type lives in Unity.RenderPipelines.Universal.Runtime)
            var bloomType = Type.GetType(
                "UnityEngine.Rendering.Universal.Bloom, Unity.RenderPipelines.Universal.Runtime");
            if (bloomType == null)
            {
                Debug.LogWarning("[PostProcessHelper] Bloom type not found — URP package missing?");
                return;
            }

            // VolumeProfile.Add<T>(bool overrides) — call with overrides=true
            var addMethod = typeof(VolumeProfile)
                .GetMethod("Add", new[] { typeof(bool) });
            if (addMethod == null) return;

            var genericAdd = addMethod.MakeGenericMethod(bloomType);
            var bloom = genericAdd.Invoke(volume.profile, new object[] { true });
            if (bloom == null) return;

            // Set parameters: each is a VolumeParameter<T> with .value property
            SetVolumeParam(bloom, "threshold", 0.9f);
            SetVolumeParam(bloom, "intensity", 0.4f);
            SetVolumeParam(bloom, "scatter", 0.7f);
        }

        private static void SetVolumeParam(object component, string fieldName, float value)
        {
            var field = component.GetType().GetField(fieldName);
            if (field == null) return;

            var param = field.GetValue(component);
            if (param == null) return;

            var valueProp = param.GetType().GetProperty("value");
            valueProp?.SetValue(param, value);
        }
    }
}
