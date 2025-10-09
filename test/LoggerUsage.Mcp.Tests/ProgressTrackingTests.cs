using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using LoggerUsage.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LoggerUsage.Mcp.Tests;

/// <summary>
/// Tests for MCP progress tracking support in analyze_logger_usages_in_csproj tool.
/// Tests follow TDD: written before implementation, expected to FAIL initially.
/// </summary>
public class ProgressTrackingTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// T003: Contract test - Tool works without progress token (backward compatibility).
    /// Expected: PASS (tool should work without progressToken parameter)
    /// </summary>
    [Fact]
    public async Task Tool_WithoutProgressToken_ReturnsValidResult()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("http://localhost/sse"),
        }, factory.CreateClient());
        var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var response = await mcpClient.CallToolAsync("analyze_logger_usages_in_csproj",
            new Dictionary<string, object?>
            {
                { "fullPathToCsproj", GetTestCsprojPath() }
                // No progressToken provided - this should work (backward compatibility)
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Content.Should().HaveCount(1);
        var text = response.Content[0].Should().BeOfType<TextContentBlock>().Which;
        
        var loggerUsages = JsonSerializer.Deserialize<LoggerUsageExtractionResult>(text.Text!, _jsonOptions);
        loggerUsages.Should().NotBeNull();
        loggerUsages!.Results.Should().NotBeNull();
    }

    /// <summary>
    /// T004: Contract test - Tool accepts progress token parameter.
    /// Expected: FAIL initially (parameter doesn't exist yet)
    /// </summary>
    [Fact]
    public async Task Tool_WithProgressToken_AcceptsParameter()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("http://localhost/sse"),
        }, factory.CreateClient());
        var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);

        // Act - This will FAIL initially because progressToken parameter doesn't exist
        var response = await mcpClient.CallToolAsync("analyze_logger_usages_in_csproj",
            new Dictionary<string, object?>
            {
                { "fullPathToCsproj", GetTestCsprojPath() },
                { "progressToken", "test-123" } // This parameter doesn't exist yet!
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        var text = response.Content[0].Should().BeOfType<TextContentBlock>().Which;
        var loggerUsages = JsonSerializer.Deserialize<LoggerUsageExtractionResult>(text.Text!, _jsonOptions);
        loggerUsages.Should().NotBeNull();
    }

    /// <summary>
    /// T005-T008: Progress notification tests (skipped until client SDK supports progress handlers).
    /// These tests require understanding how the MCP C# client SDK handles progress notifications.
    /// Will be implemented after core functionality is working.
    /// </summary>
    [Fact(Skip = "Requires MCP client progress notification handling - implement after adapter created")]
    public async Task ProgressNotifications_AreSentWhenTokenProvided()
    {
        // TODO: Implement after understanding MCP client SDK progress notification API
        // The server-side implementation (adapter) can be tested independently
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires MCP client progress notification handling - implement after adapter created")]
    public async Task ProgressValues_AreCorrectAndMonotonicallyIncreasing()
    {
        // TODO: Implement after understanding MCP client SDK progress notification API
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires MCP client progress notification handling - implement after adapter created")]
    public async Task SingleFileAnalysis_ShowsCorrectProgress()
    {
        // TODO: Implement after understanding MCP client SDK progress notification API
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires ability to mock IMcpServer - implement after adapter created")]
    public async Task ProgressNotificationFailure_DoesNotFailAnalysis()
    {
        // TODO: Implement test with mocked IMcpServer that throws exceptions
        await Task.CompletedTask;
    }

    #region Helper Methods

    private static string GetTestCsprojPath()
    {
        var gitRoot = FindGitRoot();
        return Path.Combine(gitRoot, "src", "LoggerUsage.Mcp", "LoggerUsage.Mcp.csproj");
    }

    private static string FindGitRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        while (currentDirectory != null && !Directory.Exists(Path.Combine(currentDirectory, ".git")))
        {
            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }
        return currentDirectory ?? throw new InvalidOperationException("Git root not found");
    }

    #endregion
}
