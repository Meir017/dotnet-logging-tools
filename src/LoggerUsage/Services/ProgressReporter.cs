using LoggerUsage.Models;

namespace LoggerUsage.Services;

/// <summary>
/// Helper class for calculating and reporting progress efficiently during logger usage extraction.
/// </summary>
internal class ProgressReporter
{
    private readonly IProgress<LoggerUsageProgress>? _progress;
    private readonly bool _isEnabled;
    private readonly object _lock = new();
    private int _completedFiles = 0;
    private int _totalFiles = 0;
    private int _projectIndex = 0;
    private int _totalProjects = 1;
    private long _lastProgressReportTimestamp = 0;
    private const int ProgressReportIntervalMs = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressReporter"/> class.
    /// </summary>
    /// <param name="progress">The progress reporter to use. Can be null to disable progress reporting.</param>
    public ProgressReporter(IProgress<LoggerUsageProgress>? progress)
    {
        _progress = progress;
        _isEnabled = progress != null;
        _lastProgressReportTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Sets the project context for progress calculation.
    /// </summary>
    /// <param name="projectIndex">Zero-based index of the current project.</param>
    /// <param name="totalProjects">Total number of projects to analyze.</param>
    public void SetProjectContext(int projectIndex, int totalProjects)
    {
        lock (_lock)
        {
            _projectIndex = projectIndex;
            _totalProjects = totalProjects;
            _completedFiles = 0;
            _totalFiles = 0;
        }
    }

    /// <summary>
    /// Sets the total number of files to be analyzed in the current project.
    /// </summary>
    /// <param name="totalFiles">Total number of files in the current project.</param>
    public void SetTotalFiles(int totalFiles)
    {
        lock (_lock)
        {
            _totalFiles = totalFiles;
            _completedFiles = 0;
        }
    }

    /// <summary>
    /// Increments the completed file count and reports progress if appropriate (throttled).
    /// This method is thread-safe and can be called from parallel tasks.
    /// </summary>
    /// <param name="fileName">Name of the file that was completed.</param>
    public void IncrementFileProgress(string fileName)
    {
        if (!_isEnabled)
        {
            return;
        }

        int currentCompleted;
        int total;
        int projectIdx;
        int totalProj;
        bool shouldReport;

        lock (_lock)
        {
            _completedFiles++;
            currentCompleted = _completedFiles;
            total = _totalFiles;
            projectIdx = _projectIndex;
            totalProj = _totalProjects;

            // Throttle progress reports (except for first and last)
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_lastProgressReportTimestamp).TotalMilliseconds;
            shouldReport = elapsed >= ProgressReportIntervalMs ||
                          currentCompleted == 1 ||
                          currentCompleted == total;

            if (shouldReport)
            {
                _lastProgressReportTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            }
        }

        if (!shouldReport)
        {
            return;
        }

        // Calculate percentage based on overall progress across all projects
        var projectWeight = 100.0 / Math.Max(1, totalProj);
        var projectBasePercent = projectIdx * projectWeight;
        var fileProgressWithinProject = total > 0 ? ((currentCompleted - 1) / (double)total) : 0;
        var percent = (int)(projectBasePercent + (fileProgressWithinProject * projectWeight));

        // Extract just the filename for cleaner display
        var displayName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = fileName;
        }

        try
        {
            _progress!.Report(new LoggerUsageProgress
            {
                PercentComplete = percent,
                OperationDescription = $"Analyzing {displayName}",
                CurrentFilePath = fileName
            });
        }
        catch
        {
            // Ignore progress reporting errors
        }
    }

    /// <summary>
    /// Reports progress for project-level operations.
    /// </summary>
    /// <param name="projectName">Name of the project being analyzed.</param>
    public void ReportProjectProgress(string projectName)
    {
        if (!_isEnabled)
        {
            return;
        }

        int projectIdx;
        int totalProj;

        lock (_lock)
        {
            projectIdx = _projectIndex;
            totalProj = _totalProjects;
        }

        var percent = totalProj > 0 ? (projectIdx * 100) / totalProj : 0;

        try
        {
            _progress!.Report(new LoggerUsageProgress
            {
                PercentComplete = percent,
                OperationDescription = $"Analyzing project {projectIdx + 1} of {totalProj}: {projectName}"
            });
        }
        catch
        {
            // Ignore progress reporting errors
        }
    }
}
