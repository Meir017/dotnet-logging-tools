using System.Text.Json.Serialization;

namespace LoggerUsage.VSCode.Bridge.Models;

/// <summary>
/// Base interface for all bridge responses
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "status")]
[JsonDerivedType(typeof(ReadyResponse), "ready")]
[JsonDerivedType(typeof(AnalysisSuccessResponse), "success")]
[JsonDerivedType(typeof(AnalysisErrorResponse), "error")]
[JsonDerivedType(typeof(AnalysisProgress), "progress")]
public interface IBridgeResponse
{
    string Status { get; }
}

/// <summary>
/// Response indicating bridge is ready (handshake confirmation)
/// </summary>
public record ReadyResponse : IBridgeResponse
{
    public string Status => "ready";
    
    [JsonPropertyName("version")]
    public required string Version { get; init; }
}

/// <summary>
/// Progress update during analysis
/// </summary>
public record AnalysisProgress : IBridgeResponse
{
    public string Status => "progress";
    
    [JsonPropertyName("percentage")]
    public required int Percentage { get; init; }
    
    [JsonPropertyName("message")]
    public required string Message { get; init; }
    
    [JsonPropertyName("currentFile")]
    public string? CurrentFile { get; init; }
}

/// <summary>
/// Successful analysis result
/// </summary>
public record AnalysisSuccessResponse : IBridgeResponse
{
    public string Status => "success";
    
    [JsonPropertyName("result")]
    public required AnalysisResult Result { get; init; }
}

/// <summary>
/// Analysis result data
/// </summary>
public record AnalysisResult
{
    [JsonPropertyName("insights")]
    public required List<LoggingInsightDto> Insights { get; init; }
    
    [JsonPropertyName("summary")]
    public required AnalysisSummaryDto Summary { get; init; }
}

/// <summary>
/// Error response when analysis or command fails
/// </summary>
public record AnalysisErrorResponse : IBridgeResponse
{
    public string Status => "error";
    
    [JsonPropertyName("message")]
    public required string Message { get; init; }
    
    [JsonPropertyName("details")]
    public required string Details { get; init; }
    
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }
}
