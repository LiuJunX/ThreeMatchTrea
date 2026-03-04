using System;
using System.Collections.Generic;
using Match3.Core.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Unity.UI
{
    /// <summary>
    /// Level selection grid with lock/star state.
    /// Own Canvas (ScreenSpaceOverlay).
    /// </summary>
    public sealed class LevelSelectPanel : MonoBehaviour
    {
        private const int Columns = 4;
        private const float CellSize = 140f;
        private const float Spacing = 15f;

        private Canvas _canvas;
        private RectTransform _gridParent;
        private readonly List<LevelCell> _cells = new();

        public event Action<string> OnLevelSelected;
        public event Action OnBackClicked;

        public void Initialize()
        {
            _canvas = UIFactory.CreateCanvas("LevelSelectCanvas");
            _canvas.sortingOrder = 200;
            _canvas.transform.SetParent(transform, false);

            // Dark background
            var bg = UIFactory.CreatePanel(
                _canvas.transform,
                new Color(0.1f, 0.1f, 0.15f, 1f),
                "Background");
            UIFactory.SetAnchors(bg, AnchorPreset.Stretch);
            bg.sizeDelta = Vector2.zero;

            // Header
            var header = UIFactory.CreatePanel(
                _canvas.transform,
                new Color(0.15f, 0.15f, 0.2f, 0.95f),
                "Header");
            UIFactory.SetAnchors(header, AnchorPreset.TopStretch);
            header.sizeDelta = new Vector2(0, 80);

            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(20, 20, 10, 10);
            headerLayout.spacing = 10;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = false;
            headerLayout.childControlHeight = false;

            var backBtn = UIFactory.CreateButton(
                header, "< Back", () => OnBackClicked?.Invoke(),
                new Color(0.3f, 0.3f, 0.35f), Color.white, 22, "BackButton");
            var backLayout = backBtn.gameObject.AddComponent<LayoutElement>();
            backLayout.preferredWidth = 100;
            backLayout.preferredHeight = 50;

            var headerTitle = UIFactory.CreateText(
                header, "SELECT LEVEL", 36,
                Color.white, TextAlignmentOptions.Center, "HeaderTitle");
            var titleLayout = headerTitle.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredWidth = 400;
            titleLayout.preferredHeight = 50;

            // Scroll view area
            var scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(_canvas.transform, false);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            UIFactory.SetAnchors(scrollRect, AnchorPreset.Stretch);
            scrollRect.offsetMin = new Vector2(20, 20);
            scrollRect.offsetMax = new Vector2(-20, -90);

            var scrollView = scrollGo.AddComponent<ScrollRect>();
            scrollView.horizontal = false;
            scrollView.vertical = true;
            scrollView.movementType = ScrollRect.MovementType.Clamped;

            // Mask
            var maskImage = scrollGo.AddComponent<Image>();
            maskImage.color = Color.white; // Mask needs non-zero alpha for stencil
            scrollGo.AddComponent<Mask>().showMaskGraphic = false;

            // Content container
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            _gridParent = contentGo.AddComponent<RectTransform>();
            _gridParent.anchorMin = new Vector2(0, 1);
            _gridParent.anchorMax = new Vector2(1, 1);
            _gridParent.pivot = new Vector2(0.5f, 1);
            _gridParent.anchoredPosition = Vector2.zero;

            var gridLayout = contentGo.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(CellSize, CellSize);
            gridLayout.spacing = new Vector2(Spacing, Spacing);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Columns;
            gridLayout.padding = new RectOffset(10, 10, 10, 10);

            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollView.content = _gridParent;
        }

        /// <summary>
        /// Populate the grid from level IDs and progress.
        /// </summary>
        public void Populate(string[] levelIds, PlayerProgress progress)
        {
            // Clear old cells
            foreach (var cell in _cells)
                Destroy(cell.Root);
            _cells.Clear();

            for (int i = 0; i < levelIds.Length; i++)
            {
                var levelId = levelIds[i];
                var unlocked = progress.IsLevelUnlocked(levelId);
                var stars = progress.GetBestStars(levelId);
                var displayNum = i + 1;

                var cell = CreateCell(levelId, displayNum, unlocked, stars);
                _cells.Add(cell);
            }
        }

        private LevelCell CreateCell(string levelId, int num, bool unlocked, int stars)
        {
            var go = new GameObject($"Level_{levelId}");
            go.transform.SetParent(_gridParent, false);

            var rect = go.AddComponent<RectTransform>();

            var bgColor = unlocked
                ? new Color(0.25f, 0.35f, 0.55f, 1f)
                : new Color(0.2f, 0.2f, 0.25f, 1f);
            var bgImage = go.AddComponent<Image>();
            bgImage.color = bgColor;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.padding = new RectOffset(10, 10, 15, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;

            // Level number
            var numText = UIFactory.CreateText(
                rect, num.ToString(), 32,
                unlocked ? Color.white : Color.gray,
                TextAlignmentOptions.Center, "Num");
            var numLayout = numText.gameObject.AddComponent<LayoutElement>();
            numLayout.preferredHeight = 40;

            // Stars or lock icon
            string starsStr;
            Color starsColor;
            if (!unlocked)
            {
                starsStr = "LOCKED";
                starsColor = new Color(0.6f, 0.6f, 0.6f);
            }
            else if (stars > 0)
            {
                // Use * for filled, - for empty (safe for all fonts)
                starsStr = new string('*', stars) + new string('-', 3 - stars);
                starsColor = new Color(1f, 0.85f, 0.2f);
            }
            else
            {
                starsStr = "- - -";
                starsColor = new Color(0.6f, 0.6f, 0.6f);
            }

            var starText = UIFactory.CreateText(
                rect, starsStr, 22,
                starsColor, TextAlignmentOptions.Center, "Stars");
            var starLayout = starText.gameObject.AddComponent<LayoutElement>();
            starLayout.preferredHeight = 30;

            // Button
            if (unlocked)
            {
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = bgImage;
                var colors = btn.colors;
                colors.normalColor = bgColor;
                colors.highlightedColor = new Color(0.35f, 0.45f, 0.65f);
                colors.pressedColor = new Color(0.2f, 0.25f, 0.4f);
                colors.selectedColor = bgColor;
                btn.colors = colors;

                var id = levelId; // capture
                btn.onClick.AddListener(() => OnLevelSelected?.Invoke(id));
            }

            return new LevelCell { Root = go, LevelId = levelId };
        }

        public void Show() => _canvas.gameObject.SetActive(true);
        public void Hide() => _canvas.gameObject.SetActive(false);

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private struct LevelCell
        {
            public GameObject Root;
            public string LevelId;
        }
    }
}
