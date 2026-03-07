using Match3.Core.Models.Grid;
using Match3.Unity.Controllers;
using NUnit.Framework;

namespace Match3.Unity.Tests
{
    public class HintControllerTests
    {
        private class StubHintContext : IHintContext
        {
            public bool IsIdle { get; set; }
            public bool HasSelection { get; set; }
            public int ClearSelectionCallCount { get; private set; }

            // Configure hint move output
            public bool HintMoveAvailable { get; set; }
            public int HintActionType { get; set; } // 0=Swap, 1=Tap
            public Position HintFrom { get; set; }
            public Position HintTo { get; set; }
            public int TileIdToReturn { get; set; } = 42;

            public void ClearSelection() => ClearSelectionCallCount++;

            public bool TryGetHintMove(out int actionType, out Position from, out Position to)
            {
                actionType = HintActionType;
                from = HintFrom;
                to = HintTo;
                return HintMoveAvailable;
            }

            public int GetTileIdAt(Position pos) => TileIdToReturn;
        }

        private StubHintContext _context;
        private HintController _controller;

        [SetUp]
        public void SetUp()
        {
            _context = new StubHintContext();
            _controller = new HintController(_context);
        }

        // === Basic State Machine ===

        [Test]
        public void InitialState_IsDisabled()
        {
            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);
            Assert.IsFalse(_controller.CurrentHint.IsActive);
        }

        [Test]
        public void WhenIdle_TransitionsToWaiting()
        {
            _context.IsIdle = true;
            _controller.Update(0f);
            Assert.AreEqual(HintController.HintState.Waiting, _controller.State);
        }

        [Test]
        public void WhenIdleFor4Seconds_ShowsHint()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0; // Swap
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.Update(4.1f);

            Assert.AreEqual(HintController.HintState.Showing, _controller.State);
            Assert.IsTrue(_controller.CurrentHint.IsActive);
            Assert.AreEqual(HintAnimationType.SwapNudge, _controller.CurrentHint.Type);
        }

        [Test]
        public void WhenShowingExpires_GoesToWaitingThenShowing()
        {
            // Get into Showing state
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.Update(4.1f); // -> Showing
            Assert.AreEqual(HintController.HintState.Showing, _controller.State);

            // After 3s show duration → Waiting (gap)
            _controller.Update(3.1f);
            Assert.AreEqual(HintController.HintState.Waiting, _controller.State);
            Assert.IsFalse(_controller.CurrentHint.IsActive);

            // After 3s gap → Showing again
            _controller.Update(3.1f);
            Assert.AreEqual(HintController.HintState.Showing, _controller.State);
            Assert.IsTrue(_controller.CurrentHint.IsActive);
        }

        [Test]
        public void CycleShowsNewHintAfterGap()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);
            _context.TileIdToReturn = 10;

            _controller.Update(4.1f); // -> Showing with tileId 10
            Assert.AreEqual(10, _controller.CurrentHint.TileId);

            // Change hint move for next cycle
            _context.HintFrom = new Position(2, 2);
            _context.HintTo = new Position(3, 2);
            _context.TileIdToReturn = 20;

            _controller.Update(3.1f); // show expires → Waiting (gap)
            Assert.IsFalse(_controller.CurrentHint.IsActive);

            _controller.Update(3.1f); // gap expires → Showing with new hint
            Assert.AreEqual(20, _controller.CurrentHint.TileId);
        }

        // === User Input ===

        [Test]
        public void OnUserInput_ClearsHintAndGoesDisabled()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.Update(4.1f); // -> Showing
            Assert.IsTrue(_controller.CurrentHint.IsActive);

            _controller.OnUserInput();
            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);
            Assert.IsFalse(_controller.CurrentHint.IsActive);
        }

        [Test]
        public void OnUserInput_DuringWaiting_ResetsTimer()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.Update(3f); // Waiting, 1s left
            Assert.AreEqual(HintController.HintState.Waiting, _controller.State);

            _controller.OnUserInput(); // -> Disabled
            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);

            // Re-enter Waiting, timer should restart from 4s
            _controller.Update(0f); // -> Waiting
            Assert.AreEqual(HintController.HintState.Waiting, _controller.State);

            _controller.Update(3.9f); // still waiting (less than 4s total)
            Assert.AreEqual(HintController.HintState.Waiting, _controller.State);

            _controller.Update(0.2f); // now > 4s total -> Showing
            Assert.AreEqual(HintController.HintState.Showing, _controller.State);
        }

        [Test]
        public void MultipleRapidOnUserInput_NoError()
        {
            _controller.OnUserInput();
            _controller.OnUserInput();
            _controller.OnUserInput();
            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);
        }

        // === Enable/Disable ===

        [Test]
        public void SetEnabled_False_ClearsHintImmediately()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.Update(4.1f); // -> Showing
            Assert.IsTrue(_controller.CurrentHint.IsActive);

            _controller.SetEnabled(false);
            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);
            Assert.IsFalse(_controller.CurrentHint.IsActive);
        }

        [Test]
        public void SetEnabled_True_AfterDisable_RestartsFromDisabled()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.SetEnabled(false);
            _controller.SetEnabled(true);

            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);

            _controller.Update(4.1f); // -> Showing
            Assert.IsTrue(_controller.CurrentHint.IsActive);
        }

        [Test]
        public void WhenDisabled_UpdateDoesNothing()
        {
            _controller.SetEnabled(false);
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;

            _controller.Update(10f);
            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);
            Assert.IsFalse(_controller.CurrentHint.IsActive);
        }

        // === Board Unstable ===

        [Test]
        public void BoardBecomesUnstable_DuringWaiting_GoesDisabled()
        {
            _context.IsIdle = true;
            _controller.Update(2f); // Waiting
            Assert.AreEqual(HintController.HintState.Waiting, _controller.State);

            _context.IsIdle = false;
            _controller.Update(0.1f);
            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);
        }

        [Test]
        public void BoardBecomesUnstable_DuringShowing_ClearsHint()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.Update(4.1f); // -> Showing
            Assert.IsTrue(_controller.CurrentHint.IsActive);

            _context.IsIdle = false;
            _controller.Update(0.1f);
            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);
            Assert.IsFalse(_controller.CurrentHint.IsActive);
        }

        // === Selection Conflict ===

        [Test]
        public void ShowHint_WhenPlayerHasSelection_ClearsSelectionFirst()
        {
            _context.IsIdle = true;
            _context.HasSelection = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.Update(4.1f); // -> Showing
            Assert.IsTrue(_context.ClearSelectionCallCount > 0);
        }

        [Test]
        public void ShowHint_WhenNoSelection_DoesNotClearSelection()
        {
            _context.IsIdle = true;
            _context.HasSelection = false;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.Update(4.1f); // -> Showing
            Assert.AreEqual(0, _context.ClearSelectionCallCount);
        }

        // === Hint Types ===

        [Test]
        public void SwapMove_ProducesSwapNudgeHint()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0; // Swap
            _context.HintFrom = new Position(2, 3);
            _context.HintTo = new Position(3, 3);

            _controller.Update(4.1f);
            Assert.AreEqual(HintAnimationType.SwapNudge, _controller.CurrentHint.Type);
            Assert.AreEqual(new Position(2, 3), _controller.CurrentHint.From);
            Assert.AreEqual(new Position(3, 3), _controller.CurrentHint.To);
        }

        [Test]
        public void TapMove_ProducesBombPulseHint()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 1; // Tap
            _context.HintFrom = new Position(4, 5);

            _controller.Update(4.1f);
            Assert.AreEqual(HintAnimationType.BombPulse, _controller.CurrentHint.Type);
            Assert.AreEqual(new Position(4, 5), _controller.CurrentHint.From);
        }

        [Test]
        public void NoValidMoves_ShowsThenRetriesAfterGap()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = false;

            _controller.Update(4.1f);
            Assert.AreEqual(HintController.HintState.Showing, _controller.State);
            Assert.IsFalse(_controller.CurrentHint.IsActive);

            // After 3s show duration → Waiting (gap)
            _controller.Update(3.1f);
            Assert.AreEqual(HintController.HintState.Waiting, _controller.State);

            // After 3s gap, retries with valid move
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(1, 1);
            _context.HintTo = new Position(2, 1);

            _controller.Update(3.1f);
            Assert.AreEqual(HintController.HintState.Showing, _controller.State);
            Assert.IsTrue(_controller.CurrentHint.IsActive);
        }

        // === Cycle Edge Cases ===

        [Test]
        public void Cycle_SameTileId_UpdatesHintDirection()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0); // nudge right
            _context.TileIdToReturn = 42;

            _controller.Update(4.1f); // -> Showing
            Assert.AreEqual(new Position(1, 0), _controller.CurrentHint.To);

            // Cycle: show expires → gap → new hint with different direction
            _context.HintTo = new Position(0, 1); // nudge down
            _controller.Update(3.1f); // show expires → Waiting
            _controller.Update(3.1f); // gap expires → Showing
            Assert.AreEqual(42, _controller.CurrentHint.TileId);
            Assert.AreEqual(new Position(0, 1), _controller.CurrentHint.To);
        }

        [Test]
        public void GetTileIdAt_ReturnsNegative_HintStillActive()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);
            _context.TileIdToReturn = -1; // tile disappeared

            _controller.Update(4.1f);
            Assert.AreEqual(HintController.HintState.Showing, _controller.State);
            // HintResult is active but tileId is -1 — GameController handles this
            Assert.IsTrue(_controller.CurrentHint.IsActive);
            Assert.AreEqual(-1, _controller.CurrentHint.TileId);
        }

        [Test]
        public void SetEnabled_RapidToggle_DoesNotCorruptState()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.Update(4.1f); // -> Showing
            Assert.IsTrue(_controller.CurrentHint.IsActive);

            _controller.SetEnabled(false);
            _controller.SetEnabled(true);
            _controller.SetEnabled(false);
            _controller.SetEnabled(true);

            // After rapid toggle ending on true, should be Disabled (clean slate)
            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);
            Assert.IsFalse(_controller.CurrentHint.IsActive);
        }

        [Test]
        public void DisabledToShowing_SingleLargeUpdate()
        {
            // Verify that a single Update with large deltaTime goes Disabled→Waiting→Showing
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 1; // Tap
            _context.HintFrom = new Position(3, 3);

            _controller.Update(100f); // very large delta
            Assert.AreEqual(HintController.HintState.Showing, _controller.State);
            Assert.IsTrue(_controller.CurrentHint.IsActive);
            Assert.AreEqual(HintAnimationType.BombPulse, _controller.CurrentHint.Type);
        }

        // === Timer Edge Cases ===

        [Test]
        public void IdleFlicker_ResetsTimer()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(0, 0);
            _context.HintTo = new Position(1, 0);

            _controller.Update(3.9f); // Waiting, almost done
            Assert.AreEqual(HintController.HintState.Waiting, _controller.State);

            // Brief unstable
            _context.IsIdle = false;
            _controller.Update(0.01f); // -> Disabled
            Assert.AreEqual(HintController.HintState.Disabled, _controller.State);

            // Back to idle
            _context.IsIdle = true;
            _controller.Update(0f); // -> Waiting (timer restarts at 4s)
            Assert.AreEqual(HintController.HintState.Waiting, _controller.State);

            // Not enough time yet
            _controller.Update(3.5f);
            Assert.AreEqual(HintController.HintState.Waiting, _controller.State);

            // Now enough
            _controller.Update(0.6f);
            Assert.AreEqual(HintController.HintState.Showing, _controller.State);
        }

        [Test]
        public void HintTileId_ComesFromContext()
        {
            _context.IsIdle = true;
            _context.HintMoveAvailable = true;
            _context.HintActionType = 0;
            _context.HintFrom = new Position(1, 2);
            _context.HintTo = new Position(2, 2);
            _context.TileIdToReturn = 42;

            _controller.Update(4.1f);
            Assert.AreEqual(42, _controller.CurrentHint.TileId);
        }
    }
}
