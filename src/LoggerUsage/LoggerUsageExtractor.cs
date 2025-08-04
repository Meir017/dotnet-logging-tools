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
    /// <returns>A task that represents the asynchronous operation, containing the extraction results.</returns>
    public async Task<LoggerUsageExtractionResult> ExtractLoggerUsagesAsync(Workspace workspace)
    {
        var results = new List<LoggerUsageInfo>();

        foreach (var project in workspace.CurrentSolution.Projects)
        {
            if (project.Language != LanguageNames.CSharp)
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                continue;
            }

            _logger.LogInformation("Analyzing project compilation '{Project}' with {Count} references", compilation.AssemblyName, compilation.References.Count());

            var extractionResult = ExtractLoggerUsages(compilation);
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
    /// Extracts logger usage information from a single compilation unit.
    /// </summary>
    /// <param name="compilation">The compilation to analyze for logger usage patterns.</param>
    /// <returns>The extraction results containing all found logger usage information.</returns>
    public LoggerUsageExtractionResult ExtractLoggerUsages(Compilation compilation)
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

        var loggingTypes = new LoggingTypes(compilation, loggerInterface);
        var results = new ConcurrentBag<LoggerUsageInfo>();

        Parallel.ForEach(compilation.SyntaxTrees, syntaxTree =>
        {
            if (syntaxTree.FilePath.EndsWith("LoggerMessage.g.cs"))
            {
                return;
            }

            _logger.LogDebug("Analyzing file {File}", syntaxTree.FilePath);

            var root = syntaxTree.GetRoot();
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            if (root == null || semanticModel == null)
            {
                return;
            }

            foreach (var analyzer in _analyzers)
            {
                var start = Stopwatch.GetTimestamp();
                _logger.LogDebug("Running Analyzer {AnalyzerType} on file {File}", analyzer.GetType().Name, syntaxTree.FilePath);
                var usages = analyzer.Analyze(loggingTypes, root, semanticModel);
                var level = usages.Any() ? LogLevel.Information : LogLevel.Debug;
                var duration = Stopwatch.GetElapsedTime(start);
                _logger.Log(level, "Analyzer {AnalyzerType} Found {Usages} in file {FilePath} in {Duration}ms", analyzer.GetType().Name, usages.Count(), syntaxTree.FilePath, duration.TotalMilliseconds);

                foreach (var usage in usages)
                {
                    results.Add(usage);
                }
            }
        });

        // TODO: Populate summary.ParameterTypesByName from results

        return new LoggerUsageExtractionResult
        {
            Results = [.. results],
            Summary = new()
        };
    }
}
