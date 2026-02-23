using Match3.Unity.LevelEditor.Services;
using Match3.Unity.LevelEditor.Views;
using LevelEditorCore = global::Match3.Editor.LevelEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Match3.Unity.LevelEditor
{
    /// <summary>
    /// Scene entry point for the runtime level editor.
    /// Creates LevelEditor (pure C#) and wires it to thin Unity views.
    /// All editing logic lives in Match3.Editor DLL.
    /// </summary>
    public class LevelEditorBootstrap : MonoBehaviour
    {
        private LevelEditorCore _editor;

        // ── Style constants ──
        private static readonly Color BgColor = new Color(0.13f, 0.13f, 0.15f);
        private static readonly Color PanelColor = new Color(0.18f, 0.18f, 0.21f);
        private static readonly Color HeaderColor = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color BtnColor = new Color(0.26f, 0.26f, 0.30f);
        private static readonly Color AccentColor = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color TextWhite = new Color(0.92f, 0.92f, 0.94f);
        private static readonly Color TextDim = new Color(0.52f, 0.52f, 0.58f);
        private static readonly Color InputBg = new Color(0.11f, 0.11f, 0.13f);
        private static readonly Color DividerColor = new Color(0.24f, 0.24f, 0.28f);

        private static Font _font;
        private static Font F => _font ?? (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        private void Start()
        {
            Application.runInBackground = true;
            _editor = new LevelEditorCore(new UnityEditorFileSystem());

            if (FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            BuildUI();
        }

        // ═══════════════════════════════════════════
        //  BUILD UI
        // ═══════════════════════════════════════════

        private void BuildUI()
        {
            // Canvas
            var canvasGo = new GameObject("EditorCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<Image>().color = BgColor;

            var root = canvasGo.GetComponent<RectTransform>();

            BuildTopBar(root);

            var mainArea = CreateAnchoredPanel(root, "MainArea",
                Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(0, -44));

            BuildLeftPanel(mainArea);
            BuildRightPanel(mainArea);
            BuildCenterPanel(mainArea);
        }

        // ═══════════════════════════════════════════
        //  TOP BAR  (h=44)
        // ═══════════════════════════════════════════

        private void BuildTopBar(RectTransform root)
        {
            var bar = CreateAnchoredPanel(root, "TopBar",
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -44), Vector2.zero);
            bar.gameObject.GetComponent<Image>().color = HeaderColor;

            var hlg = ConfigHLG(bar.gameObject, 6, new RectOffset(10, 10, 4, 4));
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = true;

            // Title
            AddLabel(bar.transform, "Level Editor", 16, TextWhite, 130);
            AddVDivider(bar.transform);

            // Buttons
            var undoBtn = AddBtn(bar.transform, "Undo", 52);
            var redoBtn = AddBtn(bar.transform, "Redo", 52);
            AddVDivider(bar.transform);
            var saveBtn = AddBtn(bar.transform, "Save", 52);
            var newBtn = AddBtn(bar.transform, "New", 48);
            var randomBtn = AddBtn(bar.transform, "Random", 62);

            // Flexible spacer
            AddSpacer(bar.transform);

            // Status text (right-aligned)
            var statusGo = AddLabel(bar.transform, "", 12, TextDim, 280);
            statusGo.GetComponent<Text>().alignment = TextAnchor.MiddleRight;

            // Wire EditorToolbarView
            var view = bar.gameObject.AddComponent<EditorToolbarView>();
            Wire(view, "_undoButton", undoBtn.GetComponent<Button>());
            Wire(view, "_redoButton", redoBtn.GetComponent<Button>());
            Wire(view, "_saveButton", saveBtn.GetComponent<Button>());
            Wire(view, "_newButton", newBtn.GetComponent<Button>());
            Wire(view, "_randomButton", randomBtn.GetComponent<Button>());
            Wire(view, "_statusText", statusGo.GetComponent<Text>());
            view.Bind(_editor);
        }

        // ═══════════════════════════════════════════
        //  LEFT PANEL  (w=200, Layer tabs + Palette)
        // ═══════════════════════════════════════════

        private void BuildLeftPanel(RectTransform parent)
        {
            var panel = CreateAnchoredPanel(parent, "LeftPanel",
                Vector2.zero, new Vector2(0, 1),
                Vector2.zero, new Vector2(200, 0));
            panel.gameObject.GetComponent<Image>().color = PanelColor;

            var vlg = ConfigVLG(panel.gameObject, 4, new RectOffset(6, 6, 8, 8));
            vlg.childForceExpandWidth = true;

            // Section: Layers
            AddSectionHeader(panel.transform, "LAYERS");

            var layerTabs = CreateHContainer(panel.transform, "LayerTabs", 30);

            AddHDivider(panel.transform);

            // Section: Palette
            AddSectionHeader(panel.transform, "PALETTE");

            var scrollGo = BuildScrollGrid(panel.transform, "TypeScroll", 2, new Vector2(88, 30), 3);
            var typeContent = scrollGo.transform.Find("Viewport/Content").GetComponent<RectTransform>();

            // Wire EditorPaletteView
            var view = panel.gameObject.AddComponent<EditorPaletteView>();
            Wire(view, "_layerTabContainer", layerTabs);
            Wire(view, "_typeButtonContainer", typeContent);
            view.Bind(_editor);
        }

        // ═══════════════════════════════════════════
        //  RIGHT PANEL  (w=230, Config + Objectives)
        // ═══════════════════════════════════════════

        private void BuildRightPanel(RectTransform parent)
        {
            var panel = CreateAnchoredPanel(parent, "RightPanel",
                new Vector2(1, 0), Vector2.one,
                new Vector2(-230, 0), Vector2.zero);
            panel.gameObject.GetComponent<Image>().color = PanelColor;

            var vlg = ConfigVLG(panel.gameObject, 4, new RectOffset(8, 8, 8, 8));
            vlg.childForceExpandWidth = true;

            // ── Board Config ──
            AddSectionHeader(panel.transform, "BOARD");
            var configGo = BuildConfigSection(panel.transform);
            configGo.GetComponent<EditorConfigView>().Bind(_editor);

            AddHDivider(panel.transform);

            // ── Objectives ──
            AddSectionHeader(panel.transform, "OBJECTIVES");

            var objList = new GameObject("ObjList", typeof(RectTransform));
            objList.transform.SetParent(panel.transform, false);
            var objVlg = ConfigVLG(objList, 3, new RectOffset(0, 0, 0, 0));
            objVlg.childForceExpandWidth = true;
            objList.AddComponent<LayoutElement>().flexibleHeight = 1;

            var addBtn = AddBtn(panel.transform, "+ Add Objective", -1);
            addBtn.GetComponent<LayoutElement>().preferredHeight = 28;

            // Wire EditorObjectiveView
            var view = panel.gameObject.AddComponent<EditorObjectiveView>();
            Wire(view, "_objectiveListContainer", objList.GetComponent<RectTransform>());
            Wire(view, "_addButton", addBtn.GetComponent<Button>());
            view.Bind(_editor);
        }

        // ═══════════════════════════════════════════
        //  CENTER PANEL  (stretch, Grid)
        // ═══════════════════════════════════════════

        private void BuildCenterPanel(RectTransform parent)
        {
            var panel = CreateAnchoredPanel(parent, "CenterPanel",
                Vector2.zero, Vector2.one,
                new Vector2(200, 0), new Vector2(-230, 0));
            panel.gameObject.GetComponent<Image>().color = new Color(0.11f, 0.11f, 0.13f);

            // Grid with inset
            var gridGo = new GameObject("GridLayout", typeof(RectTransform));
            gridGo.transform.SetParent(panel.transform, false);
            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.03f, 0.03f);
            gridRt.anchorMax = new Vector2(0.97f, 0.97f);
            gridRt.offsetMin = Vector2.zero;
            gridRt.offsetMax = Vector2.zero;

            var gridLayout = gridGo.AddComponent<GridLayoutGroup>();
            gridLayout.childAlignment = TextAnchor.MiddleCenter;

            // Wire EditorGridView
            var view = panel.gameObject.AddComponent<EditorGridView>();
            Wire(view, "_gridLayout", gridLayout);
            Wire(view, "_gridContainer", gridRt);
            view.Bind(_editor);
        }

        // ═══════════════════════════════════════════
        //  CONFIG SECTION
        // ═══════════════════════════════════════════

        private static GameObject BuildConfigSection(Transform parent)
        {
            var go = new GameObject("ConfigSection", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var vlg = ConfigVLG(go, 4, new RectOffset(0, 0, 0, 0));
            vlg.childForceExpandWidth = true;
            go.AddComponent<LayoutElement>().preferredHeight = 155;

            var wRow = BuildInputRow(go.transform, "Width");
            var wInput = wRow.transform.Find("Input").GetComponent<InputField>();

            var hRow = BuildInputRow(go.transform, "Height");
            var hInput = hRow.transform.Find("Input").GetComponent<InputField>();

            var resizeBtn = BuildAccentButton(go.transform, "Resize", 26);

            var mRow = BuildInputRow(go.transform, "Moves");
            var mInput = mRow.transform.Find("Input").GetComponent<InputField>();

            var diffRow = BuildSliderRow(go.transform);
            var slider = diffRow.transform.Find("Slider").GetComponent<Slider>();
            var diffLabel = diffRow.transform.Find("ValueLabel").GetComponent<Text>();

            var configView = go.AddComponent<EditorConfigView>();
            Wire(configView, "_widthInput", wInput);
            Wire(configView, "_heightInput", hInput);
            Wire(configView, "_resizeButton", resizeBtn.GetComponent<Button>());
            Wire(configView, "_moveLimitInput", mInput);
            Wire(configView, "_difficultySlider", slider);
            Wire(configView, "_difficultyLabel", diffLabel);

            return go;
        }

        // ═══════════════════════════════════════════
        //  HELPER: Anchored Panel
        // ═══════════════════════════════════════════

        private static RectTransform CreateAnchoredPanel(Component parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = Color.clear;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        // ═══════════════════════════════════════════
        //  HELPER: Layout Groups
        // ═══════════════════════════════════════════

        private static HorizontalLayoutGroup ConfigHLG(GameObject go, float spacing, RectOffset padding)
        {
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.padding = padding;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            return hlg;
        }

        private static VerticalLayoutGroup ConfigVLG(GameObject go, float spacing, RectOffset padding)
        {
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = padding;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            return vlg;
        }

        // ═══════════════════════════════════════════
        //  HELPER: Containers
        // ═══════════════════════════════════════════

        private static RectTransform CreateHContainer(Transform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var hlg = ConfigHLG(go, 3, new RectOffset(0, 0, 0, 0));
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            go.AddComponent<LayoutElement>().preferredHeight = height;
            return go.GetComponent<RectTransform>();
        }

        private static GameObject BuildScrollGrid(Transform parent, string name,
            int columns, Vector2 cellSize, float cellSpacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().flexibleHeight = 1;

            var sr = go.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 25;

            // Viewport
            var vp = new GameObject("Viewport", typeof(RectTransform));
            vp.transform.SetParent(go.transform, false);
            vp.AddComponent<Image>().color = Color.clear;
            vp.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(vp.GetComponent<RectTransform>());

            // Content
            var ct = new GameObject("Content", typeof(RectTransform));
            ct.transform.SetParent(vp.transform, false);
            var ctRt = ct.GetComponent<RectTransform>();
            ctRt.anchorMin = new Vector2(0, 1);
            ctRt.anchorMax = new Vector2(1, 1);
            ctRt.pivot = new Vector2(0.5f, 1);
            ctRt.offsetMin = Vector2.zero;
            ctRt.offsetMax = Vector2.zero;

            var glg = ct.AddComponent<GridLayoutGroup>();
            glg.cellSize = cellSize;
            glg.spacing = Vector2.one * cellSpacing;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = columns;
            glg.padding = new RectOffset(2, 2, 2, 2);

            var csf = ct.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = vp.GetComponent<RectTransform>();
            sr.content = ctRt;

            return go;
        }

        // ═══════════════════════════════════════════
        //  HELPER: Buttons
        // ═══════════════════════════════════════════

        private static GameObject AddBtn(Transform parent, string label, float width)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 30;
            if (width > 0)
                le.preferredWidth = width;
            else
                le.flexibleWidth = 1;

            var img = go.AddComponent<Image>();
            img.color = BtnColor;
            go.AddComponent<Button>().targetGraphic = img;

            AddCenteredText(go.transform, label, 12, TextWhite);
            return go;
        }

        private static GameObject BuildAccentButton(Transform parent, string label, float height)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = height;

            var img = go.AddComponent<Image>();
            img.color = AccentColor;
            go.AddComponent<Button>().targetGraphic = img;

            AddCenteredText(go.transform, label, 12, TextWhite);
            return go;
        }

        // ═══════════════════════════════════════════
        //  HELPER: Text
        // ═══════════════════════════════════════════

        private static GameObject AddLabel(Transform parent, string content, int size, Color color, float width)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            if (width > 0) le.preferredWidth = width;

            var t = go.AddComponent<Text>();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.font = F;
            t.alignment = TextAnchor.MiddleLeft;
            return go;
        }

        private static void AddSectionHeader(Transform parent, string title)
        {
            var go = new GameObject("Header_" + title, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 18;

            var t = go.AddComponent<Text>();
            t.text = title;
            t.fontSize = 10;
            t.color = TextDim;
            t.font = F;
            t.fontStyle = FontStyle.Bold;
        }

        private static void AddCenteredText(Transform parent, string content, int size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = content;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = size;
            t.color = color;
            t.font = F;
            Stretch(go.GetComponent<RectTransform>());
        }

        // ═══════════════════════════════════════════
        //  HELPER: Dividers & Spacers
        // ═══════════════════════════════════════════

        private static void AddVDivider(Transform parent)
        {
            var go = new GameObject("VDiv", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredWidth = 1;
            go.AddComponent<Image>().color = DividerColor;
        }

        private static void AddHDivider(Transform parent)
        {
            var go = new GameObject("HDiv", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 1;
            go.AddComponent<Image>().color = DividerColor;
        }

        private static void AddSpacer(Transform parent)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        // ═══════════════════════════════════════════
        //  HELPER: Input Row
        // ═══════════════════════════════════════════

        private static GameObject BuildInputRow(Transform parent, string label)
        {
            var row = new GameObject(label + "Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 24;

            var hlg = ConfigHLG(row, 4, new RectOffset(0, 0, 0, 0));
            hlg.childForceExpandHeight = true;

            // Label
            var lbl = new GameObject("Label", typeof(RectTransform));
            lbl.transform.SetParent(row.transform, false);
            lbl.AddComponent<LayoutElement>().preferredWidth = 50;
            var lt = lbl.AddComponent<Text>();
            lt.text = label;
            lt.fontSize = 11;
            lt.color = TextDim;
            lt.font = F;
            lt.alignment = TextAnchor.MiddleLeft;

            // Input field
            var inp = new GameObject("Input", typeof(RectTransform));
            inp.transform.SetParent(row.transform, false);
            inp.AddComponent<LayoutElement>().flexibleWidth = 1;
            inp.AddComponent<Image>().color = InputBg;

            var field = inp.AddComponent<InputField>();
            field.contentType = InputField.ContentType.IntegerNumber;

            var txt = new GameObject("Text", typeof(RectTransform));
            txt.transform.SetParent(inp.transform, false);
            var t = txt.AddComponent<Text>();
            t.fontSize = 12;
            t.color = TextWhite;
            t.font = F;
            t.alignment = TextAnchor.MiddleLeft;
            var tRt = txt.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(6, 0);
            tRt.offsetMax = new Vector2(-4, 0);

            field.textComponent = t;
            return row;
        }

        // ═══════════════════════════════════════════
        //  HELPER: Slider Row
        // ═══════════════════════════════════════════

        private static GameObject BuildSliderRow(Transform parent)
        {
            var row = new GameObject("DiffRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 24;

            var hlg = ConfigHLG(row, 4, new RectOffset(0, 0, 0, 0));
            hlg.childForceExpandHeight = true;

            // Label
            var lbl = new GameObject("Label", typeof(RectTransform));
            lbl.transform.SetParent(row.transform, false);
            lbl.AddComponent<LayoutElement>().preferredWidth = 50;
            var lt = lbl.AddComponent<Text>();
            lt.text = "Diff";
            lt.fontSize = 11;
            lt.color = TextDim;
            lt.font = F;
            lt.alignment = TextAnchor.MiddleLeft;

            // Slider
            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(row.transform, false);
            sliderGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Background track
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(sliderGo.transform, false);
            bg.AddComponent<Image>().color = InputBg;
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.3f);
            bgRt.anchorMax = new Vector2(1, 0.7f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill area
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0, 0.3f);
            faRt.anchorMax = new Vector2(1, 0.7f);
            faRt.offsetMin = Vector2.zero;
            faRt.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            fill.AddComponent<Image>().color = AccentColor;
            Stretch(fill.GetComponent<RectTransform>());

            // Handle area
            var handleArea = new GameObject("HandleSlideArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>());

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            handle.AddComponent<Image>().color = Color.white;
            var hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(10, 10);

            // Wire Slider component
            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = hRt;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 0.5f;

            // Value label
            var valGo = new GameObject("ValueLabel", typeof(RectTransform));
            valGo.transform.SetParent(row.transform, false);
            valGo.AddComponent<LayoutElement>().preferredWidth = 80;
            var vt = valGo.AddComponent<Text>();
            vt.text = "0.50";
            vt.fontSize = 11;
            vt.color = TextDim;
            vt.font = F;
            vt.alignment = TextAnchor.MiddleRight;

            return row;
        }

        // ═══════════════════════════════════════════
        //  HELPER: Utility
        // ═══════════════════════════════════════════

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Wire(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
