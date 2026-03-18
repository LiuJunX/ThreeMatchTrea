using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Match3.Unity.UI
{
    /// <summary>
    /// Drag handle for a bookmark marker on the replay progress bar.
    /// Supports click-to-select and drag-to-reposition.
    /// </summary>
    public sealed class BookmarkMarkerHandle : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private static readonly Color NormalColor = new(1f, 0.85f, 0.2f, 1f);
        private static readonly Color SelectedColor = new(1f, 0.4f, 0.2f, 1f);
        private static readonly Color DragColor = Color.white;

        private RectTransform _progressBarRect;
        private RectTransform _rect;
        private int _bookmarkTick;
        private int _totalTicks;
        private Action<int, int> _onMoved;
        private Action<BookmarkMarkerHandle> _onClicked;
        private Image _visualImage;
        private bool _selected;
        private bool _isDragging;

        public int BookmarkTick => _bookmarkTick;

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                if (_visualImage != null && !_isDragging)
                    _visualImage.color = _selected ? SelectedColor : NormalColor;
            }
        }

        public void Initialize(int tick, int totalTicks, RectTransform progressBarRect,
            Action<int, int> onMoved, Action<BookmarkMarkerHandle> onClicked)
        {
            _bookmarkTick = tick;
            _totalTicks = totalTicks;
            _progressBarRect = progressBarRect;
            _onMoved = onMoved;
            _onClicked = onClicked;
            _rect = GetComponent<RectTransform>();
            if (transform.childCount > 0)
                _visualImage = transform.GetChild(0).GetComponent<Image>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isDragging) return;
            _onClicked?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            if (_visualImage != null)
                _visualImage.color = DragColor;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_progressBarRect == null || _rect == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _progressBarRect, eventData.position, eventData.pressEventCamera, out var localPos))
            {
                var barRect = _progressBarRect.rect;
                float t = Mathf.Clamp01((localPos.x - barRect.xMin) / barRect.width);
                _rect.anchorMin = new Vector2(t, 0);
                _rect.anchorMax = new Vector2(t, 1);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            if (_visualImage != null)
                _visualImage.color = _selected ? SelectedColor : NormalColor;

            float t = _rect.anchorMin.x;
            int newTick = Mathf.RoundToInt(t * _totalTicks);

            if (newTick != _bookmarkTick)
            {
                int oldTick = _bookmarkTick;
                _bookmarkTick = newTick;
                _onMoved?.Invoke(oldTick, newTick);
            }
        }
    }
}
