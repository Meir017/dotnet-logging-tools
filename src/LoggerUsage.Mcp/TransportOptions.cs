namespace LoggerUsage.Mcp;

/// <summary>
/// Defines the available MCP server transport mechanisms.
/// </summary>
public enum TransportMode
{
    /// <summary>
    /// HTTP transport.
    /// </summary>
    Http = 0,

    /// <summary>
    /// Standard Input/Output transport.
    /// </summary>
    Stdio = 1
}

/// <summary>
/// Configuration options for MCP server transport.
/// </summary>
public class TransportOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Transport";

    /// <summary>
    /// Gets or sets the transport mode. Defaults to <see cref="TransportMode.Stdio"/>.
    /// </summary>
    public TransportMode Mode { get; set; } = TransportMode.Stdio;
}
