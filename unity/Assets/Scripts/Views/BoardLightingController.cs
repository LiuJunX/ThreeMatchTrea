using System.Collections.Generic;
using UnityEngine;

namespace Match3.Unity.Views
{
    /// <summary>
    /// Manages all board lights (key/fill/rim/selection) and automatically
    /// dampens base light intensities when dynamic lights (selection, bombs, etc.) are active.
    /// </summary>
    public sealed class BoardLightingController : MonoBehaviour
    {
        [Header("Adaptive Damping")]
        [SerializeField, Range(0.3f, 0.9f), Tooltip("Minimum damping factor — prevents scene from going too dark")]
        private float _minFloor = 0.55f;

        [SerializeField, Range(0.05f, 1f), Tooltip("How quickly dynamic light intensity causes damping")]
        private float _sensitivity = 0.15f;

        [SerializeField, Range(1f, 20f), Tooltip("Smoothing speed for intensity transitions")]
        private float _transitionSpeed = 5f;

        private Light _keyLight;
        private Light _fillLight;
        private Light _rimLight;
        private Light _selectionLight;
        private Light _hintLight;

        private float _baseKeyIntensity;
        private float _baseKeyShadowStrength;
        private float _baseFillIntensity;

        private float _baseRimIntensity;

        private float _currentKeyIntensity;
        private float _currentFillIntensity;
        private float _currentRimIntensity;

        private bool _baseInitialized;
        private readonly List<Light> _dynamicLights = new();

        public Light SelectionLight => _selectionLight;
        public Light HintLight => _hintLight;

        public void Initialize()
        {
            CreateKeyLight();
            CreateFillLight();
            CreateRimLight();
            CreateSelectionLight();
            CreateHintLight();
        }

        public void SetBaseIntensities(float keyIntensity, float keyShadowStrength, float fillIntensity)
        {
            _baseKeyIntensity = keyIntensity;
            _baseKeyShadowStrength = keyShadowStrength;
            _baseFillIntensity = fillIntensity;

            if (_keyLight != null)
                _keyLight.shadowStrength = keyShadowStrength;

            // Only snap on first call (startup); subsequent calls let Update() smooth-transition
            if (!_baseInitialized)
            {
                _baseInitialized = true;
                _currentKeyIntensity = keyIntensity;
                _currentFillIntensity = fillIntensity;
                if (_keyLight != null)
                    _keyLight.intensity = keyIntensity;
                if (_fillLight != null)
                    _fillLight.intensity = fillIntensity;
            }
        }

        public void RegisterDynamicLight(Light light)
        {
            if (light != null && !_dynamicLights.Contains(light))
                _dynamicLights.Add(light);
        }

        public void UnregisterDynamicLight(Light light)
        {
            _dynamicLights.Remove(light);
        }

        private void Update()
        {
            float totalDynamic = 0f;
            for (int i = _dynamicLights.Count - 1; i >= 0; i--)
            {
                var light = _dynamicLights[i];
                if (light == null)
                {
                    _dynamicLights.RemoveAt(i);
                    continue;
                }
                if (light.enabled)
                    totalDynamic += light.intensity;
            }

            // damping = minFloor + (1 - minFloor) / (1 + totalDynamic * sensitivity)
            float damping = _minFloor + (1f - _minFloor) / (1f + totalDynamic * _sensitivity);

            float targetKey = _baseKeyIntensity * damping;
            float targetFill = _baseFillIntensity * damping;
            float targetRim = _baseRimIntensity * damping;

            // Exponential smoothing: lerp factor = 1 - e^(-speed * dt)
            float t = 1f - Mathf.Exp(-_transitionSpeed * Time.deltaTime);
            _currentKeyIntensity = Mathf.Lerp(_currentKeyIntensity, targetKey, t);
            _currentFillIntensity = Mathf.Lerp(_currentFillIntensity, targetFill, t);
            _currentRimIntensity = Mathf.Lerp(_currentRimIntensity, targetRim, t);

            if (_keyLight != null)
                _keyLight.intensity = _currentKeyIntensity;
            if (_fillLight != null)
                _fillLight.intensity = _currentFillIntensity;
            if (_rimLight != null && _rimLight.enabled)
                _rimLight.intensity = _currentRimIntensity;
        }

        private void CreateKeyLight()
        {
            var go = new GameObject("BoardLight_Key");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-0.03f, 1.02f, 0.41f);
            go.transform.rotation = Quaternion.Euler(7.5f, 7.5f, 12.629f);
            _keyLight = go.AddComponent<Light>();
            _keyLight.type = LightType.Directional;
            _keyLight.color = new Color(0.92f, 0.92f, 0.92f);
            _keyLight.intensity = 1.0f;
            _keyLight.shadows = LightShadows.Soft;
            _keyLight.shadowStrength = 0.8f;
            RenderSettings.sun = _keyLight;

            _baseKeyIntensity = _keyLight.intensity;
            _baseKeyShadowStrength = _keyLight.shadowStrength;
            _currentKeyIntensity = _baseKeyIntensity;
        }

        private void CreateFillLight()
        {
            var go = new GameObject("BoardLight_Fill");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(322.86f, 336.94f, 350.83f);
            _fillLight = go.AddComponent<Light>();
            _fillLight.type = LightType.Directional;
            _fillLight.color = new Color(0.95f, 0.93f, 0.90f);
            _fillLight.intensity = 0.18f;
            _fillLight.shadows = LightShadows.None;
            _fillLight.renderMode = LightRenderMode.ForceVertex;

            _baseFillIntensity = _fillLight.intensity;
            _currentFillIntensity = _baseFillIntensity;
        }

        private void CreateRimLight()
        {
            var go = new GameObject("BoardLight_Rim");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(340.89f, 209.44f, 4.03f);
            _rimLight = go.AddComponent<Light>();
            _rimLight.type = LightType.Directional;
            _rimLight.color = new Color(1.0f, 0.95f, 0.88f);
            _rimLight.intensity = 0.0f;
            _rimLight.shadows = LightShadows.None;
            _rimLight.renderMode = LightRenderMode.ForceVertex;
            _rimLight.enabled = false;

            _baseRimIntensity = _rimLight.intensity;
            _currentRimIntensity = _baseRimIntensity;
        }

        private void CreateSelectionLight()
        {
            var go = new GameObject("SelectionLight");
            go.transform.SetParent(transform, false);
            _selectionLight = go.AddComponent<Light>();
            _selectionLight.type = LightType.Point;
            _selectionLight.range = 1.5f;
            _selectionLight.intensity = 4f;
            _selectionLight.shadows = LightShadows.None;
            _selectionLight.renderMode = LightRenderMode.ForcePixel;
            _selectionLight.enabled = false;

            RegisterDynamicLight(_selectionLight);
        }

        private void CreateHintLight()
        {
            var go = new GameObject("HintLight");
            go.transform.SetParent(transform, false);
            _hintLight = go.AddComponent<Light>();
            _hintLight.type = LightType.Point;
            _hintLight.range = 1.5f;
            _hintLight.intensity = 3f;
            _hintLight.shadows = LightShadows.None;
            _hintLight.renderMode = LightRenderMode.ForcePixel;
            _hintLight.enabled = false;

            RegisterDynamicLight(_hintLight);
        }

        private void OnDestroy()
        {
            _dynamicLights.Clear();

            if (_keyLight != null)
            {
                Destroy(_keyLight.gameObject);
                _keyLight = null;
            }
            if (_fillLight != null)
            {
                Destroy(_fillLight.gameObject);
                _fillLight = null;
            }
            if (_rimLight != null)
            {
                Destroy(_rimLight.gameObject);
                _rimLight = null;
            }
            if (_selectionLight != null)
            {
                Destroy(_selectionLight.gameObject);
                _selectionLight = null;
            }
            if (_hintLight != null)
            {
                Destroy(_hintLight.gameObject);
                _hintLight = null;
            }
        }
    }
}
