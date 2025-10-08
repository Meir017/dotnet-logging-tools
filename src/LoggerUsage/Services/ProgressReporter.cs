using LoggerUsage.Models;

namespace LoggerUsage.Services;

/// <summary>
/// Helper class for calculating and reporting progress efficiently during logger usage extraction.
/// </summary>
internal class ProgressReporter
{
    private readonly IProgress<LoggerUsageProgress>? _progress;
    private readonly bool _isEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressReporter"/> class.
    /// </summary>
    /// <param name="progress">The progress reporter to use. Can be null to disable progress reporting.</param>
    public ProgressReporter(IProgress<LoggerUsageProgress>? progress)
    {
        _progress = progress;
        _isEnabled = progress != null;
    }

    /// <summary>
    /// Reports progress for project-level operations.
    /// </summary>
    /// <param name="projectIndex">Zero-based index of the current project.</param>
    /// <param name="totalProjects">Total number of projects to analyze.</param>
    /// <param name="projectName">Name of the project being analyzed.</param>
    public void ReportProjectProgress(int projectIndex, int totalProjects, string projectName)
    {
        if (!_isEnabled)
        {
            return;
        }

        var percent = totalProjects > 0 ? (projectIndex * 100) / totalProjects : 0;

        try
        {
            _progress!.Report(new LoggerUsageProgress
            {
                PercentComplete = percent,
                OperationDescription = $"Analyzing project {projectIndex + 1} of {totalProjects}: {projectName}"
            });
        }
        catch
        {
            // Ignore progress reporting errors
        }
    }

    /// <summary>
    /// Reports progress for file-level operations within a project.
    /// </summary>
    /// <param name="projectIndex">Zero-based index of the current project.</param>
    /// <param name="totalProjects">Total number of projects to analyze.</param>
    /// <param name="fileIndex">Zero-based index of the current file within the project.</param>
    /// <param name="totalFiles">Total number of files in the current project.</param>
    /// <param name="fileName">Name of the file being analyzed.</param>
    public void ReportFileProgress(int projectIndex, int totalProjects, int fileIndex, int totalFiles, string fileName)
    {
        if (!_isEnabled)
        {
            return;
        }

        // Calculate percentage based on overall progress across all projects
        // Each project gets an equal share of the 100% progress
        var projectWeight = 100.0 / Math.Max(1, totalProjects);
        var projectBasePercent = projectIndex * projectWeight;
        var fileProgressWithinProject = totalFiles > 0 ? (fileIndex / (double)totalFiles) : 0;
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
    /// Reports progress for analyzer-level operations.
    /// </summary>
    /// <param name="projectIndex">Zero-based index of the current project.</param>
    /// <param name="totalProjects">Total number of projects to analyze.</param>
    /// <param name="fileIndex">Zero-based index of the current file within the project.</param>
    /// <param name="totalFiles">Total number of files in the current project.</param>
    /// <param name="analyzerName">Name of the analyzer being executed.</param>
    /// <param name="filePath">Path of the file being analyzed.</param>
    public void ReportAnalyzerProgress(int projectIndex, int totalProjects, int fileIndex, int totalFiles, string analyzerName, string? filePath = null)
    {
        if (!_isEnabled)
        {
            return;
        }

        // Calculate percentage similar to file progress
        var projectWeight = 100.0 / Math.Max(1, totalProjects);
        var projectBasePercent = projectIndex * projectWeight;
        var fileProgressWithinProject = totalFiles > 0 ? (fileIndex / (double)totalFiles) : 0;
        var percent = (int)(projectBasePercent + (fileProgressWithinProject * projectWeight));

        try
        {
            _progress!.Report(new LoggerUsageProgress
            {
                PercentComplete = percent,
                OperationDescription = $"Running analyzer: {analyzerName}",
                CurrentFilePath = filePath,
                CurrentAnalyzer = analyzerName
            });
        }
        catch
        {
            // Ignore progress reporting errors
        }
    }
}
