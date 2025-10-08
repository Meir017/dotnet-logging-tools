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
    /// <param name="basePercent">Base percentage from project-level progress.</param>
    /// <param name="fileIndex">Zero-based index of the current file.</param>
    /// <param name="totalFiles">Total number of files in the current project.</param>
    /// <param name="fileName">Name of the file being analyzed.</param>
    public void ReportFileProgress(int basePercent, int fileIndex, int totalFiles, string fileName)
    {
        if (!_isEnabled)
        {
            return;
        }

        var filePercent = totalFiles > 0 ? (fileIndex * 100) / totalFiles : 0;
        var percent = basePercent + (filePercent / Math.Max(1, totalFiles));

        try
        {
            _progress!.Report(new LoggerUsageProgress
            {
                PercentComplete = percent,
                OperationDescription = $"Analyzing file {fileIndex + 1} of {totalFiles}",
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
    /// <param name="basePercent">Base percentage from file-level progress.</param>
    /// <param name="analyzerName">Name of the analyzer being executed.</param>
    /// <param name="filePath">Path of the file being analyzed.</param>
    public void ReportAnalyzerProgress(int basePercent, string analyzerName, string? filePath = null)
    {
        if (!_isEnabled)
        {
            return;
        }

        try
        {
            _progress!.Report(new LoggerUsageProgress
            {
                PercentComplete = basePercent,
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
