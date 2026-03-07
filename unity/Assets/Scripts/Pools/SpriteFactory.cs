using System.Collections.Generic;
using Match3.Core.Config;
using Match3.Core.Models.Enums;
using Match3.Unity.Services;
using UnityEngine;

namespace Match3.Unity.Pools
{
    /// <summary>
    /// Runtime sprite generation for tiles.
    /// Creates 1x1 pixel textures - no external asset dependencies.
    /// Colors are loaded from configuration.
    /// </summary>
    public static class SpriteFactory
    {
        private static readonly Dictionary<Color, Sprite> _colorCache = new();
        private static readonly Dictionary<BombType, Sprite> _bombOverlayCache = new();

        // Cached colors from config
        private static Dictionary<string, Color> _tileColors;
        private static Dictionary<string, Color> _bombColors;
        private static bool _configLoaded;

        /// <summary>
        /// Ensure configuration is loaded.
        /// </summary>
        private static void EnsureConfigLoaded()
        {
            if (_configLoaded) return;

            try
            {
                var config = UnityConfigProvider.Instance.GetVisualConfig();
                _tileColors = new Dictionary<string, Color>();
                _bombColors = new Dictionary<string, Color>();

                foreach (var kvp in config.TileColors)
                {
                    _tileColors[kvp.Key] = ConfigColorUtility.ParseHexColor(kvp.Value, Color.gray);
                }

                if (config.BombIndicatorColors != null)
                {
                    foreach (var kvp in config.BombIndicatorColors)
                    {
                        _bombColors[kvp.Key] = ConfigColorUtility.ParseHexColor(kvp.Value, Color.white);
                    }
                }

                _configLoaded = true;
                Debug.Log($"[SpriteFactory] Loaded {_tileColors.Count} tile colors, {_bombColors.Count} bomb colors from config");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SpriteFactory] Failed to load config, using defaults: {ex.Message}");
                _tileColors = new Dictionary<string, Color>();
                _bombColors = new Dictionary<string, Color>();
                _configLoaded = true;
            }
        }

        /// <summary>
        /// Get or create a sprite for the given color.
        /// </summary>
        public static Sprite GetColorSprite(Color color)
        {
            if (_colorCache.TryGetValue(color, out var cached))
                return cached;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixel(0, 0, color);
            tex.Apply();

            // pixelsPerUnit = 1 means the sprite is 1 unit in world space
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = $"Color_{color}";
            _colorCache[color] = sprite;

            return sprite;
        }

        /// <summary>
        /// Get color for a tile type.
        /// </summary>
        public static Color GetTileColor(ElementType type)
        {
            EnsureConfigLoaded();

            // Try to get from config first
            string typeName = GetTileTypeName(type);
            if (typeName != null && _tileColors.TryGetValue(typeName, out var configColor))
            {
                return configColor;
            }

            // Fallback to hardcoded defaults
            if (type == ElementType.Item1) return new Color(0.9f, 0.2f, 0.2f);       // Red
            if (type == ElementType.Item2) return new Color(0.2f, 0.8f, 0.3f);       // Green
            if (type == ElementType.Item3) return new Color(0.2f, 0.4f, 0.9f);       // Blue
            if (type == ElementType.Item4) return new Color(0.95f, 0.85f, 0.2f);     // Yellow
            if (type == ElementType.Item5) return new Color(0.7f, 0.3f, 0.8f);       // Purple
            if (type == ElementType.Item6) return new Color(0.95f, 0.5f, 0.1f);      // Orange
            if (type == ElementType.Universal) return new Color(0.9f, 0.9f, 0.9f);   // Rainbow

            return Color.gray;
        }

        /// <summary>
        /// Get the config key name for an ElementType.
        /// </summary>
        private static string GetTileTypeName(ElementType type)
        {
            if (type == ElementType.Item1) return "Red";
            if (type == ElementType.Item2) return "Green";
            if (type == ElementType.Item3) return "Blue";
            if (type == ElementType.Item4) return "Yellow";
            if (type == ElementType.Item5) return "Purple";
            if (type == ElementType.Item6) return "Orange";
            if (type == ElementType.Universal) return "Rainbow";
            return null;
        }

        /// <summary>
        /// Get sprite for a tile type.
        /// </summary>
        public static Sprite GetTileSprite(ElementType type)
        {
            var color = GetTileColor(type);
            return GetColorSprite(color);
        }

        /// <summary>
        /// Get bomb overlay sprite.
        /// </summary>
        public static Sprite GetBombOverlay(BombType bombType)
        {
            if (bombType == BombType.None)
                return null;

            if (_bombOverlayCache.TryGetValue(bombType, out var cached))
                return cached;

            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            // Clear texture
            var clearPixels = new Color32[64];
            for (int i = 0; i < 64; i++)
                clearPixels[i] = new Color32(0, 0, 0, 0);
            tex.SetPixels32(clearPixels);

            // Draw bomb indicator based on type
            var indicatorColor = GetBombIndicatorColor(bombType);
            DrawBombPattern(tex, bombType, indicatorColor);

            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            sprite.name = $"Bomb_{bombType}";
            _bombOverlayCache[bombType] = sprite;

            return sprite;
        }

        private static Color GetBombIndicatorColor(BombType bombType)
        {
            EnsureConfigLoaded();

            // Try to get from config first
            string typeName = bombType.ToString();
            if (_bombColors.TryGetValue(typeName, out var configColor))
            {
                return configColor;
            }

            // Fallback to hardcoded defaults
            return bombType switch
            {
                BombType.Horizontal => new Color(1f, 1f, 1f, 0.9f),
                BombType.Vertical => new Color(1f, 1f, 1f, 0.9f),
                BombType.Square5x5 => new Color(1f, 0.8f, 0.2f, 0.9f),
                BombType.Color => new Color(1f, 1f, 1f, 0.9f),
                BombType.Ufo => new Color(0.5f, 1f, 0.5f, 0.9f),
                _ => Color.white
            };
        }

        private static void DrawBombPattern(Texture2D tex, BombType bombType, Color color)
        {
            switch (bombType)
            {
                case BombType.Horizontal:
                    // Horizontal line
                    for (int x = 1; x < 7; x++)
                    {
                        tex.SetPixel(x, 3, color);
                        tex.SetPixel(x, 4, color);
                    }
                    break;

                case BombType.Vertical:
                    // Vertical line
                    for (int y = 1; y < 7; y++)
                    {
                        tex.SetPixel(3, y, color);
                        tex.SetPixel(4, y, color);
                    }
                    break;

                case BombType.Square5x5:
                    // Cross pattern for area bomb
                    for (int i = 1; i < 7; i++)
                    {
                        tex.SetPixel(i, 3, color);
                        tex.SetPixel(i, 4, color);
                        tex.SetPixel(3, i, color);
                        tex.SetPixel(4, i, color);
                    }
                    break;

                case BombType.Color:
                    // Diamond pattern for color bomb
                    tex.SetPixel(3, 1, color);
                    tex.SetPixel(4, 1, color);
                    tex.SetPixel(2, 2, color);
                    tex.SetPixel(5, 2, color);
                    tex.SetPixel(1, 3, color);
                    tex.SetPixel(6, 3, color);
                    tex.SetPixel(1, 4, color);
                    tex.SetPixel(6, 4, color);
                    tex.SetPixel(2, 5, color);
                    tex.SetPixel(5, 5, color);
                    tex.SetPixel(3, 6, color);
                    tex.SetPixel(4, 6, color);
                    break;

                case BombType.Ufo:
                    // UFO shape
                    tex.SetPixel(3, 5, color);
                    tex.SetPixel(4, 5, color);
                    for (int x = 2; x < 6; x++)
                    {
                        tex.SetPixel(x, 4, color);
                        tex.SetPixel(x, 3, color);
                    }
                    tex.SetPixel(2, 2, color);
                    tex.SetPixel(5, 2, color);
                    break;
            }
        }

        /// <summary>
        /// Clear all cached sprites and reload config.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var sprite in _colorCache.Values)
            {
                if (sprite != null && sprite.texture != null)
                {
                    Object.Destroy(sprite.texture);
                    Object.Destroy(sprite);
                }
            }
            _colorCache.Clear();

            foreach (var sprite in _bombOverlayCache.Values)
            {
                if (sprite != null && sprite.texture != null)
                {
                    Object.Destroy(sprite.texture);
                    Object.Destroy(sprite);
                }
            }
            _bombOverlayCache.Clear();

            // Reset config cache to reload on next access
            _configLoaded = false;
            _tileColors = null;
            _bombColors = null;
        }
    }
}
