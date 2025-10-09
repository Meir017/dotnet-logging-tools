using LoggerUsage.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LoggerUsage.Mcp;

/// <summary>
/// Adapter that bridges <see cref="IProgress{T}"/> of <see cref="LoggerUsageProgress"/> 
/// to MCP progress notifications.
/// </summary>
/// <remarks>
/// This adapter converts progress reports from the LoggerUsageExtractor into MCP protocol
/// progress notifications, enabling clients to track analysis progress in real-time.
/// Progress notification errors are caught and logged but do not fail the analysis operation.
/// </remarks>
internal sealed class McpProgressAdapter : IProgress<LoggerUsageProgress>
{
    private readonly McpServer _mcpServer;
    private readonly ProgressToken _progressToken;
    private readonly ILogger<McpProgressAdapter> _logger;
    private int _totalSteps;
    private int _currentStep;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpProgressAdapter"/> class.
    /// </summary>
    /// <param name="mcpServer">The MCP server for sending progress notifications.</param>
    /// <param name="progressToken">The progress token from the client request.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public McpProgressAdapter(
        McpServer mcpServer,
        ProgressToken progressToken,
        ILogger<McpProgressAdapter> logger)
    {
        _mcpServer = mcpServer ?? throw new ArgumentNullException(nameof(mcpServer));
        _progressToken = progressToken;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reports progress by sending an MCP progress notification.
    /// </summary>
    /// <param name="value">The progress information to report.</param>
    /// <remarks>
    /// This method converts the <see cref="LoggerUsageProgress"/> to MCP's <see cref="ProgressNotificationParams"/>
    /// and sends it via the MCP server. Exceptions are caught and logged but not propagated,
    /// ensuring that progress reporting failures do not interrupt the analysis.
    /// </remarks>
    public void Report(LoggerUsageProgress value)
    {
        try
        {
            // Convert percentage-based progress to step-based progress for MCP
            // MCP expects current/total, but LoggerUsageProgress provides PercentComplete
            // We'll infer steps based on changes in percentage
            var newProgress = value.PercentComplete;
            
            // Estimate total and current from percentage (assuming 100 steps for granularity)
            _totalSteps = 100;
            _currentStep = value.PercentComplete;

            var notificationParams = new ProgressNotificationParams
            {
                ProgressToken = _progressToken,
                Progress = new ProgressNotificationValue
                {
                    Progress = _currentStep,
                    Total = _totalSteps,
                    Message = BuildMessage(value)
                }
            };

            // Send notification asynchronously (fire and forget - best effort)
            _ = _mcpServer.SendNotificationAsync("notifications/progress", notificationParams);

            _logger.LogDebug(
                "Progress notification sent: {Current}/{Total} - {Message}",
                _currentStep,
                _totalSteps,
                value.OperationDescription);
        }
        catch (Exception ex)
        {
            // Log but don't throw - progress is best effort
            _logger.LogWarning(
                ex,
                "Failed to send progress notification for progress {Current}/{Total}",
                _currentStep,
                _totalSteps);
        }
    }

    private static string? BuildMessage(LoggerUsageProgress value)
    {
        // Build a meaningful message from available information
        if (!string.IsNullOrEmpty(value.CurrentFilePath))
        {
            var fileName = Path.GetFileName(value.CurrentFilePath);
            return $"{value.OperationDescription}: {fileName}";
        }

        if (!string.IsNullOrEmpty(value.CurrentAnalyzer))
        {
            return $"{value.OperationDescription} ({value.CurrentAnalyzer})";
        }

        return value.OperationDescription;
    }
}
