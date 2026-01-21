using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Match3.Core.Analysis;
using Match3.Core.Config;
using Match3.Editor.Interfaces;

namespace Match3.Editor.Logic
{
    /// <summary>
    /// Manages level analysis operations including basic and deep analysis.
    /// Extracted from LevelEditorViewModel to reduce class size.
    /// </summary>
    public class LevelAnalysisManager : INotifyPropertyChanged, IDisposable
    {
        private readonly ILevelAnalysisService _analysisService;
        private readonly ILevelService _levelService;
        private readonly IFileSystemService _fileSystem;
        private readonly Func<LevelConfig> _getActiveConfig;
        private readonly Func<string> _getCurrentLevelPath;

        private CancellationTokenSource? _analysisCts;

        // --- Analysis State ---
        private bool _isAnalyzing;
        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            private set { _isAnalyzing = value; OnPropertyChanged(nameof(IsAnalyzing)); }
        }

        private float _analysisProgress;
        public float AnalysisProgress
        {
            get => _analysisProgress;
            private set { _analysisProgress = value; OnPropertyChanged(nameof(AnalysisProgress)); }
        }

        private string _analysisProgressText = "";
        public string AnalysisProgressText
        {
            get => _analysisProgressText;
            private set { _analysisProgressText = value; OnPropertyChanged(nameof(AnalysisProgressText)); }
        }

        private float _winRate;
        public float WinRate
        {
            get => _winRate;
            private set { _winRate = value; OnPropertyChanged(nameof(WinRate)); }
        }

        private float _deadlockRate;
        public float DeadlockRate
        {
            get => _deadlockRate;
            private set { _deadlockRate = value; OnPropertyChanged(nameof(DeadlockRate)); }
        }

        private string _difficultyText = "";
        public string DifficultyText
        {
            get => _difficultyText;
            private set { _difficultyText = value; OnPropertyChanged(nameof(DifficultyText)); }
        }

        // --- Deep Analysis State ---
        private bool _isDeepAnalysis;
        public bool IsDeepAnalysis
        {
            get => _isDeepAnalysis;
            set { _isDeepAnalysis = value; OnPropertyChanged(nameof(IsDeepAnalysis)); }
        }

        private DeepAnalysisResult? _deepResult;
        public DeepAnalysisResult? DeepResult
        {
            get => _deepResult;
            private set { _deepResult = value; OnPropertyChanged(nameof(DeepResult)); }
        }

        private LevelAnalysisSnapshot? _currentAnalysisSnapshot;
        public LevelAnalysisSnapshot? CurrentAnalysisSnapshot
        {
            get => _currentAnalysisSnapshot;
            private set { _currentAnalysisSnapshot = value; OnPropertyChanged(nameof(CurrentAnalysisSnapshot)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public LevelAnalysisManager(
            ILevelService levelService,
            IFileSystemService fileSystem,
            Func<LevelConfig> getActiveConfig,
            Func<string> getCurrentLevelPath)
        {
            _levelService = levelService;
            _fileSystem = fileSystem;
            _getActiveConfig = getActiveConfig;
            _getCurrentLevelPath = getCurrentLevelPath;
            _analysisService = new LevelAnalysisService();
        }

        public void Dispose()
        {
            _analysisCts?.Cancel();
            _analysisCts?.Dispose();
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Loads cached analysis data from the analysis snapshot file (if exists).
        /// </summary>
        public void LoadCachedAnalysis()
        {
            var currentLevelPath = _getCurrentLevelPath();
            LevelAnalysisSnapshot? snapshot = null;
            if (!string.IsNullOrEmpty(currentLevelPath))
            {
                snapshot = _levelService.ReadAnalysisSnapshot(currentLevelPath);
            }

            CurrentAnalysisSnapshot = snapshot;

            if (snapshot?.Basic != null)
            {
                WinRate = snapshot.Basic.WinRate;
                DeadlockRate = snapshot.Basic.DeadlockRate;
                DifficultyText = $"{snapshot.Basic.DifficultyRating} ({snapshot.Basic.WinRate:P0})";

                if (snapshot.Deep != null)
                {
                    DeepResult = snapshot.Deep.ToResult();
                }
                else
                {
                    DeepResult = null;
                }
            }
            else
            {
                // Backwards compatibility: try reading from old AnalysisCache
                var config = _getActiveConfig();
                var cache = config.AnalysisCache;
                if (cache != null)
                {
                    WinRate = cache.WinRate;
                    DeadlockRate = cache.DeadlockRate;
                    DifficultyText = $"{cache.Difficulty} ({cache.WinRate:P0})";
                }
                else
                {
                    WinRate = 0;
                    DeadlockRate = 0;
                    DifficultyText = "未分析";
                }
                DeepResult = null;
            }
        }

        /// <summary>
        /// Restarts level analysis (cancels previous analysis if running).
        /// </summary>
        public void RestartAnalysis()
        {
            _analysisCts?.Cancel();
            _analysisCts?.Dispose();
            _analysisCts = new CancellationTokenSource();

            var config = _getActiveConfig();
            var cache = config.AnalysisCache;
            if (cache != null)
            {
                WinRate = cache.WinRate;
                DeadlockRate = cache.DeadlockRate;
                DifficultyText = $"{cache.Difficulty} (重新分析中...)";
            }
            else
            {
                WinRate = 0;
                DeadlockRate = 0;
                DifficultyText = "分析中...";
            }

            IsAnalyzing = true;
            AnalysisProgress = 0;

            if (IsDeepAnalysis)
            {
                DeepResult = null;
                AnalysisProgressText = "Deep 0%";
                _ = RunDeepAnalysisAsync(_analysisCts.Token);
            }
            else
            {
                AnalysisProgressText = "0 / 500";
                _ = RunAnalysisAsync(_analysisCts.Token);
            }
        }

        private async Task RunAnalysisAsync(CancellationToken token)
        {
            var progress = new Progress<SimulationProgress>(p =>
            {
                AnalysisProgress = p.Progress;
                AnalysisProgressText = $"{p.CompletedCount} / {p.TotalCount}";
                WinRate = p.WinRate;
                DeadlockRate = p.DeadlockRate;
                DifficultyText = $"通过率: {p.WinRate:P0}";
            });

            try
            {
                var config = _getActiveConfig();
                var result = await _analysisService.AnalyzeAsync(
                    config,
                    new AnalysisConfig { SimulationCount = 500, ProgressReportInterval = 10 },
                    progress,
                    token);

                if (!result.WasCancelled)
                {
                    WinRate = result.WinRate;
                    DeadlockRate = result.DeadlockRate;
                    DifficultyText = $"{GetDifficultyName(result.DifficultyRating)} ({result.WinRate:P0})";

                    SaveAnalysisSnapshot(BasicAnalysisData.FromResult(result), null);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled, ignore
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        private async Task RunDeepAnalysisAsync(CancellationToken token)
        {
            var deepService = new DeepAnalysisService();
            var progress = new Progress<DeepAnalysisProgress>(p =>
            {
                AnalysisProgress = p.Progress;
                AnalysisProgressText = $"Deep {p.Progress:P0}";
                DifficultyText = p.Stage;
            });

            try
            {
                var config = _getActiveConfig();
                var result = await deepService.AnalyzeAsync(
                    config,
                    simulationsPerTier: 250,
                    progress,
                    token);

                if (!result.WasCancelled)
                {
                    DeepResult = result;

                    float casualWinRate = 0;
                    float deadlockRate = 0;
                    if (result.TierWinRates.TryGetValue("Casual", out casualWinRate))
                    {
                        WinRate = casualWinRate;
                    }

                    var skillDesc = result.SkillSensitivity > 0.5f ? "技能关" : "运气关";
                    DifficultyText = $"Deep完成 ({skillDesc})";

                    var basicData = new BasicAnalysisData
                    {
                        TotalSimulations = result.TotalSimulations,
                        WinRate = casualWinRate,
                        DeadlockRate = deadlockRate,
                        DifficultyRating = skillDesc,
                        ElapsedMs = result.ElapsedMs
                    };
                    SaveAnalysisSnapshot(basicData, DeepAnalysisData.FromResult(result));
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled, ignore
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        private void SaveAnalysisSnapshot(BasicAnalysisData? basic, DeepAnalysisData? deep)
        {
            var currentLevelPath = _getCurrentLevelPath();
            if (string.IsNullOrEmpty(currentLevelPath)) return;

            try
            {
                var snapshot = new LevelAnalysisSnapshot
                {
                    Version = 1,
                    AnalyzedAt = DateTime.UtcNow,
                    LevelFileName = _fileSystem.GetFileName(currentLevelPath),
                    Basic = basic,
                    Deep = deep
                };

                if (CurrentAnalysisSnapshot != null)
                {
                    if (basic == null && CurrentAnalysisSnapshot.Basic != null)
                    {
                        snapshot.Basic = CurrentAnalysisSnapshot.Basic;
                    }
                    if (deep == null && CurrentAnalysisSnapshot.Deep != null)
                    {
                        snapshot.Deep = CurrentAnalysisSnapshot.Deep;
                    }
                }

                _levelService.WriteAnalysisSnapshot(currentLevelPath, snapshot);
                CurrentAnalysisSnapshot = snapshot;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save analysis snapshot: {ex.Message}");
            }
        }

        private static string GetDifficultyName(DifficultyRating rating) => rating switch
        {
            DifficultyRating.VeryEasy => "非常简单",
            DifficultyRating.Easy => "简单",
            DifficultyRating.Medium => "中等",
            DifficultyRating.Hard => "困难",
            DifficultyRating.VeryHard => "非常困难",
            _ => "未知"
        };
    }
}
