using System.ComponentModel;
using LoggerUsage;
using LoggerUsage.Mcp;
using LoggerUsage.Models;
using LoggerUsage.ReportGenerator;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

IHostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

var transportOptions = builder.Configuration
    .GetSection(TransportOptions.SectionName)
    .Get<TransportOptions>() ?? new TransportOptions();

builder = transportOptions.Mode switch
{
    TransportMode.Http => WebApplication.CreateBuilder(args),
    TransportMode.Stdio => builder,
    _ => throw new NotSupportedException($"Unsupported transport mode: {transportOptions.Mode}")
};

builder.Services.AddLoggerUsageExtractor().AddMSBuild();

var mcp = builder.Services.AddMcpServer()
    .WithTools<LoggerUsageExtractorTool>();

IHost app = null!;
if (transportOptions.Mode is TransportMode.Stdio)
{
    mcp.WithStdioServerTransport();
    app = ((HostApplicationBuilder)builder).Build();
}
else if (transportOptions.Mode is TransportMode.Http)
{
    mcp.WithHttpTransport();
    app = ((WebApplicationBuilder)builder).Build();
    ((WebApplication)app).MapMcp();
}

await app.RunAsync();

[McpServerToolType]
public class LoggerUsageExtractorTool(
    ILogger<LoggerUsageExtractorTool> logger,
    IWorkspaceFactory workspaceFactory,
    LoggerUsageExtractor loggerUsageExtractor,
    ILoggerReportGeneratorFactory loggerReportGeneratorFactory,
    McpServer mcpServer,
    ILoggerFactory loggerFactory)
{
    /// <summary>
    /// Analyzes logger usages in a C# project file (.csproj).
    /// </summary>
    /// <param name="fullPathToCsproj">The absolute path to the .csproj file to analyze.</param>
    /// <param name="progressToken">Optional progress token for tracking analysis progress. When provided, the server will send progress notifications during analysis.</param>
    /// <returns>A task that represents the asynchronous operation, containing the logger usage extraction results.</returns>
    [McpServerTool(Name = "analyze_logger_usages_in_csproj")]
    [Description("Analyze logger usages of C# files in a csproj file. Extracts logging patterns, custom tag names, tag providers, data classifications, and transitive properties from LogProperties parameters.")]
    public async Task<LoggerUsageExtractionResult> AnalyzeLoggerUsagesInCsproj(
        string fullPathToCsproj,
        ProgressToken? progressToken = null)
    {
        using var workspace = await workspaceFactory.Create(new FileInfo(fullPathToCsproj));

        // Create progress adapter if progress token provided
        IProgress<LoggerUsageProgress>? progress = null;
        if (progressToken is not null)
        {
            progress = new McpProgressAdapter(
                mcpServer,
                progressToken.Value,
                loggerFactory.CreateLogger<McpProgressAdapter>());
        }

        var loggerUsage = await loggerUsageExtractor.ExtractLoggerUsagesAsync(workspace, progress);
        var reportGenerator = loggerReportGeneratorFactory.GetReportGenerator(".json");
        var report = reportGenerator.GenerateReport(loggerUsage);

        logger.LogInformation("Report generated successfully.");
        return loggerUsage;
    }
}
