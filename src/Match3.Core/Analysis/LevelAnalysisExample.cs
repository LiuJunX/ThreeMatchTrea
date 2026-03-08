using System;
using System.Threading;
using System.Threading.Tasks;

namespace Match3.Core.Analysis;

/// <summary>
/// 关卡分析使用示例
/// </summary>
public static class LevelAnalysisExample
{
    /// <summary>
    /// 示例：在关卡编辑器中使用
    /// </summary>
    public static async Task EditorUsageExample()
    {
        var service = new LevelAnalysisService();

        // 当前编辑的关卡数据
        var levelData = new LevelData
        {
            Width = 8,
            Height = 8,
            MoveLimit = 20,
            TileTypesCount = 5
        };

        // 取消令牌（关卡修改时取消当前分析）
        using var cts = new CancellationTokenSource();

        // 进度回调（注意：在后台线程调用，需要调度到 UI 线程）
        var progress = new Progress<SimulationProgress>(p =>
        {
            // Unity: 使用 MainThreadDispatcher 或 UniTask
            // WPF: 使用 Dispatcher.Invoke
            // 平台相关输出，例如：
            // Unity: MainThreadDispatcher.Post(() => label.text = $"{p.WinRate:P1}");
            // Console: Console.WriteLine($"{p.CompletedCount}/{p.TotalCount}");
            _ = p; // 使用进度数据
        });

        try
        {
            var result = await service.AnalyzeAsync(
                levelData,
                new AnalysisConfig
                {
                    SimulationCount = 1000,
                    ProgressReportInterval = 100,
                    UseParallel = true
                },
                progress,
                cts.Token);

            // 使用分析结果 (result.WinRate, result.DifficultyRating 等)
            _ = result;
        }
        catch (OperationCanceledException)
        {
            // 分析已取消
        }
    }

    /// <summary>
    /// 示例：关卡修改时自动重新分析
    /// </summary>
    public class LevelEditorViewModel : IDisposable
    {
        private readonly ILevelAnalysisService _analysisService;
        private CancellationTokenSource? _currentCts;
        private LevelData _levelData;

        // UI 绑定属性
        public float WinRate { get; private set; }
        public float Progress { get; private set; }
        public string DifficultyText { get; private set; } = "分析中...";
        public bool IsAnalyzing { get; private set; }

        public event Action? PropertyChanged;

        public LevelEditorViewModel()
        {
            _analysisService = new LevelAnalysisService();
            _levelData = new LevelData();
        }

        /// <summary>
        /// 关卡数据变化时调用
        /// </summary>
        public void OnLevelDataChanged(LevelData newData)
        {
            _levelData = newData;

            // 取消当前分析，重新开始
            RestartAnalysis();
        }

        /// <summary>
        /// 重新开始分析
        /// </summary>
        public async void RestartAnalysis()
        {
            // 取消之前的分析
            _currentCts?.Cancel();
            _currentCts?.Dispose();
            _currentCts = new CancellationTokenSource();

            IsAnalyzing = true;
            Progress = 0;
            WinRate = 0;
            DifficultyText = "分析中...";
            PropertyChanged?.Invoke();

            var progress = new Progress<SimulationProgress>(p =>
            {
                // 更新 UI（需要调度到主线程）
                Progress = p.Progress;
                WinRate = p.WinRate;
                DifficultyText = $"通过率: {p.WinRate:P0} ({p.CompletedCount}/{p.TotalCount})";
                PropertyChanged?.Invoke();
            });

            try
            {
                var result = await _analysisService.AnalyzeAsync(
                    _levelData,
                    new AnalysisConfig { SimulationCount = 500, ProgressReportInterval = 50 },
                    progress,
                    _currentCts.Token);

                if (!result.WasCancelled)
                {
                    WinRate = result.WinRate;
                    DifficultyText = $"{result.DifficultyRating} ({result.WinRate:P0})";
                }
            }
            catch (OperationCanceledException)
            {
                // 被取消，忽略
            }
            finally
            {
                IsAnalyzing = false;
                PropertyChanged?.Invoke();
            }
        }

        public void Dispose()
        {
            _currentCts?.Cancel();
            _currentCts?.Dispose();
        }
    }

    /// <summary>
    /// Unity 使用示例（伪代码）
    /// </summary>
    /*
    public class LevelEditorUI : MonoBehaviour
    {
        private LevelAnalysisService _service;
        private CancellationTokenSource _cts;

        [SerializeField] private Text _winRateText;
        [SerializeField] private Slider _progressBar;

        void Start()
        {
            _service = new LevelAnalysisService();
        }

        public async void OnLevelChanged(LevelData data)
        {
            // 取消之前的分析
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // 进度更新（使用 UniTask 调度到主线程）
            var progress = new Progress<SimulationProgress>(async p =>
            {
                await UniTask.SwitchToMainThread();
                _progressBar.value = p.Progress;
                _winRateText.text = $"通过率: {p.WinRate:P1}";
            });

            try
            {
                var result = await _service.AnalyzeAsync(data, progress: progress, cancellationToken: _cts.Token);

                await UniTask.SwitchToMainThread();
                _winRateText.text = $"通过率: {result.WinRate:P1} ({result.DifficultyRating})";
            }
            catch (OperationCanceledException) { }
        }

        void OnDestroy()
        {
            _cts?.Cancel();
        }
    }
    */
}
