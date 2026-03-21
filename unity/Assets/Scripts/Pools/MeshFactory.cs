using System.Collections.Generic;
using Match3.Core.Models.Enums;
using Match3.Unity.Services;
using Match3.Unity.Views;
using UnityEngine;

namespace Match3.Unity.Pools
{
    /// <summary>
    /// Runtime mesh and material generation for 3D tiles.
    /// Reuses SpriteFactory color definitions. Loads mesh via ResourceService.
    /// </summary>
    public static class MeshFactory
    {
        private static readonly Dictionary<ElementType, Mesh> _tileMeshCache = new();
        private static Mesh _fallbackMesh;
        private static readonly Dictionary<ElementType, Material> _materialCache = new();
        private static readonly Dictionary<ElementType, Material[]> _singleMaterialArrayCache = new();
        private static readonly Dictionary<ElementType, Material[]> _collectibleMaterialCache = new();
        private static readonly Dictionary<ElementType, Mesh> _bombMeshCache = new();
        private static readonly Dictionary<ElementType, Material[]> _bombMaterialCache = new();
        private static readonly Dictionary<ObstacleType, Mesh> _obstacleMeshCache = new();
        private static readonly Dictionary<ObstacleType, Material[]> _obstacleMaterialCache = new();
        private static readonly Dictionary<GroundType, Mesh> _groundMeshCache = new();
        private static readonly Dictionary<GroundType, Material[]> _groundMaterialCache = new();
        private static Shader _litShader;
        private static Shader _tileLitShader;

        private static RenderTuningSettings _tuning;

        // Blob shadow resources
        private static Mesh _blobShadowMesh;
        private static Material _blobShadowMaterial;
        private static Texture2D _blobShadowTexture;

        /// <summary>
        /// Get the mesh for a specific tile type.
        /// Each color type has its own distinct shape.
        /// </summary>
        public static Mesh GetTileMesh(ElementType type)
        {
            if (_tileMeshCache.TryGetValue(type, out var cached))
                return cached;

            // Collectibles use their own model name (no Gem_ prefix)
            var collectibleName = GetCollectibleModelName(type);
            if (collectibleName != null)
                return LoadTileModel(type, collectibleName);

            var typeName = GetTileTypeName(type);
            return LoadTileModel(type, $"Gem_{typeName}");
        }

        private static Mesh LoadTileModel(ElementType type, string modelName)
        {
            var model = ResourceService.Loader.Load<GameObject>($"Art/Gems/Models/{modelName}");
            if (model != null)
            {
                var meshFilter = model.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null)
                {
                    _tileMeshCache[type] = meshFilter.sharedMesh;

                    // Cache FBX-embedded materials for collectibles (clone with TileLit for clip support)
                    if (type.IsCollectible())
                    {
                        var renderer = model.GetComponentInChildren<MeshRenderer>();
                        if (renderer != null && renderer.sharedMaterials.Length > 0)
                            _collectibleMaterialCache[type] = CloneMaterialsWithTileLit(renderer.sharedMaterials);
                    }

                    Debug.Log($"[MeshFactory] Loaded {modelName} mesh from Resources");
                    return meshFilter.sharedMesh;
                }
            }

            Debug.LogWarning($"[MeshFactory] {modelName} not found, using fallback sphere");
            return GetFallbackMesh();
        }

        /// <summary>
        /// Get a built-in sphere mesh (alias for fallback mesh).
        /// </summary>
        public static Mesh GetSphereMesh() => GetFallbackMesh();

        /// <summary>
        /// Get the shared fallback mesh (built-in sphere).
        /// </summary>
        public static Mesh GetFallbackMesh()
        {
            if (_fallbackMesh != null) return _fallbackMesh;

            var tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _fallbackMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tempSphere);
            return _fallbackMesh;
        }

        /// <summary>
        /// Get the mesh for a bomb type.
        /// Loads from Resources, falls back to tile mesh.
        /// </summary>
        public static Mesh GetBombMesh(ElementType type)
        {
            if (type == ElementType.None) return GetFallbackMesh();

            if (_bombMeshCache.TryGetValue(type, out var cached))
                return cached;

            LoadBombModel(type);
            return _bombMeshCache.TryGetValue(type, out cached) ? cached : GetFallbackMesh();
        }

        /// <summary>
        /// Get the cached materials array for a bomb type.
        /// Returns null if the bomb model has no embedded materials.
        /// </summary>
        public static Material[] GetBombMaterials(ElementType type)
        {
            if (type == ElementType.None) return null;

            if (!_bombMaterialCache.ContainsKey(type))
                LoadBombModel(type);

            return _bombMaterialCache.TryGetValue(type, out var mats) ? mats : null;
        }

        private static void LoadBombModel(ElementType type)
        {
            if (_bombMeshCache.ContainsKey(type)) return;

            var typeName = type switch
            {
                ElementType.HorizontalRocket => "Horizontal",
                ElementType.VerticalRocket => "Vertical",
                ElementType.ColorBomb => "Color",
                _ => type.ToString()
            };
            var model = ResourceService.Loader.Load<GameObject>($"Art/Gems/Models/Bomb_{typeName}");
            if (model != null)
            {
                var meshFilter = model.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null)
                {
                    _bombMeshCache[type] = meshFilter.sharedMesh;

                    // Cache embedded materials from the FBX model (clone with TileLit for clip support)
                    var renderer = model.GetComponentInChildren<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterials.Length > 0)
                        _bombMaterialCache[type] = CloneMaterialsWithTileLit(renderer.sharedMaterials);

                    Debug.Log($"[MeshFactory] Loaded Bomb_{typeName} mesh from Resources ({renderer?.sharedMaterials.Length ?? 0} materials)");
                    return;
                }
            }

            Debug.LogWarning($"[MeshFactory] Bomb_{typeName} mesh not found, using fallback sphere");
            _bombMeshCache[type] = GetFallbackMesh();
        }

        /// <summary>
        /// Get the mesh for an obstacle type.
        /// Loads from Resources/Art/Gems/Models/Box_State3 etc., falls back to fallback mesh.
        /// </summary>
        public static Mesh GetObstacleMesh(ObstacleType type)
        {
            if (type == ObstacleType.None) return GetFallbackMesh();

            if (_obstacleMeshCache.TryGetValue(type, out var cached))
                return cached;

            LoadObstacleModel(type);
            return _obstacleMeshCache.TryGetValue(type, out cached) ? cached : GetFallbackMesh();
        }

        /// <summary>
        /// Get the cached materials array for an obstacle type.
        /// Returns null if the model has no embedded materials.
        /// </summary>
        public static Material[] GetObstacleMaterials(ObstacleType type)
        {
            if (type == ObstacleType.None) return null;

            if (!_obstacleMaterialCache.ContainsKey(type))
                LoadObstacleModel(type);

            return _obstacleMaterialCache.TryGetValue(type, out var mats) ? mats : null;
        }

        private static void LoadObstacleModel(ObstacleType type)
        {
            if (_obstacleMeshCache.ContainsKey(type)) return;

            var typeName = type switch
            {
                ObstacleType.Box => "Box_State3",
                ObstacleType.Bush => "Bush",
                ObstacleType.Safe => "Safe",
                ObstacleType.Cupboard => "Cupboard",
                ObstacleType.Owl => "Owl",
                ObstacleType.Stone => "Stone",
                _ => type.ToString()
            };
            var model = ResourceService.Loader.Load<GameObject>($"Art/Gems/Models/{typeName}");
            if (model != null)
            {
                var meshFilter = model.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null)
                {
                    _obstacleMeshCache[type] = meshFilter.sharedMesh;

                    var renderer = model.GetComponentInChildren<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterials.Length > 0)
                        _obstacleMaterialCache[type] = CloneMaterialsWithTileLit(renderer.sharedMaterials);

                    Debug.Log($"[MeshFactory] Loaded {typeName} mesh from Resources ({renderer?.sharedMaterials.Length ?? 0} materials)");
                    return;
                }
            }

            Debug.LogWarning($"[MeshFactory] {typeName} mesh not found, using fallback");
            _obstacleMeshCache[type] = GetFallbackMesh();
        }

        /// <summary>
        /// Get the mesh for a ground type.
        /// Loads from Resources/Art/Gems/Models/Grass etc., falls back to fallback mesh.
        /// </summary>
        public static Mesh GetGroundMesh(GroundType type)
        {
            if (type == GroundType.None) return GetFallbackMesh();

            if (_groundMeshCache.TryGetValue(type, out var cached))
                return cached;

            LoadGroundModel(type);
            return _groundMeshCache.TryGetValue(type, out cached) ? cached : GetFallbackMesh();
        }

        /// <summary>
        /// Get the cached materials array for a ground type.
        /// Returns null if the model has no embedded materials.
        /// </summary>
        public static Material[] GetGroundMaterials(GroundType type)
        {
            if (type == GroundType.None) return null;

            if (!_groundMaterialCache.ContainsKey(type))
                LoadGroundModel(type);

            return _groundMaterialCache.TryGetValue(type, out var mats) ? mats : null;
        }

        private static void LoadGroundModel(GroundType type)
        {
            if (_groundMeshCache.ContainsKey(type)) return;

            var typeName = type switch
            {
                GroundType.Grass => "Grass",
                _ => type.ToString()
            };
            var model = ResourceService.Loader.Load<GameObject>($"Art/Gems/Models/{typeName}");
            if (model != null)
            {
                var meshFilter = model.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null)
                {
                    _groundMeshCache[type] = meshFilter.sharedMesh;

                    var renderer = model.GetComponentInChildren<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterials.Length > 0)
                        _groundMaterialCache[type] = CloneMaterialsWithTileLit(renderer.sharedMaterials);

                    Debug.Log($"[MeshFactory] Loaded {typeName} mesh from Resources ({renderer?.sharedMaterials.Length ?? 0} materials)");
                    return;
                }
            }

            Debug.LogWarning($"[MeshFactory] {typeName} mesh not found, using fallback");
            _groundMeshCache[type] = GetFallbackMesh();
        }

        /// <summary>
        /// Get a cached ceramic material for the given tile type.
        /// Uses confirmed color palette with colored specular highlights.
        /// </summary>
        public static Material GetTileMaterial(ElementType type)
        {
            if (_materialCache.TryGetValue(type, out var cached))
                return cached;

            var color = GetCeramicColor(type);
            var mat = CreateCeramicMaterial(color);
            ApplyRenderTuningToMaterial(mat, _tuning);
            mat.name = $"Tile3D_{GetTileTypeName(type)}";
            _materialCache[type] = mat;
            return mat;
        }

        /// <summary>
        /// Confirmed ceramic color palette (HSV-derived, boosted for 3D).
        /// Intentionally decoupled from SpriteFactory/VisualConfig — these are
        /// art-directed values with higher saturation to compensate for 3D lighting.
        /// Source of truth: Match3Art/docs/art-direction.md
        /// </summary>
        private static Color GetCeramicColor(ElementType type)
        {
            if (type == ElementType.Item1) return new Color(0.900f, 0.092f, 0.018f); // Red
            if (type == ElementType.Item3) return new Color(0.070f, 0.367f, 0.880f); // Blue
            if (type == ElementType.Item2) return new Color(0.041f, 0.820f, 0.301f); // Green
            if (type == ElementType.Item4) return new Color(0.950f, 0.724f, 0.048f); // Yellow
            if (type == ElementType.Item5) return new Color(0.482f, 0.140f, 0.780f); // Purple
            if (type == ElementType.Item6) return new Color(0.950f, 0.464f, 0.038f); // Orange
            return Color.gray;
        }

        /// <summary>
        /// Get a cached single-element material array for the given tile type.
        /// Avoids per-frame allocation when setting sharedMaterials.
        /// </summary>
        public static Material[] GetTileMaterialArray(ElementType type)
        {
            // Bombs use their own multi-material array from FBX model
            if (type.IsBomb())
            {
                var bombMats = GetBombMaterials(type);
                if (bombMats != null && bombMats.Length > 0)
                    return bombMats;
            }

            // Collectibles use FBX-embedded materials (cached during LoadTileModel)
            if (type.IsCollectible())
            {
                if (!_collectibleMaterialCache.ContainsKey(type))
                    GetTileMesh(type); // trigger load + material cache
                if (_collectibleMaterialCache.TryGetValue(type, out var colMats) && colMats.Length > 0)
                    return colMats;
            }

            // Cached single-element array for tile materials
            if (_singleMaterialArrayCache.TryGetValue(type, out var cached))
                return cached;

            var mat = GetTileMaterial(type);
            if (mat == null) mat = GetFallbackMaterial();
            var arr = new[] { mat };
            _singleMaterialArrayCache[type] = arr;
            return arr;
        }

        /// <summary>
        /// Get fallback material for unsupported types (Rainbow, bombs).
        /// </summary>
        public static Material GetFallbackMaterial()
        {
            return GetOrCreateFallbackMaterial();
        }

        /// <summary>
        /// Apply runtime render tuning (material + shadow texture) to cached resources.
        /// Safe to call repeatedly; call only when settings change.
        /// </summary>
        public static void ApplyRenderTuning(RenderTuningSettings settings)
        {
            _tuning = settings;

            foreach (var mat in _materialCache.Values)
                ApplyRenderTuningToMaterial(mat, settings);

            if (_fallbackMaterial != null)
                ApplyRenderTuningToMaterial(_fallbackMaterial, settings);
        }

        /// <summary>
        /// Get the ceramic base color for a tile type.
        /// </summary>
        public static Color GetTileColor(ElementType type)
        {
            return GetCeramicColor(type);
        }

        /// <summary>
        /// Get the shared blob shadow quad mesh.
        /// </summary>
        public static Mesh GetBlobShadowMesh()
        {
            if (_blobShadowMesh != null) return _blobShadowMesh;

            _blobShadowMesh = new Mesh { name = "BlobShadow" };
            _blobShadowMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0)
            };
            _blobShadowMesh.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            };
            _blobShadowMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _blobShadowMesh.RecalculateNormals();
            return _blobShadowMesh;
        }

        /// <summary>
        /// Get the shared blob shadow material (transparent URP Unlit + soft circle texture).
        /// </summary>
        public static Material GetBlobShadowMaterial()
        {
            if (_blobShadowMaterial != null) return _blobShadowMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Transparent");

            _blobShadowMaterial = new Material(shader) { name = "BlobShadow" };

            // Transparent surface
            if (_blobShadowMaterial.HasProperty("_Surface"))
                _blobShadowMaterial.SetFloat("_Surface", 1f);
            if (_blobShadowMaterial.HasProperty("_Blend"))
                _blobShadowMaterial.SetFloat("_Blend", 0f);
            if (_blobShadowMaterial.HasProperty("_SrcBlend"))
                _blobShadowMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (_blobShadowMaterial.HasProperty("_DstBlend"))
                _blobShadowMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (_blobShadowMaterial.HasProperty("_ZWrite"))
                _blobShadowMaterial.SetFloat("_ZWrite", 0f);

            _blobShadowMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _blobShadowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            // White base color (texture alpha drives transparency)
            if (_blobShadowMaterial.HasProperty("_BaseColor"))
                _blobShadowMaterial.SetColor("_BaseColor", Color.white);
            else if (_blobShadowMaterial.HasProperty("_Color"))
                _blobShadowMaterial.SetColor("_Color", Color.white);

            _blobShadowMaterial.mainTexture = GetOrCreateBlobShadowTexture();
            return _blobShadowMaterial;
        }

        /// <summary>
        /// Generate a soft circular gradient texture for blob shadow.
        /// Dark center fading to transparent at edges.
        /// </summary>
        private static Texture2D GetOrCreateBlobShadowTexture()
        {
            if (_blobShadowTexture != null) return _blobShadowTexture;

            // Slightly higher resolution makes edges noticeably softer on screen.
            const int size = 256;
            _blobShadowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BlobShadowTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float center = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / center;

                    // Exponential falloff: darker near contact, very soft edge.
                    // dist=0 => 1, dist=1 => ~0.018
                    float alpha = Mathf.Exp(-dist * dist * 4.0f);
                    alpha = Mathf.Clamp01(alpha);

                    _blobShadowTexture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            _blobShadowTexture.Apply();
            return _blobShadowTexture;
        }

        /// <summary>
        /// Clear all cached materials and mesh reference.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var mat in _materialCache.Values)
            {
                if (mat != null)
                    Object.Destroy(mat);
            }
            _materialCache.Clear();

            if (_fallbackMaterial != null)
            {
                Object.Destroy(_fallbackMaterial);
                _fallbackMaterial = null;
            }

            // Note: Do NOT destroy _blobShadowMaterial, _blobShadowTexture, _blobShadowMesh here.
            // Pooled Tile3DView objects hold references to these materials created in Awake().
            // Destroying them turns shadows magenta on pool re-use.

            _bombMeshCache.Clear();

            // Destroy cloned collectible materials
            foreach (var mats in _collectibleMaterialCache.Values)
            {
                if (mats == null) continue;
                foreach (var mat in mats)
                {
                    if (mat != null)
                        Object.Destroy(mat);
                }
            }
            _collectibleMaterialCache.Clear();

            // Destroy cloned bomb materials (created by CloneMaterialsWithTileLit)
            foreach (var mats in _bombMaterialCache.Values)
            {
                if (mats == null) continue;
                foreach (var mat in mats)
                {
                    if (mat != null)
                        Object.Destroy(mat);
                }
            }
            _bombMaterialCache.Clear();

            _obstacleMeshCache.Clear();
            foreach (var mats in _obstacleMaterialCache.Values)
            {
                if (mats == null) continue;
                foreach (var mat in mats)
                {
                    if (mat != null)
                        Object.Destroy(mat);
                }
            }
            _obstacleMaterialCache.Clear();

            _groundMeshCache.Clear();
            foreach (var mats in _groundMaterialCache.Values)
            {
                if (mats == null) continue;
                foreach (var mat in mats)
                {
                    if (mat != null)
                        Object.Destroy(mat);
                }
            }
            _groundMaterialCache.Clear();

            _singleMaterialArrayCache.Clear();
            _tileMeshCache.Clear();
            _fallbackMesh = null;
            _litShader = null;
            _tileLitShader = null;
        }

        private static Shader GetTileLitShader()
        {
            if (_tileLitShader != null) return _tileLitShader;
            _tileLitShader = Shader.Find("Match3/TileLit");
            return _tileLitShader;
        }

        private static Shader GetLitShader()
        {
            if (_litShader != null) return _litShader;

            _litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_litShader == null)
                _litShader = Shader.Find("Standard");

            return _litShader;
        }

        /// <summary>
        /// Clone FBX materials and swap their shader to Match3/TileLit for clip support.
        /// </summary>
        private static Material[] CloneMaterialsWithTileLit(Material[] sourceMats)
        {
            var tileLit = GetTileLitShader();
            if (tileLit == null) return sourceMats;

            var result = new Material[sourceMats.Length];
            for (int i = 0; i < sourceMats.Length; i++)
            {
                if (sourceMats[i] != null)
                {
                    result[i] = new Material(sourceMats[i]); // clone all properties
                    result[i].shader = tileLit;               // swap to TileLit
                }
            }
            return result;
        }

        /// <summary>
        /// Create ceramic material with glossy PBR highlights.
        /// Uses Match3/TileLit shader (supports Y-axis clipping for hole portals).
        /// Note: ClearCoat (was 0.07 mask) is intentionally dropped — TileLit uses
        /// UniversalFragmentPBR which doesn't support ClearCoat. The visual difference
        /// at 0.07 mask was negligible.
        /// </summary>
        private static Material CreateCeramicMaterial(Color color)
        {
            // Prefer Match3/TileLit (supports Y-axis clipping), fallback to Lit, then Standard
            var shader = GetTileLitShader() ?? GetLitShader();
            var mat = new Material(shader);

            // Base color
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.SetColor("_Color", color);

            // Metallic workflow: low metallic = white specular highlights (candy/ceramic look)
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.05f);

            // Glossy ceramic: high enough for highlights, not so high that
            // flat-shading normal seams show harsh dark lines
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.62f);

            // Emission (default black = no glow).
            // Tile3DView sets emission color via PropertyBlock when selected/hinted.
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", Color.black);

            return mat;
        }

        private static void ApplyRenderTuningToMaterial(Material mat, RenderTuningSettings settings)
        {
            if (mat == null || settings == null) return;

            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", settings.Metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", settings.Smoothness);
        }

        private static Material _fallbackMaterial;

        private static Material GetOrCreateFallbackMaterial()
        {
            if (_fallbackMaterial != null) return _fallbackMaterial;

            _fallbackMaterial = CreateCeramicMaterial(Color.gray);
            _fallbackMaterial.name = "Tile3D_Fallback";
            return _fallbackMaterial;
        }

        private static string GetTileTypeName(ElementType type)
        {
            if (type == ElementType.Item1) return "Red";
            if (type == ElementType.Item2) return "Green";
            if (type == ElementType.Item3) return "Blue";
            if (type == ElementType.Item4) return "Yellow";
            if (type == ElementType.Item5) return "Purple";
            if (type == ElementType.Item6) return "Orange";
            return "Unknown";
        }

        /// <summary>
        /// Returns the model name for collectible tile types, or null for non-collectibles.
        /// </summary>
        private static string GetCollectibleModelName(ElementType type) => type switch
        {
            ElementType.Plate => "Plate",
            ElementType.Pearl => "Pearl",
            ElementType.Bird  => "Bird",
            _ => null
        };
    }
}
