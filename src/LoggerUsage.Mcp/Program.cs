using System.ComponentModel;
using LoggerUsage;
using LoggerUsage.Models;
using LoggerUsage.ReportGenerator;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<LoggerUsageExtractorTool>();

builder.Services.AddLoggerUsageExtractor()
    .AddMSBuild();

var app = builder.Build();

app.MapMcp();

app.Run();

[McpServerToolType]
public class LoggerUsageExtractorTool(
    ILogger<LoggerUsageExtractorTool> logger,
    IWorkspaceFactory workspaceFactory,
    LoggerUsageExtractor loggerUsageExtractor,
    ILoggerReportGeneratorFactory loggerReportGeneratorFactory)
{
    [McpServerTool(Name = "analyze_logger_usages_in_csproj")]
    [Description("Analyze logger usages of C# files in a csproj file.")]
    public async Task<LoggerUsageExtractionResult> AnalyzeLoggerUsagesInCsproj(
        string fullPathToCsproj)
    {
        using var workspace = await workspaceFactory.Create(new FileInfo(fullPathToCsproj));

        var loggerUsage = await loggerUsageExtractor.ExtractLoggerUsagesAsync(workspace);
        var reportGenerator = loggerReportGeneratorFactory.GetReportGenerator(".json");
        var report = reportGenerator.GenerateReport(loggerUsage);

        logger.LogInformation("Report generated successfully.");
        return loggerUsage;
    }
}
