using System.Diagnostics;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LoggerUsage.Cli.ReportGenerator;

namespace LoggerUsage.Cli;

public partial class LoggerUsageWorker(
    LoggerUsageExtractor extractor,
    IOptions<LoggerUsageOptions> options,
    ILogger<LoggerUsageWorker> logger,
    ILoggerReportGeneratorFactory reportGeneratorFactory)
{
    private readonly LoggerUsageExtractor _extractor = extractor;
    private readonly LoggerUsageOptions _options = options.Value;
    private readonly ILogger<LoggerUsageWorker> _logger = logger;
    private readonly ILoggerReportGeneratorFactory _reportGeneratorFactory = reportGeneratorFactory;

    static LoggerUsageWorker()
    {
        MSBuildLocator.RegisterDefaults();
    }

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

        using var workspace = MSBuildWorkspace.Create();
        if (fileInfo.Extension == ".sln" || fileInfo.Extension == ".slnx")
        {
            var start = Stopwatch.GetTimestamp();
            LogInfoLoadingSolution(_logger, path);
            var solution = await workspace.OpenSolutionAsync(path);
            _logger.LogInformation("Loaded solution '{path}' with {count} projects in {duration}ms", solution.FilePath, solution.Projects.Count(), Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        else if (fileInfo.Extension == ".csproj")
        {
            var start = Stopwatch.GetTimestamp();
            LogInfoLoadingProject(_logger, path);
            var project = await workspace.OpenProjectAsync(path);
            _logger.LogInformation("Loaded project '{path}' with {count} documents in {duration}ms", project.FilePath, project.Documents.Count(), Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        var extractionStart = Stopwatch.GetTimestamp();
        var results = await _extractor.ExtractLoggerUsagesAsync(workspace);
        _logger.LogInformation("Found {count} logger usages in {duration}ms", results.Count, Stopwatch.GetElapsedTime(extractionStart).TotalMilliseconds);

        if (!string.IsNullOrWhiteSpace(_options.OutputPath))
        {
            _logger.LogInformation("Writing results to '{outputPath}'", _options.OutputPath);
            var generator = _reportGeneratorFactory.GetReportGenerator(_options.OutputPath);
            await File.WriteAllTextAsync(_options.OutputPath, generator.GenerateReport(results));
            _logger.LogInformation("Wrote results to '{outputPath}'", 
                _options.OutputPath);
        }

        return 0;
    }

    [LoggerMessage(
        Level = LogLevel.Information, 
        Message = "Loading solution '{Path}'"
    )]
    private static partial void LogInfoLoadingSolution(ILogger logger, string path);

    [LoggerMessage(
        Level = LogLevel.Information, 
        Message = "Loading project '{Path}'"
    )]
    private static partial void LogInfoLoadingProject(ILogger logger, string path);
}
