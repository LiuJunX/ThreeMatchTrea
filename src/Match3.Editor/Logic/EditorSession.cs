using System;
using System.ComponentModel;
using System.Linq;
using Match3.Core.Config;
using Match3.Core.Scenarios;
using Match3.Core.Models.Enums;

namespace Match3.Editor.Logic
{
    public enum EditorMode
    {
        Level,
        Scenario
    }

    public class EditorSession : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private EditorMode _currentMode = EditorMode.Level;
        private LevelConfig _currentLevel = new LevelConfig();
        private ScenarioConfig _currentScenario = new ScenarioConfig();
        private string _scenarioName = "New Scenario";
        private bool _isDirty;

        public EditorSession()
        {
            EnsureDefaultLevel();
        }

        public EditorMode CurrentMode
        {
            get => _currentMode;
            set
            {
                if (_currentMode != value)
                {
                    _currentMode = value;
                    OnPropertyChanged(nameof(CurrentMode));
                    OnPropertyChanged(nameof(ActiveLevelConfig));
                }
            }
        }

        public LevelConfig CurrentLevel
        {
            get => _currentLevel;
            set
            {
                _currentLevel = value;
                OnPropertyChanged(nameof(CurrentLevel));
                if (CurrentMode == EditorMode.Level)
                    OnPropertyChanged(nameof(ActiveLevelConfig));
            }
        }

        public ScenarioConfig CurrentScenario
        {
            get => _currentScenario;
            set
            {
                _currentScenario = value;
                OnPropertyChanged(nameof(CurrentScenario));
                OnPropertyChanged(nameof(ScenarioDescription));
                if (CurrentMode == EditorMode.Scenario)
                    OnPropertyChanged(nameof(ActiveLevelConfig));
            }
        }

        public string ScenarioName
        {
            get => _scenarioName;
            set
            {
                if (_scenarioName != value)
                {
                    _scenarioName = value;
                    OnPropertyChanged(nameof(ScenarioName));
                    IsDirty = true;
                }
            }
        }

        public string ScenarioDescription
        {
            get => CurrentScenario.Description;
            set
            {
                if (CurrentScenario.Description != value)
                {
                    CurrentScenario.Description = value;
                    OnPropertyChanged(nameof(ScenarioDescription));
                    IsDirty = true;
                }
            }
        }

        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged(nameof(IsDirty));
                }
            }
        }

        public LevelConfig ActiveLevelConfig => CurrentMode == EditorMode.Level ? CurrentLevel : CurrentScenario.InitialState;

        public void EnsureDefaultLevel()
        {
            if (CurrentLevel.Grid == null || CurrentLevel.Grid.Length == 0)
            {
                CurrentLevel = new LevelConfig(8, 8);
                // GridManipulator will handle filling content
            }
            if (CurrentLevel.Bombs == null || CurrentLevel.Bombs.Length != CurrentLevel.Grid.Length)
            {
                CurrentLevel.Bombs = new BombType[CurrentLevel.Grid.Length];
            }
        }

        public void NotifyActiveLevelChanged()
        {
            OnPropertyChanged(nameof(ActiveLevelConfig));
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
