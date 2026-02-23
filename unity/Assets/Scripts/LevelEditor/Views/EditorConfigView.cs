using UnityEngine;
using UnityEngine.UI;
using LevelEditorCore = global::Match3.Editor.LevelEditor;

namespace Match3.Unity.LevelEditor.Views
{
    /// <summary>
    /// Renders level configuration fields (width, height, move limit, difficulty).
    /// Thin view: reads LevelEditor state, forwards changes.
    /// </summary>
    public class EditorConfigView : MonoBehaviour
    {
        [SerializeField] private InputField _widthInput;
        [SerializeField] private InputField _heightInput;
        [SerializeField] private Button _resizeButton;
        [SerializeField] private InputField _moveLimitInput;
        [SerializeField] private Slider _difficultySlider;
        [SerializeField] private Text _difficultyLabel;

        private LevelEditorCore _editor;
        private bool _updating;

        public void Bind(LevelEditorCore editor)
        {
            _editor = editor;
            _editor.StateChanged += Refresh;

            _resizeButton.onClick.AddListener(OnResize);
            _moveLimitInput.onEndEdit.AddListener(OnMoveLimitChanged);
            _difficultySlider.onValueChanged.AddListener(OnDifficultyChanged);

            Refresh();
        }

        private void OnDestroy()
        {
            if (_editor != null)
                _editor.StateChanged -= Refresh;
        }

        private void Refresh()
        {
            _updating = true;

            _widthInput.text = _editor.Level.Width.ToString();
            _heightInput.text = _editor.Level.Height.ToString();
            _moveLimitInput.text = _editor.Level.MoveLimit.ToString();
            _difficultySlider.value = _editor.Level.TargetDifficulty;

            if (_difficultyLabel != null)
                _difficultyLabel.text = $"Difficulty: {_editor.Level.TargetDifficulty:F2}";

            _updating = false;
        }

        private void OnResize()
        {
            if (int.TryParse(_widthInput.text, out int w) && int.TryParse(_heightInput.text, out int h))
                _editor.Resize(w, h);
        }

        private void OnMoveLimitChanged(string value)
        {
            if (_updating) return;
            if (int.TryParse(value, out int limit))
                _editor.SetMoveLimit(limit);
        }

        private void OnDifficultyChanged(float value)
        {
            if (_updating) return;
            _editor.SetTargetDifficulty(value);
        }
    }
}
