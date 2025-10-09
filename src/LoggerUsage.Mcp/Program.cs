using System.ComponentModel;
using LoggerUsage;
using LoggerUsage.Mcp;
using LoggerUsage.Models;
using LoggerUsage.ReportGenerator;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Read transport configuration
var transportOptions = builder.Configuration
    .GetSection(TransportOptions.SectionName)
    .Get<TransportOptions>() ?? new TransportOptions();

// Log selected transport mode to console (before DI container is built)
Console.WriteLine($"Transport mode configured: {transportOptions.Mode}");

// Validate transport mode - currently only HTTP is supported
if (transportOptions.Mode != TransportMode.Http)
{
    Console.WriteLine($"WARNING: STDIO transport mode is not yet supported by ModelContextProtocol.AspNetCore 0.4.0-preview.1. Falling back to HTTP transport.");
    transportOptions = new TransportOptions { Mode = TransportMode.Http };
}

// Configure MCP server with HTTP transport
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<LoggerUsageExtractorTool>();

builder.Services.AddLoggerUsageExtractor()
    .AddMSBuild();

var app = builder.Build();

// Log after app is built
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("MCP Server starting with transport mode: {TransportMode}", transportOptions.Mode);

app.MapMcp();

app.Run();

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
