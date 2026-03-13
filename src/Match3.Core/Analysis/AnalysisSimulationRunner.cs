using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Match3.Core.Analysis;

/// <summary>
/// Reusable parallel simulation runner that eliminates boilerplate across analysis services.
/// Encapsulates the common pattern of running N simulations in parallel (or sequentially),
/// aggregating results, reporting progress, and handling cancellation.
/// </summary>
/// <typeparam name="TWorkItem">The type of each work item (e.g., simulation index, tier+index tuple).</typeparam>
/// <typeparam name="TPerIterationResult">The per-iteration result produced by the simulation function.</typeparam>
/// <typeparam name="TAggregatedResult">The final aggregated result returned to the caller.</typeparam>
internal sealed class AnalysisSimulationRunner<TWorkItem, TPerIterationResult, TAggregatedResult>
{
    private readonly Func<TWorkItem, TPerIterationResult> _simulate;
    private readonly Action<TPerIterationResult> _aggregate;
    private readonly Func<int, int, double, bool, TAggregatedResult> _buildResult;
    private readonly Action<int, int>? _reportProgress;
    private readonly int _progressReportInterval;

    /// <summary>
    /// Creates a new simulation runner with the specified delegates.
    /// </summary>
    /// <param name="simulate">
    /// Function that runs a single simulation for a given work item.
    /// Called from potentially multiple threads; must be thread-safe or use ThreadLocal state.
    /// </param>
    /// <param name="aggregate">
    /// Action that merges a single iteration result into the shared accumulator.
    /// Called under a lock — safe to mutate shared state without additional synchronization.
    /// </param>
    /// <param name="buildResult">
    /// Function that produces the final result from aggregated state.
    /// Parameters: (completedCount, totalCount, elapsedMs, wasCancelled).
    /// </param>
    /// <param name="reportProgress">
    /// Optional action to report progress. Parameters: (completedCount, totalCount).
    /// Called under a lock at the configured interval.
    /// </param>
    /// <param name="progressReportInterval">
    /// Number of completed simulations between progress reports. Defaults to 50.
    /// </param>
    public AnalysisSimulationRunner(
        Func<TWorkItem, TPerIterationResult> simulate,
        Action<TPerIterationResult> aggregate,
        Func<int, int, double, bool, TAggregatedResult> buildResult,
        Action<int, int>? reportProgress = null,
        int progressReportInterval = 50)
    {
        _simulate = simulate ?? throw new ArgumentNullException(nameof(simulate));
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        _buildResult = buildResult ?? throw new ArgumentNullException(nameof(buildResult));
        _reportProgress = reportProgress;
        _progressReportInterval = progressReportInterval > 0 ? progressReportInterval : 50;
    }

    /// <summary>
    /// Runs simulations over a range of integer indices [0, count).
    /// Supports both parallel and sequential execution.
    /// </summary>
    /// <param name="count">Total number of simulations to run.</param>
    /// <param name="indexToWorkItem">Converts an integer index to a work item.</param>
    /// <param name="useParallel">Whether to run in parallel.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The aggregated result.</returns>
    public TAggregatedResult Run(
        int count,
        Func<int, TWorkItem> indexToWorkItem,
        bool useParallel,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        int completedCount = 0;
        int lastReportedCount = 0;
        object lockObj = new object();

        if (useParallel)
        {
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            try
            {
                Parallel.For(0, count, options, i =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var workItem = indexToWorkItem(i);
                    var result = _simulate(workItem);

                    lock (lockObj)
                    {
                        _aggregate(result);
                        completedCount++;

                        TryReportProgress(completedCount, count, ref lastReportedCount);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                return _buildResult(completedCount, count, sw.Elapsed.TotalMilliseconds, true);
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    return _buildResult(completedCount, count, sw.Elapsed.TotalMilliseconds, true);
                }

                var workItem = indexToWorkItem(i);
                var result = _simulate(workItem);

                _aggregate(result);
                completedCount++;

                TryReportProgress(completedCount, count, ref lastReportedCount);
            }
        }

        sw.Stop();

        // Final progress report
        _reportProgress?.Invoke(completedCount, count);

        return _buildResult(completedCount, count, sw.Elapsed.TotalMilliseconds, false);
    }

    /// <summary>
    /// Runs simulations over a pre-built list of work items.
    /// Supports both parallel and sequential execution.
    /// </summary>
    /// <param name="workItems">The list of work items to process.</param>
    /// <param name="useParallel">Whether to run in parallel.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The aggregated result.</returns>
    public TAggregatedResult Run(
        IReadOnlyList<TWorkItem> workItems,
        bool useParallel,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        int total = workItems.Count;
        int completedCount = 0;
        int lastReportedCount = 0;
        object lockObj = new object();

        if (useParallel)
        {
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            try
            {
                Parallel.ForEach(workItems, options, workItem =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = _simulate(workItem);

                    lock (lockObj)
                    {
                        _aggregate(result);
                        completedCount++;

                        TryReportProgress(completedCount, total, ref lastReportedCount);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                return _buildResult(completedCount, total, sw.Elapsed.TotalMilliseconds, true);
            }
        }
        else
        {
            foreach (var workItem in workItems)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    return _buildResult(completedCount, total, sw.Elapsed.TotalMilliseconds, true);
                }

                var result = _simulate(workItem);

                _aggregate(result);
                completedCount++;

                TryReportProgress(completedCount, total, ref lastReportedCount);
            }
        }

        sw.Stop();

        // Final progress report
        _reportProgress?.Invoke(completedCount, total);

        return _buildResult(completedCount, total, sw.Elapsed.TotalMilliseconds, false);
    }

    /// <summary>
    /// Reports progress if enough simulations have completed since the last report.
    /// </summary>
    private void TryReportProgress(int completedCount, int totalCount, ref int lastReportedCount)
    {
        if (_reportProgress != null && completedCount - lastReportedCount >= _progressReportInterval)
        {
            lastReportedCount = completedCount;
            _reportProgress(completedCount, totalCount);
        }
    }
}
