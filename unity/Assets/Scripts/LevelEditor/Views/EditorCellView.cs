using Match3.Core.Config;
using Match3.Core.Models.Enums;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Unity.LevelEditor.Views
{
    /// <summary>
    /// Renders a single grid cell. Purely visual — no logic.
    /// </summary>
    public class EditorCellView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _coverOverlay;
        [SerializeField] private Image _groundOverlay;
        [SerializeField] private Text _bombLabel;
        [SerializeField] private Image _activeLayerHighlight;

        public void UpdateVisual(LevelConfig config, int index, int activeLayer)
        {
            if (index < 0 || index >= config.Grid.Length)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // Tile background
            var tileType = config.Grid[index];
            _background.color = EditorColorMap.GetTileColor(tileType);

            // Bomb label (bombs are stored directly in Grid as ElementType values)
            if (_bombLabel != null)
            {
                var label = tileType.IsBomb() ? EditorColorMap.GetBombLabel(tileType) : "";
                _bombLabel.text = label;
                _bombLabel.gameObject.SetActive(!string.IsNullOrEmpty(label));
            }

            // Cover overlay
            if (_coverOverlay != null)
            {
                var coverType = config.Covers != null && index < config.Covers.Length
                    ? config.Covers[index] : CoverType.None;
                _coverOverlay.color = EditorColorMap.GetCoverColor(coverType);
                _coverOverlay.gameObject.SetActive(coverType != CoverType.None);
            }

            // Ground overlay
            if (_groundOverlay != null)
            {
                var groundType = config.Grounds != null && index < config.Grounds.Length
                    ? config.Grounds[index] : GroundType.None;
                _groundOverlay.color = EditorColorMap.GetGroundColor(groundType);
                _groundOverlay.gameObject.SetActive(groundType != GroundType.None);
            }

            // Active layer highlight border
            if (_activeLayerHighlight != null)
            {
                bool showHighlight = false;
                if (activeLayer == 1 && config.Covers != null && index < config.Covers.Length)
                    showHighlight = config.Covers[index] != CoverType.None;
                else if (activeLayer == 2 && config.Grounds != null && index < config.Grounds.Length)
                    showHighlight = config.Grounds[index] != GroundType.None;

                _activeLayerHighlight.gameObject.SetActive(showHighlight);
            }
        }
    }
}
