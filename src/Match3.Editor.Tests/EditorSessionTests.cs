using Xunit;
using Match3.Editor.Logic;
using Match3.Core.Config;

namespace Match3.Editor.Tests
{
    public class EditorSessionTests
    {
        [Fact]
        public void CurrentMode_Change_ShouldNotifyProperties()
        {
            var session = new EditorSession();
            bool modeChanged = false;
            bool activeConfigChanged = false;

            session.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(EditorSession.CurrentMode)) modeChanged = true;
                if (e.PropertyName == nameof(EditorSession.ActiveLevelConfig)) activeConfigChanged = true;
            };

            session.CurrentMode = EditorMode.Scenario;

            Assert.True(modeChanged);
            Assert.True(activeConfigChanged);
            Assert.Equal(EditorMode.Scenario, session.CurrentMode);
        }

        [Fact]
        public void ScenarioName_Change_ShouldSetDirty()
        {
            var session = new EditorSession();
            session.IsDirty = false;

            session.ScenarioName = "Changed";

            Assert.True(session.IsDirty);
        }

        [Fact]
        public void ActiveLevelConfig_ShouldReturnCorrectConfigBasedOnMode()
        {
            var session = new EditorSession();
            
            // Default is Level
            Assert.Same(session.CurrentLevel, session.ActiveLevelConfig);

            session.CurrentMode = EditorMode.Scenario;
            Assert.Same(session.CurrentScenario.InitialState, session.ActiveLevelConfig);
        }
    }
}
