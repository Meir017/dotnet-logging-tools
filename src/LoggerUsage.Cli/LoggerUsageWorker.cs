using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LoggerUsage.ReportGenerator;

namespace LoggerUsage.Cli;

public class LoggerUsageWorker(
    LoggerUsageExtractor extractor,
    IOptions<LoggerUsageOptions> options,
    ILogger<LoggerUsageWorker> logger,
    ILoggerReportGeneratorFactory reportGeneratorFactory,
    IWorkspaceFactory workspaceFactory)
{
    private readonly LoggerUsageExtractor _extractor = extractor;
    private readonly LoggerUsageOptions _options = options.Value;
    private readonly ILogger<LoggerUsageWorker> _logger = logger;
    private readonly ILoggerReportGeneratorFactory _reportGeneratorFactory = reportGeneratorFactory;
    private readonly IWorkspaceFactory _workspaceFactory = workspaceFactory;

    public async Task<int> RunAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.Path))
        {
            _logger.LogError("Please provide a path to a csproj or solution file.");
            return -1;
        }

        var path = _options.Path;
        if (!File.Exists(path))
        {
            _logger.LogError("The file '{path}' does not exist.", path);
            return -1;
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Extension != ".csproj" && fileInfo.Extension != ".sln" && fileInfo.Extension != ".slnx")
        {
            _logger.LogError("The file '{path}' is not a csproj, solution, or slnx file.", path);
            return -1;
        }

        using var workspace = await _workspaceFactory.Create(fileInfo);

        // Setup progress reporting if verbose mode is enabled
        IProgress<Models.LoggerUsageProgress>? progress = null;
        ProgressBarHandler? progressBar = null;

        if (_options.Verbose)
        {
            progressBar = new ProgressBarHandler();
            progress = new Progress<Models.LoggerUsageProgress>(progressBar.Report);
        }

        try
        {
            var extractionStart = Stopwatch.GetTimestamp();
            var loggerUsages = await _extractor.ExtractLoggerUsagesAsync(workspace, progress);

            // Clear progress bar before logging results
            progressBar?.Clear();

            _logger.LogInformation("Found {count} logger usages in {duration}ms",
                loggerUsages.Results.Count,
                Stopwatch.GetElapsedTime(extractionStart).TotalMilliseconds);

            if (!string.IsNullOrWhiteSpace(_options.OutputPath))
            {
                var outputPathInfo = new FileInfo(_options.OutputPath);

                _logger.LogInformation("Writing results to '{outputPath}'", _options.OutputPath);
                var generator = _reportGeneratorFactory.GetReportGenerator(outputPathInfo.Extension);
                await File.WriteAllTextAsync(_options.OutputPath, generator.GenerateReport(loggerUsages));
                _logger.LogInformation("Wrote results to '{outputPath}'",
                    _options.OutputPath);
            }
        }
        finally
        {
            // Ensure progress bar is cleared even on error
            progressBar?.Clear();
        }

        return 0;
    }
}
