# Research: MCP STDIO and HTTP Transport Support

**Feature**: 005-mcp-stdio-and
**Date**: 2025-10-09
**Status**: Complete

## Research Questions

### 1. How does ASP.NET Core support command-line argument configuration?

**Decision**: Use `WebApplication.CreateBuilder(args)` with command-line configuration provider

**Rationale**:
- ASP.NET Core's `WebApplicationBuilder` automatically includes command-line arguments as a configuration source
- Command-line arguments have the **highest priority** in the default configuration hierarchy
- Supports multiple syntaxes: `--key=value`, `--key value`, `/key value`
- Built-in support via `CommandLineConfigurationProvider`

**Alternatives Considered**:
- Manual argument parsing: Rejected - reinvents the wheel, loses integration with appsettings.json
- Environment variables only: Rejected - less user-friendly for CLI tools

**Implementation Pattern** (from Microsoft docs):
```csharp
var builder = WebApplication.CreateBuilder(args);
// Command-line args automatically loaded
// Access via builder.Configuration["Transport"]
```

### 2. How does ModelContextProtocol.Server support multiple transports?

**Decision**: Use `WithStdioTransport()` and `WithHttpTransport()` extension methods conditionally

**Rationale**:
- The MCP server library provides fluent API for transport configuration
- Transports are mutually exclusive - only one can be active
- Configuration happens during service registration via `AddMcpServer()`

**Alternatives Considered**:
- Supporting both transports simultaneously: Not supported by the library architecture
- Custom transport implementation: Unnecessary - built-in transports are sufficient

**Implementation Pattern**:
```csharp
builder.Services.AddMcpServer()
    .WithStdioTransport()  // OR
    .WithHttpTransport()
    .WithTools<LoggerUsageExtractorTool>();
```

### 3. What is the best way to model the transport configuration?

**Decision**: Create a `TransportOptions` class with enum and bind from configuration

**Rationale**:
- Strongly typed configuration prevents typos and provides IntelliSense
- Enum makes valid values explicit
- Options pattern integrates with ASP.NET Core validation
- Supports binding from multiple sources (command-line, appsettings.json, environment variables)

**Alternatives Considered**:
- String-based configuration: Rejected - error-prone, no compile-time safety
- Boolean flag (IsStdio): Rejected - doesn't scale if more transports added later

**Implementation Pattern**:
```csharp
public enum TransportMode
{
    Http,  // Default
    Stdio
}

public class TransportOptions
{
    public const string SectionName = "Transport";
    public TransportMode Mode { get; set; } = TransportMode.Http;
}

// Usage
builder.Services.Configure<TransportOptions>(builder.Configuration.GetSection(TransportOptions.SectionName));
```

### 4. How should invalid transport arguments be handled?

**Decision**: Validate at startup, fail fast with clear error message

**Rationale**:
- Configuration errors should be caught immediately, not during runtime
- Clear error messages improve developer experience
- Failing fast prevents the server from starting in an invalid state

**Alternatives Considered**:
- Fallback to default: Rejected - silent failures are confusing
- Runtime validation: Rejected - server shouldn't start if misconfigured

**Implementation Pattern**:
```csharp
var transportMode = builder.Configuration.GetValue<string>("Transport:Mode");
if (!Enum.TryParse<TransportMode>(transportMode, ignoreCase: true, out var mode))
{
    throw new InvalidOperationException(
        $"Invalid transport mode '{transportMode}'. Valid values: {string.Join(", ", Enum.GetNames<TransportMode>())}");
}
```

### 5. What is the backward compatibility strategy?

**Decision**: Default to HTTP transport when no argument specified

**Rationale**:
- Maintains existing behavior for users who don't specify `--transport`
- Zero breaking changes
- Explicit opt-in to STDIO transport

**Alternatives Considered**:
- Require explicit transport argument: Rejected - breaks existing deployments
- Auto-detect based on environment: Rejected - too magical, hard to debug

**Implementation Pattern**:
```csharp
public TransportMode Mode { get; set; } = TransportMode.Http;  // Default value
```

### 6. How should the configuration hierarchy work?

**Decision**: Command-line > appsettings.{Environment}.json > appsettings.json

**Rationale**:
- ASP.NET Core default configuration order (command-line has highest priority)
- Allows environment-specific defaults (e.g., STDIO for Development)
- Command-line override enables runtime flexibility

**Alternatives Considered**:
- Command-line only: Rejected - loses environment-specific configuration
- appsettings only: Rejected - can't override at runtime

**Configuration Priority** (highest to lowest):
1. `--Transport:Mode=stdio` (command-line)
2. `TRANSPORT__MODE=stdio` (environment variable)
3. `appsettings.Development.json` (environment-specific file)
4. `appsettings.json` (base file)
5. Default value in code (`TransportMode.Http`)

## Technology Stack Decisions

### Configuration System
- **Technology**: Microsoft.Extensions.Configuration.CommandLine (built-in)
- **Rationale**: No additional dependencies, well-tested, integrated with ASP.NET Core
- **Best Practices**:
  - Use `IConfiguration` for access
  - Use Options pattern for strongly-typed configuration
  - Validate configuration at startup

### Transport Configuration
- **Technology**: ModelContextProtocol.Server fluent API
- **Rationale**: Built-in support, idiomatic C# configuration
- **Best Practices**:
  - Conditional registration based on configuration
  - Single transport per server instance
  - Log selected transport at startup

### Testing Strategy
- **Technology**: xUnit + Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory)
- **Rationale**: Industry standard for ASP.NET Core testing
- **Best Practices**:
  - Test each transport mode in isolation
  - Test configuration binding
  - Test invalid configuration handling
  - Integration tests with actual MCP protocol

## Documentation Requirements

### README Updates
- Add command-line usage examples
- Document both transport modes
- Provide examples for common scenarios (VS Code, CLI tools, web apps)

### appsettings.json Schema
```json
{
  "Transport": {
    "Mode": "Http"  // or "Stdio"
  }
}
```

### Command-Line Examples
```bash
# HTTP transport (default)
dotnet run --project src/LoggerUsage.Mcp

# Explicit HTTP transport
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Http

# STDIO transport
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Stdio
```

## Implementation Checklist

- [x] Research ASP.NET Core command-line configuration
- [x] Research MCP Server transport APIs
- [x] Design TransportOptions model
- [x] Define error handling strategy
- [x] Define backward compatibility approach
- [x] Document configuration hierarchy
- [x] Define testing strategy

## References

- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Command-line Configuration Provider](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0#command-line)
- [Options Pattern in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- ModelContextProtocol.Server NuGet package documentation

---

*All research questions resolved. Ready for Phase 1: Design & Contracts.*
