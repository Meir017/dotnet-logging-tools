using System.Text.Json;
using LoggerUsage.VSCode.Bridge;
using LoggerUsage.VSCode.Bridge.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Setup dependency injection
var services = new ServiceCollection();

// Add logging (minimal - we use stdio for communication)
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Warning);
});

// Add LoggerUsage services
services.AddLoggerUsageExtractor()
    .AddMSBuild();

// Register WorkspaceAnalyzer
services.AddSingleton<WorkspaceAnalyzer>();

var serviceProvider = services.BuildServiceProvider();

// Get the analyzer
var analyzer = serviceProvider.GetRequiredService<WorkspaceAnalyzer>();

// JSON serializer options
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
};

// Main loop - read commands from stdin, write responses to stdout
while (true)
{
    try
    {
        // Read line from stdin
        var line = await Console.In.ReadLineAsync();

        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        // Try to deserialize the request
        IBridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<IBridgeRequest>(line, jsonOptions);
        }
        catch (JsonException ex)
        {
            // Send error response for invalid JSON
            var errorResponse = new AnalysisErrorResponse
            {
                Message = "Invalid JSON command",
                Details = $"Failed to parse JSON: {ex.Message}",
                ErrorCode = "INVALID_JSON"
            };

            WriteResponse(errorResponse);
            continue;
        }

        if (request == null)
        {
            var errorResponse = new AnalysisErrorResponse
            {
                Message = "Null request",
                Details = "Deserialized request was null",
                ErrorCode = "NULL_REQUEST"
            };

            WriteResponse(errorResponse);
            continue;
        }

        // Route the command
        IBridgeResponse response = request switch
        {
            PingRequest => new ReadyResponse
            {
                Version = "1.0.0"
            },

            AnalysisRequest analysisRequest => await analyzer.AnalyzeWorkspaceAsync(
                analysisRequest,
                CancellationToken.None),

            IncrementalAnalysisRequest incrementalRequest => await analyzer.AnalyzeFileAsync(
                incrementalRequest,
                CancellationToken.None),

            ShutdownRequest => null!, // Signal to exit

            _ => new AnalysisErrorResponse
            {
                Message = "Unknown command",
                Details = $"Command type not recognized: {request.Command}",
                ErrorCode = "UNKNOWN_COMMAND"
            }
        };

        // Handle shutdown
        if (request is ShutdownRequest)
        {
            break;
        }

        // Write response to stdout
        WriteResponse(response);
    }
    catch (Exception ex)
    {
        // Handle unexpected errors
        var errorResponse = new AnalysisErrorResponse
        {
            Message = "Internal error",
            Details = $"Unexpected error during command processing: {ex.Message}\n{ex.StackTrace}",
            ErrorCode = "INTERNAL_ERROR"
        };

        WriteResponse(errorResponse);
    }
}

// Helper method to write response as JSON to stdout
void WriteResponse(IBridgeResponse response)
{
    try
    {
        var json = JsonSerializer.Serialize(response, jsonOptions);
        Console.WriteLine(json);
    }
    catch (Exception ex)
    {
        // Last resort error handling - write raw error
        Console.Error.WriteLine($"Failed to serialize response: {ex.Message}");
    }
}
