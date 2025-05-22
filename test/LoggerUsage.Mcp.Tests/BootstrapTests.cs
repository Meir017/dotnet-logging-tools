using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;

namespace LoggerUsage.Mcp.Tests;

public class BootstrapTests
{
    [Fact]
    public async Task Test_ListTools()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var transport = new SseClientTransport(new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost/sse"),
        }, factory.CreateClient());
        var mcpClient = await McpClientFactory.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var response = await mcpClient.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(response);
        Assert.Equal("AnalyzeLoggerUsagesInCsproj", response[0].Name);
    }
}
