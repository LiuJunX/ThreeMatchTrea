using Match3.Core.Models.Grid;

namespace Match3.Unity.Controllers
{
    public enum HintAnimationType { None, SwapNudge, BombPulse }

    public interface IHintContext
    {
        bool IsIdle { get; }
        bool HasSelection { get; }
        void ClearSelection();
        bool TryGetHintMove(out int actionType, out Position from, out Position to);
        int GetTileIdAt(Position pos);
    }

    public readonly struct HintResult
    {
        public static readonly HintResult None = new HintResult(false, -1, HintAnimationType.None, default, default);

        public readonly bool IsActive;
        public readonly int TileId;
        public readonly HintAnimationType Type;
        public readonly Position From;
        public readonly Position To;

        public HintResult(bool isActive, int tileId, HintAnimationType type, Position from, Position to)
        {
            IsActive = isActive;
            TileId = tileId;
            Type = type;
            From = from;
            To = to;
        }
    }

    public sealed class HintController
    {
        public enum HintState { Disabled, Waiting, Showing }

        private const float InitialDelay = 4f;
        private const float ShowDuration = 3f;
        private const float CycleInterval = 3f;

        private readonly IHintContext _context;
        private float _timer;
        private bool _enabled = true;

        public HintState State { get; private set; }
        public HintResult CurrentHint { get; private set; }
        public int HintGeneration { get; private set; }

        public HintController(IHintContext context)
        {
            _context = context;
            State = HintState.Disabled;
            CurrentHint = HintResult.None;
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                State = HintState.Disabled;
                CurrentHint = HintResult.None;
                _timer = 0f;
            }
        }

        public void OnUserInput()
        {
            State = HintState.Disabled;
            CurrentHint = HintResult.None;
            _timer = 0f;
        }

        public void Update(float deltaTime)
        {
            if (!_enabled) return;

            // Disabled → Waiting transition (then fall through to process timer)
            if (State == HintState.Disabled)
            {
                if (!_context.IsIdle) return;
                State = HintState.Waiting;
                _timer = InitialDelay;
            }

            if (State == HintState.Waiting)
            {
                if (!_context.IsIdle)
                {
                    State = HintState.Disabled;
                    _timer = 0f;
                    return;
                }
                _timer -= deltaTime;
                if (_timer <= 0f)
                {
                    ShowHint();
                }
                return;
            }

            // State == Showing
            if (!_context.IsIdle)
            {
                State = HintState.Disabled;
                CurrentHint = HintResult.None;
                _timer = 0f;
                return;
            }
            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                // Animation done → wait CycleInterval before next hint
                CurrentHint = HintResult.None;
                State = HintState.Waiting;
                _timer = CycleInterval;
            }
        }

        private void ShowHint()
        {
            HintGeneration++;

            if (_context.HasSelection)
            {
                _context.ClearSelection();
            }

            if (_context.TryGetHintMove(out int actionType, out var from, out var to))
            {
                // actionType: 0 = Swap, 1 = Tap (matches MoveActionType enum)
                var hintType = actionType == 1 ? HintAnimationType.BombPulse : HintAnimationType.SwapNudge;
                var tileId = _context.GetTileIdAt(from);

                CurrentHint = new HintResult(true, tileId, hintType, from, to);
            }
            else
            {
                CurrentHint = HintResult.None;
            }

            State = HintState.Showing;
            _timer = ShowDuration;
        }
    }
}
