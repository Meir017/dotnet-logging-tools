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

        for (int i = 0; i < projects.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var project = projects[i];
            reporter.ReportProjectProgress(i, projects.Count, project.Name ?? "Unknown");

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
                cancellationToken);
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
    /// <returns>The extraction results containing all found logger usage information.</returns>
    public async Task<LoggerUsageExtractionResult> ExtractLoggerUsagesWithSolutionAsync(
        Compilation compilation,
        Solution? solution = null,
        IProgress<Models.LoggerUsageProgress>? progress = null,
        CancellationToken cancellationToken = default)
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
            var loggingTypes = new LoggingTypes(compilation, loggerInterface);
            var results = new ConcurrentBag<LoggerUsageInfo>();
            var reporter = new Services.ProgressReporter(progress);

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

            var syntaxTrees = workingCompilation.SyntaxTrees
                .Where(syntaxTree => !syntaxTree.FilePath.EndsWith("LoggerMessage.g.cs"))
                .ToList();

            // Process syntax trees in parallel using async approach
            var syntaxTreeTasks = syntaxTrees.Select(async (syntaxTree, index) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug("Analyzing file {File}", syntaxTree.FilePath);
                reporter.ReportFileProgress(0, index, syntaxTrees.Count, syntaxTree.FilePath);

                var root = syntaxTree.GetRoot();
                var semanticModel = workingCompilation.GetSemanticModel(syntaxTree);

                if (root == null || semanticModel == null)
                {
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
                    reporter.ReportAnalyzerProgress(0, analyzer.GetType().Name, syntaxTree.FilePath);

                    var usages = await analyzer.AnalyzeAsync(analysisContext);

                    var level = usages.Any() ? LogLevel.Information : LogLevel.Debug;
                    var duration = Stopwatch.GetElapsedTime(start);
                    _logger.Log(level, "Analyzer {AnalyzerType} Found {Usages} in file {FilePath} in {Duration}ms", analyzer.GetType().Name, usages.Count(), syntaxTree.FilePath, duration.TotalMilliseconds);

                    return usages;
                });

                var allUsages = await Task.WhenAll(analyzerTasks);

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
