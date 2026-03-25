using System;
using System.Collections.Generic;
using Match3.Core.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Unity.UI
{
    /// <summary>
    /// Level selection panel with two sections: official and test levels.
    /// Each section has its own scrollable grid.
    /// Own Canvas (ScreenSpaceOverlay).
    /// </summary>
    public sealed class LevelSelectPanel : MonoBehaviour
    {
        private const int Columns = 4;
        private const float CellSize = 140f;
        private const float Spacing = 15f;
        private const float SectionHeaderHeight = 40f;

        private Canvas _canvas;
        private RectTransform _officialGridParent;
        private RectTransform _testGridParent;
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

            // Official section (top half, below header)
            _officialGridParent = CreateSection(
                _canvas.transform, "正式关卡",
                new Vector2(0, 0.5f), new Vector2(1, 1),
                new Vector2(10, 5), new Vector2(-10, -85));

            // Test section (bottom half)
            _testGridParent = CreateSection(
                _canvas.transform, "测试关卡",
                new Vector2(0, 0), new Vector2(1, 0.5f),
                new Vector2(10, 10), new Vector2(-10, -5));
        }

        private RectTransform CreateSection(
            Transform parent, string title,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var sectionGo = new GameObject($"Section_{title}");
            sectionGo.transform.SetParent(parent, false);
            var sectionRect = sectionGo.AddComponent<RectTransform>();
            sectionRect.anchorMin = anchorMin;
            sectionRect.anchorMax = anchorMax;
            sectionRect.offsetMin = offsetMin;
            sectionRect.offsetMax = offsetMax;

            // Section header
            var headerGo = new GameObject("SectionHeader");
            headerGo.transform.SetParent(sectionGo.transform, false);
            var headerRect = headerGo.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, SectionHeaderHeight);
            var headerBg = headerGo.AddComponent<Image>();
            headerBg.color = new Color(0.18f, 0.18f, 0.25f, 0.9f);

            var headerText = UIFactory.CreateText(
                headerRect, title, 24,
                new Color(0.8f, 0.8f, 0.9f),
                TextAlignmentOptions.MidlineLeft, "SectionTitle");
            var textRect = headerText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20, 0);
            textRect.offsetMax = Vector2.zero;

            // Scroll view (below section header)
            var scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(sectionGo.transform, false);
            var scrollViewRect = scrollGo.AddComponent<RectTransform>();
            scrollViewRect.anchorMin = Vector2.zero;
            scrollViewRect.anchorMax = Vector2.one;
            scrollViewRect.offsetMin = Vector2.zero;
            scrollViewRect.offsetMax = new Vector2(0, -SectionHeaderHeight);

            var scrollView = scrollGo.AddComponent<ScrollRect>();
            scrollView.horizontal = false;
            scrollView.vertical = true;
            scrollView.movementType = ScrollRect.MovementType.Clamped;

            var maskImage = scrollGo.AddComponent<Image>();
            maskImage.color = Color.white;
            scrollGo.AddComponent<Mask>().showMaskGraphic = false;

            // Content container
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            var gridParent = contentGo.AddComponent<RectTransform>();
            gridParent.anchorMin = new Vector2(0, 1);
            gridParent.anchorMax = new Vector2(1, 1);
            gridParent.pivot = new Vector2(0.5f, 1);
            gridParent.anchoredPosition = Vector2.zero;

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

            scrollView.content = gridParent;

            return gridParent;
        }

        /// <summary>
        /// Populate both sections from level IDs and progress.
        /// Test levels are always unlocked.
        /// </summary>
        public void Populate(string[] officialLevelIds, string[] testLevelIds, PlayerProgress progress)
        {
            // Clear old cells
            foreach (var cell in _cells)
                Destroy(cell.Root);
            _cells.Clear();

            // Official levels (progress-based unlock)
            for (int i = 0; i < officialLevelIds.Length; i++)
            {
                var levelId = officialLevelIds[i];
                var unlocked = progress.IsLevelUnlocked(levelId);
                var stars = progress.GetBestStars(levelId);
                var cell = CreateCell(_officialGridParent, levelId, i + 1, unlocked, stars);
                _cells.Add(cell);
            }

            // Test levels (always unlocked)
            for (int i = 0; i < testLevelIds.Length; i++)
            {
                var levelId = testLevelIds[i];
                var stars = progress.GetBestStars(levelId);
                var cell = CreateCell(_testGridParent, levelId, i + 1, true, stars);
                _cells.Add(cell);
            }
        }

        private LevelCell CreateCell(RectTransform parent, string levelId, int num, bool unlocked, int stars)
        {
            var go = new GameObject($"Level_{levelId}");
            go.transform.SetParent(parent, false);

            go.AddComponent<RectTransform>();

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
                go.transform, num.ToString(), 32,
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
                starsStr = new string('*', stars) + new string('-', 3 - stars);
                starsColor = new Color(1f, 0.85f, 0.2f);
            }
            else
            {
                starsStr = "- - -";
                starsColor = new Color(0.6f, 0.6f, 0.6f);
            }

            var starText = UIFactory.CreateText(
                go.transform, starsStr, 22,
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
