using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LevelEditorCore = global::Match3.Editor.LevelEditor;

namespace Match3.Unity.LevelEditor.Views
{
    /// <summary>
    /// Renders the level grid and handles paint input.
    /// Thin view: reads LevelEditor state, forwards input.
    /// </summary>
    public class EditorGridView : MonoBehaviour
    {
        [SerializeField] private GridLayoutGroup _gridLayout;
        [SerializeField] private RectTransform _gridContainer;

        private LevelEditorCore _editor;
        private readonly List<EditorCellView> _cells = new List<EditorCellView>();
        private int _lastWidth;
        private int _lastHeight;
        private bool _isDragging;

        private bool _needsRebuild;

        public void Bind(LevelEditorCore editor)
        {
            _editor = editor;
            _editor.StateChanged += Refresh;
            // Defer initial build to LateUpdate so layout has a chance to compute rect
            _needsRebuild = true;
        }

        private void LateUpdate()
        {
            if (_needsRebuild && _gridContainer.rect.width > 0)
            {
                _needsRebuild = false;
                RebuildGrid();
            }
        }

        private void OnDestroy()
        {
            if (_editor != null)
                _editor.StateChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_editor.Level.Width != _lastWidth || _editor.Level.Height != _lastHeight)
            {
                // Mark for rebuild in LateUpdate to ensure layout rect is current
                _needsRebuild = true;
            }
            else
            {
                UpdateAllCells();
            }
        }

        private void RebuildGrid()
        {
            _lastWidth = _editor.Level.Width;
            _lastHeight = _editor.Level.Height;

            // Clear existing
            foreach (var cell in _cells)
                Destroy(cell.gameObject);
            _cells.Clear();

            // Configure grid layout
            float containerWidth = _gridContainer.rect.width;
            float containerHeight = _gridContainer.rect.height;
            float cellSize = Mathf.Min(
                (containerWidth - (_lastWidth - 1) * 2f) / _lastWidth,
                (containerHeight - (_lastHeight - 1) * 2f) / _lastHeight);
            cellSize = Mathf.Max(20f, Mathf.Floor(cellSize));

            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.constraintCount = _lastWidth;
            _gridLayout.cellSize = new Vector2(cellSize, cellSize);
            _gridLayout.spacing = new Vector2(2f, 2f);

            // Create cells
            for (int y = 0; y < _lastHeight; y++)
            {
                for (int x = 0; x < _lastWidth; x++)
                {
                    var cellGo = CreateCellGameObject(x, y);
                    cellGo.transform.SetParent(_gridLayout.transform, false);
                    var cellView = cellGo.GetComponent<EditorCellView>();
                    _cells.Add(cellView);
                }
            }

            UpdateAllCells();
        }

        private void UpdateAllCells()
        {
            var config = _editor.Level;
            for (int i = 0; i < _cells.Count; i++)
                _cells[i].UpdateVisual(config, i, _editor.ActiveLayer);
        }

        private GameObject CreateCellGameObject(int x, int y)
        {
            var go = new GameObject($"Cell_{x}_{y}", typeof(RectTransform));

            // Background image
            var bg = go.AddComponent<Image>();
            bg.color = Color.gray;

            // Cover overlay
            var coverGo = new GameObject("CoverOverlay", typeof(RectTransform));
            coverGo.transform.SetParent(go.transform, false);
            var coverImg = coverGo.AddComponent<Image>();
            coverImg.color = Color.clear;
            StretchRectTransform(coverGo.GetComponent<RectTransform>());

            // Ground overlay
            var groundGo = new GameObject("GroundOverlay", typeof(RectTransform));
            groundGo.transform.SetParent(go.transform, false);
            var groundImg = groundGo.AddComponent<Image>();
            groundImg.color = Color.clear;
            StretchRectTransform(groundGo.GetComponent<RectTransform>());

            // Bomb label
            var labelGo = new GameObject("BombLabel", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var text = labelGo.AddComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 18;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            StretchRectTransform(labelGo.GetComponent<RectTransform>());

            // Active layer highlight (border)
            var highlightGo = new GameObject("Highlight", typeof(RectTransform));
            highlightGo.transform.SetParent(go.transform, false);
            var hlImg = highlightGo.AddComponent<Image>();
            hlImg.color = new Color(1f, 1f, 0f, 0.4f);
            StretchRectTransform(highlightGo.GetComponent<RectTransform>());
            highlightGo.SetActive(false);

            // EditorCellView component
            var cellView = go.AddComponent<EditorCellView>();
            // Wire serialized fields via reflection (no prefab, so set directly)
            SetField(cellView, "_background", bg);
            SetField(cellView, "_coverOverlay", coverImg);
            SetField(cellView, "_groundOverlay", groundImg);
            SetField(cellView, "_bombLabel", text);
            SetField(cellView, "_activeLayerHighlight", hlImg);

            // Input handler
            var handler = go.AddComponent<CellInputHandler>();
            handler.Init(x, y, this);

            return go;
        }

        internal void OnCellPointerDown(int x, int y)
        {
            _isDragging = true;
            _editor.BeginStroke();
            _editor.PaintCell(x, y);
        }

        internal void OnCellPointerEnter(int x, int y)
        {
            if (_isDragging)
                _editor.PaintCell(x, y);
        }

        internal void OnCellPointerUp()
        {
            if (_isDragging)
            {
                _isDragging = false;
                _editor.EndStroke();
            }
        }

        private static void StretchRectTransform(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }

    /// <summary>
    /// Handles pointer events on a single cell and forwards to EditorGridView.
    /// </summary>
    public class CellInputHandler : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler
    {
        private int _x, _y;
        private EditorGridView _gridView;

        public void Init(int x, int y, EditorGridView gridView)
        {
            _x = x;
            _y = y;
            _gridView = gridView;
        }

        public void OnPointerDown(PointerEventData eventData) => _gridView.OnCellPointerDown(_x, _y);
        public void OnPointerEnter(PointerEventData eventData) => _gridView.OnCellPointerEnter(_x, _y);
        public void OnPointerUp(PointerEventData eventData) => _gridView.OnCellPointerUp();
    }
}
