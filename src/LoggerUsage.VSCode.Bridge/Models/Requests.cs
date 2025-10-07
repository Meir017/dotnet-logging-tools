using System.Text.Json.Serialization;

namespace LoggerUsage.VSCode.Bridge.Models;

/// <summary>
/// Base interface for all bridge requests
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "command")]
[JsonDerivedType(typeof(PingRequest), "ping")]
[JsonDerivedType(typeof(AnalysisRequest), "analyze")]
[JsonDerivedType(typeof(IncrementalAnalysisRequest), "analyzeFile")]
[JsonDerivedType(typeof(ShutdownRequest), "shutdown")]
public interface IBridgeRequest
{
    // Note: 'command' is automatically added by JsonPolymorphic as the discriminator
}

/// <summary>
/// Request to verify bridge is ready (handshake)
/// </summary>
public record PingRequest : IBridgeRequest
{
}

/// <summary>
/// Request to analyze an entire workspace/solution
/// </summary>
public record AnalysisRequest : IBridgeRequest
{
    [JsonPropertyName("workspacePath")]
    public required string WorkspacePath { get; init; }

    [JsonPropertyName("solutionPath")]
    public string? SolutionPath { get; init; }

    [JsonPropertyName("excludePatterns")]
    public string[]? ExcludePatterns { get; init; }
}

/// <summary>
/// Request to re-analyze a single file (incremental)
/// </summary>
public record IncrementalAnalysisRequest : IBridgeRequest
{
    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    [JsonPropertyName("solutionPath")]
    public required string SolutionPath { get; init; }
}

/// <summary>
/// Request to gracefully shutdown the bridge process
/// </summary>
public record ShutdownRequest : IBridgeRequest
{
}
