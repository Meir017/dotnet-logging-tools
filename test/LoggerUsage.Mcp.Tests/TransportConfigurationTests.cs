using LoggerUsage.Mcp;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LoggerUsage.Mcp.Tests;

public class TransportConfigurationTests
{
    [Fact]
    public void ServerStartup_WithNoTransportConfig_DefaultsToHttp()
    {
        // Arrange: No transport configuration provided

        // Act: Read configuration
        var transportOptions = new TransportOptions();

        // Assert: Should default to HTTP
        Assert.Equal(TransportMode.Http, transportOptions.Mode);
    }

    [Fact]
    public void ConfigurationBinding_WithHttpMode_ParsesCorrectly()
    {
        // Arrange: Configuration with Http mode
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Transport:Mode"] = "Http"
            })
            .Build();

        // Act: Bind configuration
        var transportOptions = configuration
            .GetSection(TransportOptions.SectionName)
            .Get<TransportOptions>();

        // Assert: Should be Http
        Assert.NotNull(transportOptions);
        Assert.Equal(TransportMode.Http, transportOptions.Mode);
    }

    [Fact]
    public void ConfigurationBinding_WithStdioMode_ParsesCorrectly()
    {
        // Arrange: Configuration with Stdio mode
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Transport:Mode"] = "Stdio"
            })
            .Build();

        // Act: Bind configuration
        var transportOptions = configuration
            .GetSection(TransportOptions.SectionName)
            .Get<TransportOptions>();

        // Assert: Should be Stdio
        Assert.NotNull(transportOptions);
        Assert.Equal(TransportMode.Stdio, transportOptions.Mode);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("WebSocket")]
    [InlineData("")]
    public void ConfigurationBinding_WithInvalidMode_ThrowsException(string invalidMode)
    {
        // Arrange: Configuration with invalid mode
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Transport:Mode"] = invalidMode
            })
            .Build();

        // Act & Assert: Should throw InvalidOperationException when binding
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            configuration
                .GetSection(TransportOptions.SectionName)
                .Get<TransportOptions>();
        });

        // Verify exception message indicates configuration error
        Assert.Contains("Transport:Mode", exception.Message);
    }

    [Theory]
    [InlineData("http", TransportMode.Http)]
    [InlineData("HTTP", TransportMode.Http)]
    [InlineData("Http", TransportMode.Http)]
    [InlineData("stdio", TransportMode.Stdio)]
    [InlineData("STDIO", TransportMode.Stdio)]
    [InlineData("Stdio", TransportMode.Stdio)]
    public void ConfigurationBinding_WithVariousCasing_ParsesCorrectly(
        string modeString,
        TransportMode expectedMode)
    {
        // Arrange: Configuration with various casing
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Transport:Mode"] = modeString
            })
            .Build();

        // Act: Bind configuration
        var transportOptions = configuration
            .GetSection(TransportOptions.SectionName)
            .Get<TransportOptions>();

        // Assert: Should parse correctly regardless of case
        Assert.NotNull(transportOptions);
        Assert.Equal(expectedMode, transportOptions.Mode);
    }

    [Fact]
    public void ConfigurationPriority_CommandLineOverridesAppSettings()
    {
        // Arrange: appsettings.json says Http, command-line says Stdio
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Transport:Mode"] = "Http"  // Base config
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Transport:Mode"] = "Stdio"  // Command-line override
            })
            .Build();

        // Act: Bind configuration
        var transportOptions = configuration
            .GetSection(TransportOptions.SectionName)
            .Get<TransportOptions>();

        // Assert: Command-line should win
        Assert.NotNull(transportOptions);
        Assert.Equal(TransportMode.Stdio, transportOptions.Mode);
    }
}
