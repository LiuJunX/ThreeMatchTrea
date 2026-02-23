using System;
using Match3.Core.Models.Enums;
using Match3.Core.Models.Gameplay;
using global::Match3.Editor.Logic;
using UnityEngine;
using UnityEngine.UI;
using LevelEditorCore = global::Match3.Editor.LevelEditor;

namespace Match3.Unity.LevelEditor.Views
{
    /// <summary>
    /// Renders and edits level objectives (max 4).
    /// Thin view: reads LevelEditor.Level.Objectives, forwards changes.
    /// </summary>
    public class EditorObjectiveView : MonoBehaviour
    {
        [SerializeField] private RectTransform _objectiveListContainer;
        [SerializeField] private Button _addButton;

        private LevelEditorCore _editor;

        public void Bind(LevelEditorCore editor)
        {
            _editor = editor;
            _editor.StateChanged += Refresh;
            _addButton.onClick.AddListener(() => _editor.AddObjective());
            Refresh();
        }

        private void OnDestroy()
        {
            if (_editor != null)
                _editor.StateChanged -= Refresh;
        }

        private void Refresh()
        {
            // Clear existing rows
            for (int i = _objectiveListContainer.childCount - 1; i >= 0; i--)
                Destroy(_objectiveListContainer.GetChild(i).gameObject);

            var objectives = _editor.Level.Objectives;
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i].TargetLayer == ObjectiveTargetLayer.None)
                    continue;
                CreateObjectiveRow(i, objectives[i]);
            }

            _addButton.interactable =
                ObjectiveEditorHelper.GetActiveObjectiveCount(objectives) < ObjectiveEditorHelper.MaxObjectives;
        }

        private void CreateObjectiveRow(int index, LevelObjective obj)
        {
            var row = new GameObject($"Objective_{index}", typeof(RectTransform));
            row.transform.SetParent(_objectiveListContainer, false);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 3;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 26;

            // Layer dropdown (simplified as buttons for now)
            var layerNames = new[] { "Tile", "Cover", "Ground" };
            var layerValues = new[] { ObjectiveTargetLayer.Tile, ObjectiveTargetLayer.Cover, ObjectiveTargetLayer.Ground };
            int currentLayerIdx = Array.IndexOf(layerValues, obj.TargetLayer);
            if (currentLayerIdx < 0) currentLayerIdx = 0;

            int idx = index;
            var layerBtn = CreateSmallButton(row.transform, layerNames[currentLayerIdx], 60, () =>
            {
                int nextLayer = (currentLayerIdx + 1) % layerValues.Length;
                var types = ObjectiveEditorHelper.GetElementTypesForLayer(layerValues[nextLayer]);
                int elemType = types.Length > 0 ? types[0].Value : 0;
                _editor.SetObjective(idx, layerValues[nextLayer], elemType, obj.TargetCount);
            });

            // Element type (cycle through available types)
            var availableTypes = ObjectiveEditorHelper.GetElementTypesForLayer(obj.TargetLayer);
            string elemName = ObjectiveEditorHelper.GetElementTypeName(obj.TargetLayer, obj.ElementType);
            CreateSmallButton(row.transform, elemName, 70, () =>
            {
                if (availableTypes.Length <= 1) return;
                int currentIdx = -1;
                for (int j = 0; j < availableTypes.Length; j++)
                    if (availableTypes[j].Value == obj.ElementType) { currentIdx = j; break; }
                int nextIdx = (currentIdx + 1) % availableTypes.Length;
                _editor.SetObjective(idx, obj.TargetLayer, availableTypes[nextIdx].Value, obj.TargetCount);
            });

            // Target count (- / count / +)
            CreateSmallButton(row.transform, "-", 25, () =>
                _editor.SetObjective(idx, obj.TargetLayer, obj.ElementType,
                    Math.Max(1, obj.TargetCount - 5)));

            CreateLabel(row.transform, obj.TargetCount.ToString(), 35);

            CreateSmallButton(row.transform, "+", 25, () =>
                _editor.SetObjective(idx, obj.TargetLayer, obj.ElementType, obj.TargetCount + 5));

            // Remove button
            CreateSmallButton(row.transform, "×", 25, () => _editor.RemoveObjective(idx));
        }

        private static GameObject CreateSmallButton(Transform parent, string label, float width, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layoutElem = go.AddComponent<LayoutElement>();
            layoutElem.preferredWidth = width;
            layoutElem.preferredHeight = 28;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 11;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return go;
        }

        private static void CreateLabel(Transform parent, string text, float width)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layoutElem = go.AddComponent<LayoutElement>();
            layoutElem.preferredWidth = width;
            layoutElem.preferredHeight = 28;

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 13;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
