# Transport Configuration Usage Guide

## Overview

The LoggerUsage.Mcp server supports two transport modes for the Model Context Protocol:

- **HTTP Transport**: Server runs as a web application with HTTP+SSE endpoints (default)
- **STDIO Transport**: Server communicates via standard input/output streams

## Configuration Options

### 1. Via Configuration Files

**appsettings.json** (Production/Default):

```json
{
  "Transport": {
    "Mode": "Http"
  }
}
```

**appsettings.Development.json** (Development Environment):

```json
{
  "Transport": {
    "Mode": "Stdio"
  }
}
```

### 2. Via Command-Line Arguments

Override configuration files at runtime:

```bash
# Force HTTP transport
dotnet run --project src/LoggerUsage.Mcp -- --Transport:Mode=Http

# Force STDIO transport
dotnet run --project src/LoggerUsage.Mcp -- --Transport:Mode=Stdio
```

### 3. Via Environment Variables

```bash
# Windows
set Transport__Mode=Stdio
dotnet run --project src/LoggerUsage.Mcp

# Linux/Mac
export Transport__Mode=Stdio
dotnet run --project src/LoggerUsage.Mcp
```

## Transport Modes

### HTTP Transport (Default)

**When to use:**

- Connecting from web-based MCP clients
- Remote connections over network
- Multiple concurrent clients
- Browser-based AI applications

**Characteristics:**

- Runs as ASP.NET Core web application
- Uses Kestrel web server
- HTTP+SSE (Server-Sent Events) protocol
- Default endpoint: `http://localhost:5000/sse`

**Example usage:**

```bash
# Runs with HTTP transport (production default)
dotnet run --project src/LoggerUsage.Mcp
```

**Client connection:**

```csharp
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("http://localhost:5000/sse")
}, httpClient);
var mcpClient = await McpClient.CreateAsync(transport);
```

### STDIO Transport

**When to use:**

- Local development with VS Code or other IDEs
- Command-line tools and scripts
- Single client, direct process communication
- MCP clients that spawn server processes

**Characteristics:**

- Runs as console application
- Reads from stdin, writes to stdout
- Lower latency than HTTP
- Automatically selected in Development environment

**Example usage:**

```bash
# Runs with STDIO transport (Development default)
dotnet run --project src/LoggerUsage.Mcp --environment Development

# Or explicitly force STDIO
dotnet run --project src/LoggerUsage.Mcp -- --Transport:Mode=Stdio
```

**Client connection:**

The MCP client spawns the server process and communicates via stdin/stdout:

```csharp
var processInfo = new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = "run --project src/LoggerUsage.Mcp -- --Transport:Mode=Stdio",
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    UseShellExecute = false
};
var process = Process.Start(processInfo);
var transport = new StdioTransport(process.StandardOutput.BaseStream, 
                                    process.StandardInput.BaseStream);
var mcpClient = await McpClient.CreateAsync(transport);
```

## Configuration Priority

Configuration sources are applied in the following order (later sources override earlier):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables
4. Command-line arguments

**Example:**

```bash
# appsettings.json has Http, but command-line overrides to Stdio
dotnet run --project src/LoggerUsage.Mcp -- --Transport:Mode=Stdio
```

## Validation

Invalid transport mode values will cause the application to fail at startup:

```bash
dotnet run --project src/LoggerUsage.Mcp -- --Transport:Mode=WebSocket
# Result: System.NotSupportedException: Unsupported transport mode: WebSocket
```

Valid values (case-insensitive):

- `Http`, `HTTP`, `http`
- `Stdio`, `STDIO`, `stdio`

## VS Code Integration

For VS Code with GitHub Copilot Agent Mode, configure STDIO transport in your MCP settings:

**settings.json:**

```json
{
  "mcp.servers": {
    "logger-usage": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "path/to/src/LoggerUsage.Mcp",
        "--",
        "--Transport:Mode=Stdio"
      ]
    }
  }
}
```

## Troubleshooting

### HTTP Transport Issues

**Problem**: "Failed to bind to address"

```text
Solution: Port already in use. Configure different port:
dotnet run --project src/LoggerUsage.Mcp --urls "http://localhost:5001"
```

**Problem**: "Connection refused" from client

```text
Solution: Ensure server is running and firewall allows connection:
- Check server is running: curl http://localhost:5000
- Verify correct endpoint URL in client
- Check firewall settings
```

### STDIO Transport Issues

**Problem**: Server starts but no response from client

```text
Solution: Verify STDIO mode is actually active:
- Check configuration: --Transport:Mode=Stdio
- Review startup logs for "Transport mode configured: Stdio"
- Ensure client is reading/writing stdin/stdout correctly
```

**Problem**: "Stream was not readable" error

```text
Solution: Ensure stdin/stdout streams are properly redirected:
- Set RedirectStandardInput = true
- Set RedirectStandardOutput = true
- Set UseShellExecute = false
```

## Performance Considerations

- **HTTP Transport**: ~5-10ms overhead per request (network + HTTP protocol)
- **STDIO Transport**: ~1-2ms overhead per request (process streams only)
- **Recommendation**: Use STDIO for local development, HTTP for production/remote

## Architecture Details

The server uses different hosting models for each transport:

```csharp
// HTTP: WebApplication with ASP.NET Core middleware
WebApplicationBuilder → WebApplication → MapMcp() → HTTP endpoints

// STDIO: HostApplication with stream transport
HostApplicationBuilder → IHost → WithStdioServerTransport() → stdin/stdout
```

This architecture allows both transports to coexist in a single application binary while maintaining clean separation of concerns.

## Examples

### Example 1: Development with STDIO (VS Code)

```bash
# Development environment defaults to STDIO
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/LoggerUsage.Mcp
```

### Example 2: Production HTTP Server

```bash
# Production environment defaults to HTTP
dotnet publish src/LoggerUsage.Mcp -c Release
dotnet src/LoggerUsage.Mcp/bin/Release/net10.0/LoggerUsage.Mcp.dll
```

### Example 3: Override in Tests

```bash
# Force HTTP mode even in Development environment
dotnet run --project src/LoggerUsage.Mcp --environment Development -- --Transport:Mode=Http
```

### Example 4: Docker Container (HTTP)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY . .
RUN dotnet publish src/LoggerUsage.Mcp -c Release -o out
ENV Transport__Mode=Http
ENTRYPOINT ["dotnet", "out/LoggerUsage.Mcp.dll"]
```

## FAQ

**Q: Can I run both transports simultaneously?**
A: No, the application runs in one mode at a time. Choose the appropriate transport based on your client's needs.

**Q: Which transport should I use for GitHub Copilot in VS Code?**
A: Use STDIO transport. VS Code GitHub Copilot Agent Mode spawns MCP servers as child processes and communicates via stdin/stdout.

**Q: Does changing transport affect functionality?**
A: No, both transports expose identical MCP tool capabilities. Only the communication mechanism differs.

**Q: How do I know which transport is active?**
A: Check the startup logs: "Transport mode configured: Http" or "Transport mode configured: Stdio"

**Q: Can I switch transports without recompiling?**
A: Yes! Use configuration files, environment variables, or command-line arguments to switch at runtime.
