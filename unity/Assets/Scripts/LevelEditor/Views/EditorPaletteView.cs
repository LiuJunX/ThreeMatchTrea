using System;
using Match3.Core.Models.Enums;
using UnityEngine;
using UnityEngine.UI;
using LevelEditorCore = global::Match3.Editor.LevelEditor;

namespace Match3.Unity.LevelEditor.Views
{
    /// <summary>
    /// Renders layer tabs and type selection buttons.
    /// Thin view: reads LevelEditor state, forwards clicks.
    /// </summary>
    public class EditorPaletteView : MonoBehaviour
    {
        [SerializeField] private RectTransform _layerTabContainer;
        [SerializeField] private RectTransform _typeButtonContainer;

        private LevelEditorCore _editor;
        private readonly string[] _layerNames = { "Tiles", "Covers", "Grounds" };
        private int _lastLayer = -1;
        private TileType _lastTileType;
        private BombType _lastBombType;
        private CoverType _lastCoverType;
        private GroundType _lastGroundType;

        public void Bind(LevelEditorCore editor)
        {
            _editor = editor;
            _editor.StateChanged += Refresh;
            BuildLayerTabs();
            Refresh();
        }

        private void OnDestroy()
        {
            if (_editor != null)
                _editor.StateChanged -= Refresh;
        }

        private void Refresh()
        {
            UpdateLayerTabHighlights();

            // Only rebuild type buttons when palette-affecting state actually changed.
            // Skip during grid painting (the hot path) since layer/selection don't change.
            bool needsRebuild = _editor.ActiveLayer != _lastLayer
                || _editor.SelectedTileType != _lastTileType
                || _editor.SelectedBombType != _lastBombType
                || _editor.SelectedCoverType != _lastCoverType
                || _editor.SelectedGroundType != _lastGroundType;

            if (needsRebuild)
            {
                _lastLayer = _editor.ActiveLayer;
                _lastTileType = _editor.SelectedTileType;
                _lastBombType = _editor.SelectedBombType;
                _lastCoverType = _editor.SelectedCoverType;
                _lastGroundType = _editor.SelectedGroundType;
                RebuildTypeButtons();
            }
        }

        private void BuildLayerTabs()
        {
            for (int i = 0; i < _layerNames.Length; i++)
            {
                int layer = i;
                CreateButton(_layerTabContainer, _layerNames[i], () => _editor.SetActiveLayer(layer));
            }
        }

        private void UpdateLayerTabHighlights()
        {
            for (int i = 0; i < _layerTabContainer.childCount; i++)
            {
                var img = _layerTabContainer.GetChild(i).GetComponent<Image>();
                if (img != null)
                    img.color = i == _editor.ActiveLayer
                        ? new Color(0.3f, 0.6f, 1f)
                        : new Color(0.25f, 0.25f, 0.25f);
            }
        }

        private void RebuildTypeButtons()
        {
            // Clear existing type buttons
            for (int i = _typeButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(_typeButtonContainer.GetChild(i).gameObject);

            switch (_editor.ActiveLayer)
            {
                case 0: BuildTileButtons(); break;
                case 1: BuildCoverButtons(); break;
                case 2: BuildGroundButtons(); break;
            }
        }

        private void BuildTileButtons()
        {
            var types = new[] {
                TileType.Red, TileType.Green, TileType.Blue,
                TileType.Yellow, TileType.Purple, TileType.Orange,
                TileType.Rainbow, TileType.None, TileType.Wall,
                TileType.Hole, TileType.Spawner, TileType.Sink
            };

            foreach (var t in types)
            {
                var type = t;
                var btn = CreateButton(_typeButtonContainer, type.ToString(), () =>
                {
                    _editor.SetSelectedTileType(type);
                    _editor.SetSelectedBombType(type == TileType.Rainbow ? BombType.Color : BombType.None);
                });
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = _editor.SelectedTileType == type
                        ? HighlightColor(EditorColorMap.GetTileColor(type))
                        : EditorColorMap.GetTileColor(type);
            }

            // Bomb buttons
            var bombs = new[] { BombType.Horizontal, BombType.Vertical, BombType.Square5x5, BombType.Ufo };
            foreach (var b in bombs)
            {
                var bomb = b;
                var label = "Bomb:" + EditorColorMap.GetBombLabel(bomb);
                var btn = CreateButton(_typeButtonContainer, label, () =>
                    _editor.SetSelectedBombType(bomb));
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = _editor.SelectedBombType == bomb
                        ? new Color(1f, 0.9f, 0.3f)
                        : new Color(0.35f, 0.35f, 0.35f);
            }
        }

        private void BuildCoverButtons()
        {
            foreach (CoverType t in Enum.GetValues(typeof(CoverType)))
            {
                var type = t;
                var btn = CreateButton(_typeButtonContainer, type.ToString(), () =>
                    _editor.SetSelectedCoverType(type));
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = _editor.SelectedCoverType == type
                        ? new Color(0.3f, 0.6f, 1f)
                        : new Color(0.35f, 0.35f, 0.35f);
            }
        }

        private void BuildGroundButtons()
        {
            foreach (GroundType t in Enum.GetValues(typeof(GroundType)))
            {
                var type = t;
                var btn = CreateButton(_typeButtonContainer, type.ToString(), () =>
                    _editor.SetSelectedGroundType(type));
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = _editor.SelectedGroundType == type
                        ? new Color(0.3f, 0.6f, 1f)
                        : new Color(0.35f, 0.35f, 0.35f);
            }
        }

        private static GameObject CreateButton(RectTransform parent, string label, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 40;
            le.minHeight = 28;
            le.preferredWidth = 55;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.35f, 0.35f, 0.35f);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 12;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return go;
        }

        private static Color HighlightColor(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, s * 0.5f, Mathf.Min(1f, v * 1.3f));
        }
    }
}
