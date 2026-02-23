using UnityEngine;
using UnityEngine.UI;
using LevelEditorCore = global::Match3.Editor.LevelEditor;
using ValidationResult = global::Match3.Editor.Validation.ValidationResult;

namespace Match3.Unity.LevelEditor.Views
{
    /// <summary>
    /// Renders toolbar buttons (undo, redo, save, load, new, random).
    /// Thin view: forwards clicks to LevelEditor.
    /// </summary>
    public class EditorToolbarView : MonoBehaviour
    {
        [SerializeField] private Button _undoButton;
        [SerializeField] private Button _redoButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _newButton;
        [SerializeField] private Button _randomButton;
        [SerializeField] private Text _statusText;

        private LevelEditorCore _editor;
        private Color _defaultStatusColor;

        public void Bind(LevelEditorCore editor)
        {
            _editor = editor;
            _editor.StateChanged += Refresh;

            if (_statusText != null)
                _defaultStatusColor = _statusText.color;

            _undoButton.onClick.AddListener(() => _editor.Undo());
            _redoButton.onClick.AddListener(() => _editor.Redo());
            _saveButton.onClick.AddListener(OnSave);
            _newButton.onClick.AddListener(() => _editor.NewLevel(8, 8));
            _randomButton.onClick.AddListener(() =>
                _editor.GenerateRandom(System.Environment.TickCount));

            Refresh();
        }

        private void OnDestroy()
        {
            if (_editor != null)
                _editor.StateChanged -= Refresh;
        }

        private void Refresh()
        {
            _undoButton.interactable = _editor.CanUndo;
            _redoButton.interactable = _editor.CanRedo;

            if (_statusText != null)
            {
                _statusText.color = _defaultStatusColor;
                var dirty = _editor.IsDirty ? " *" : "";
                var fileName = _editor.GetCurrentFileName();
                var display = string.IsNullOrEmpty(fileName) ? "(unsaved)" : fileName;
                _statusText.text = display + dirty;
            }
        }

        private void OnSave()
        {
            if (string.IsNullOrEmpty(_editor.CurrentFilePath))
            {
                var name = $"level_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
                var dir = _editor.GetLevelsDirectory();
                var path = dir + "/" + name;
                var result = _editor.SaveAs(path);
                ShowValidation(result);
            }
            else
            {
                var result = _editor.Save();
                ShowValidation(result);
            }
        }

        private void ShowValidation(ValidationResult result)
        {
            if (result.IsValid)
            {
                if (_statusText != null)
                {
                    _statusText.color = _defaultStatusColor;
                    _statusText.text = "Saved!";
                }
            }
            else
            {
                if (_statusText != null)
                {
                    _statusText.text = "Save failed: " + result.Messages[0].Message;
                    _statusText.color = Color.red;
                }
            }
        }
    }
}
