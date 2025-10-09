using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;
using LoggerUsage.Analyzers;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace LoggerUsage;

/// <summary>
/// Extracts logger usage information from C# source code using Roslyn analyzers.
/// </summary>
/// <param name="analyzers">Collection of analyzers to use for extracting logger usage patterns.</param>
/// <param name="logger">Logger instance for diagnostics and information logging.</param>
public class LoggerUsageExtractor(IEnumerable<ILoggerUsageAnalyzer> analyzers, ILogger<LoggerUsageExtractor> logger)
{
    private readonly IEnumerable<ILoggerUsageAnalyzer> _analyzers = analyzers;
    private readonly ILogger<LoggerUsageExtractor> _logger = logger;

    /// <summary>
    /// Asynchronously extracts logger usage information from all projects in the specified workspace.
    /// </summary>
    /// <param name="workspace">The workspace containing the projects to analyze.</param>
    /// <param name="progress">Optional progress reporter for tracking analysis progress.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the extraction results.</returns>
    public async Task<LoggerUsageExtractionResult> ExtractLoggerUsagesAsync(
        Workspace workspace,
        IProgress<Models.LoggerUsageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<LoggerUsageInfo>();
        var projects = workspace.CurrentSolution.Projects
            .Where(p => p.Language == LanguageNames.CSharp)
            .ToList();

        var reporter = new Services.ProgressReporter(progress);

        // Initialize project context for progress tracking
        reporter.SetProjectContext(0, projects.Count);

        for (int i = 0; i < projects.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var project = projects[i];

            // Update project context for this iteration
            reporter.SetProjectContext(i, projects.Count);
            reporter.ReportProjectProgress(project.Name ?? "Unknown");

            if (project.Language != LanguageNames.CSharp)
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
            {
                continue;
            }

            _logger.LogInformation("Analyzing project compilation '{Project}' with {Count} references", compilation.AssemblyName, compilation.References.Count());

            var extractionResult = await ExtractLoggerUsagesWithSolutionAsync(
                compilation,
                workspace.CurrentSolution,
                progress,
                cancellationToken,
                projectIndex: i,
                totalProjects: projects.Count);
            results.AddRange(extractionResult.Results);
        }

        // TODO: Populate summary.ParameterTypesByName from results

        var result = new LoggerUsageExtractionResult
        {
            Results = results,
            Summary = new()
        };
        new LoggerUsageSummarizer().PopulateSummary(result);
        return result;
    }

    /// <summary>
    /// Extracts logger usage information from a single compilation unit with optional solution context.
    /// </summary>
    /// <param name="compilation">The compilation to analyze for logger usage patterns.</param>
    /// <param name="solution">Optional solution for cross-project analysis. If null, an AdhocWorkspace will be created.</param>
    /// <param name="progress">Optional progress reporter for tracking analysis progress.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <param name="projectIndex">Zero-based index of the current project (for progress calculation when called from workspace analysis).</param>
    /// <param name="totalProjects">Total number of projects being analyzed (for progress calculation when called from workspace analysis).</param>
    /// <returns>The extraction results containing all found logger usage information.</returns>
    public async Task<LoggerUsageExtractionResult> ExtractLoggerUsagesWithSolutionAsync(
        Compilation compilation,
        Solution? solution = null,
        IProgress<Models.LoggerUsageProgress>? progress = null,
        CancellationToken cancellationToken = default,
        int projectIndex = 0,
        int totalProjects = 1)
    {
        var loggerInterface = compilation.GetTypeByMetadataName(typeof(ILogger).FullName!)!;
        if (loggerInterface == null)
        {
            _logger.LogWarning("ILogger interface not found in compilation '{CompilationPath}'. Skipping analysis. Existing {Count} references [{References}]",
                compilation.SourceModule.Name,
                compilation.References.Count(),
                string.Join(',', compilation.References.Select(r => r.Display)));
            return new LoggerUsageExtractionResult();
        }

        // Ensure we have a solution (create AdhocWorkspace if needed)
        if (solution == null && progress != null)
        {
            progress.Report(new Models.LoggerUsageProgress
            {
                PercentComplete = 0,
                OperationDescription = "Creating AdhocWorkspace for compilation"
            });
        }

        var (ensuredSolution, workspace) = await Utilities.WorkspaceHelper.EnsureSolutionAsync(
            compilation,
            solution,
            _logger);

        try
        {
            // If we created an AdhocWorkspace, use its compilation to ensure symbol matching for SymbolFinder
            Compilation workingCompilation = compilation;
            if (workspace != null && ensuredSolution != null)
            {
                var project = ensuredSolution.Projects.FirstOrDefault();
                if (project != null)
                {
                    workingCompilation = await project.GetCompilationAsync(cancellationToken) ?? compilation;
                    _logger.LogDebug("Using workspace compilation with {TreeCount} trees for symbol resolution", workingCompilation.SyntaxTrees.Count());
                }
            }

            // Create LoggingTypes from the working compilation (which may be from AdhocWorkspace)
            var workingLoggerInterface = workingCompilation.GetTypeByMetadataName(typeof(ILogger).FullName!)!;
            if (workingLoggerInterface == null)
            {
                _logger.LogWarning("ILogger interface not found in working compilation. Skipping analysis.");
                return new LoggerUsageExtractionResult();
            }

            var loggingTypes = new LoggingTypes(workingCompilation, workingLoggerInterface);
            var results = new ConcurrentBag<LoggerUsageInfo>();
            var reporter = new Services.ProgressReporter(progress);

            var syntaxTrees = workingCompilation.SyntaxTrees
                .Where(syntaxTree => !syntaxTree.FilePath.EndsWith("LoggerMessage.g.cs"))
                .ToList();

            // Set total files for progress tracking
            reporter.SetTotalFiles(syntaxTrees.Count);

            // Process syntax trees in parallel using async approach
            var syntaxTreeTasks = syntaxTrees.Select(async syntaxTree =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug("Analyzing file {File}", syntaxTree.FilePath);

                var root = syntaxTree.GetRoot();
                var semanticModel = workingCompilation.GetSemanticModel(syntaxTree);

                if (root == null || semanticModel == null)
                {
                    // Still increment progress even if we skip the file
                    reporter.IncrementFileProgress(syntaxTree.FilePath);
                    return;
                }

                var analysisContext = ensuredSolution != null
                    ? LoggingAnalysisContext.CreateForWorkspace(loggingTypes, root, semanticModel, ensuredSolution, _logger)
                    : LoggingAnalysisContext.CreateForCompilation(loggingTypes, root, semanticModel, _logger);

                // Run all analyzers in parallel for this syntax tree
                var analyzerTasks = _analyzers.Select(async analyzer =>
                {
                    var start = Stopwatch.GetTimestamp();
                    _logger.LogDebug("Running Analyzer {AnalyzerType} on file {File}", analyzer.GetType().Name, syntaxTree.FilePath);
                    // Note: We don't report analyzer progress to avoid clutter - only log it

                    var usages = await analyzer.AnalyzeAsync(analysisContext);

                    var level = usages.Any() ? LogLevel.Information : LogLevel.Debug;
                    var duration = Stopwatch.GetElapsedTime(start);
                    _logger.Log(level, "Analyzer {AnalyzerType} Found {Usages} in file {FilePath} in {Duration}ms", analyzer.GetType().Name, usages.Count(), syntaxTree.FilePath, duration.TotalMilliseconds);

                    return usages;
                });

                var allUsages = await Task.WhenAll(analyzerTasks);

                // Report progress after completing this file (thread-safe, throttled internally)
                reporter.IncrementFileProgress(syntaxTree.FilePath);

                foreach (var usages in allUsages)
                {
                    foreach (var usage in usages)
                    {
                        results.Add(usage);
                    }
                }
            });

            await Task.WhenAll(syntaxTreeTasks);

            // TODO: Populate summary.ParameterTypesByName from results

            return new LoggerUsageExtractionResult
            {
                Results = [.. results],
                Summary = new()
            };
        }
        finally
        {
            workspace?.Dispose();
        }
    }
}
