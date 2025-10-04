using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoggerUsage.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LoggerUsage.Mcp.Tests;

public class IntegrationTests
{
    [Fact]
    public async Task Test_ListTools()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("http://localhost/sse"),
        }, factory.CreateClient());
        var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var response = await mcpClient.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(response);
        Assert.Equal("analyze_logger_usages_in_csproj", response[0].Name);
    }

    [Fact]
    public async Task Test_AnalyzeLoggerUsagesInCsproj()
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
                { "fullPathToCsproj", GetCliCsprojPath() }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Single(response.Content);
        Assert.Equal("text", response.Content[0].Type);
        var text = Assert.IsType<TextContentBlock>(response.Content[0]);
        Assert.NotNull(text.Text);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var loggerUsages = JsonSerializer.Deserialize<LoggerUsageExtractionResult>(text.Text!, options);
        Assert.NotNull(loggerUsages);
        Assert.NotNull(loggerUsages.Results);
        Assert.NotEmpty(loggerUsages.Results);
        Assert.NotNull(loggerUsages.Summary);
    }

    private static string GetCliCsprojPath()
    {
        var gitRoot = FindGitRoot();
        return Path.Combine(gitRoot, "src", "LoggerUsage.Mcp", "LoggerUsage.Mcp.csproj");

        static string FindGitRoot()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            while (currentDirectory != null && !Directory.Exists(Path.Combine(currentDirectory, ".git")))
            {
                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }
            return currentDirectory ?? throw new InvalidOperationException("Git root not found");
        }
    }
}
