# Data Model: MCP STDIO and HTTP Transport Support

**Feature**: 005-mcp-stdio-and
**Date**: 2025-10-09
**Status**: Complete

## Domain Entities

### TransportMode (Enum)

**Purpose**: Represents the available MCP server transport mechanisms

**Values**:
- `Http` (0) - HTTP transport (default, backward compatible)
- `Stdio` (1) - Standard Input/Output transport

**Validation Rules**:
- Must be a valid enum value
- Case-insensitive parsing from configuration strings
- Invalid values result in startup failure with clear error message

**State Transitions**: None - immutable after startup

**Usage Context**:
- Set during application startup from configuration
- Used to conditionally register transport services
- Logged during server initialization

---

### TransportOptions (Configuration Model)

**Purpose**: Strongly-typed configuration binding for transport settings

**Properties**:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Mode` | `TransportMode` | No | `TransportMode.Http` | The transport mechanism to use |

**Configuration Section**: `"Transport"`

**Configuration Sources** (priority order):
1. Command-line arguments: `--Transport:Mode=Stdio`
2. Environment variables: `TRANSPORT__MODE=Stdio`
3. `appsettings.{Environment}.json`
4. `appsettings.json`
5. Default value in code

**Validation Rules**:
- Mode must be a valid TransportMode enum value
- Invalid string values result in parsing exception at startup
- Null/missing configuration falls back to default

**Example Configurations**:

```json
// appsettings.json
{
  "Transport": {
    "Mode": "Http"
  }
}
```

```json
// appsettings.Development.json (optional override)
{
  "Transport": {
    "Mode": "Stdio"
  }
}
```

```bash
# Command-line (highest priority)
dotnet run --Transport:Mode=Stdio
```

---

## Relationships

```
TransportOptions
├── Mode: TransportMode enum
│
WebApplicationBuilder
├── Configuration (IConfiguration)
│   └── GetSection("Transport") → TransportOptions
│
McpServerBuilder
├── Conditional registration based on TransportOptions.Mode
│   ├── WithHttpTransport() when Mode == Http
│   └── WithStdioTransport() when Mode == Stdio
```

---

## State Management

### Configuration Binding Flow

1. **Startup Phase**: `WebApplication.CreateBuilder(args)` loads configuration
2. **Binding Phase**: Configuration system parses `Transport:Mode` from sources
3. **Validation Phase**: Enum parsing validates string value
4. **Registration Phase**: Conditional transport registration
5. **Runtime Phase**: Selected transport handles all MCP protocol communication

### Error States

| Error Condition | Behavior | Error Message |
|-----------------|----------|---------------|
| Invalid transport string | Startup failure | `"Invalid transport mode '{value}'. Valid values: Http, Stdio"` |
| Missing configuration | Uses default | Logs: `"Transport mode not configured, defaulting to Http"` |
| Multiple transports configured | Not possible | N/A - configuration model prevents this |

---

## Extension Points

### Future Transport Types

The `TransportMode` enum can be extended to support additional transports:

```csharp
public enum TransportMode
{
    Http = 0,
    Stdio = 1,
    WebSocket = 2,  // Future: WebSocket transport
    Grpc = 3        // Future: gRPC transport
}
```

**Design Considerations**:
- Adding enum values is non-breaking (default remains `Http`)
- Transport registration logic must be updated in `Program.cs`
- Tests must be added for each new transport

### Custom Transport Configuration

Future enhancements could add transport-specific options:

```csharp
public class TransportOptions
{
    public TransportMode Mode { get; set; } = TransportMode.Http;

    // Future: Transport-specific settings
    public HttpTransportOptions? Http { get; set; }
    public StdioTransportOptions? Stdio { get; set; }
}

public class HttpTransportOptions
{
    public int Port { get; set; } = 5000;
    public string Host { get; set; } = "localhost";
}

public class StdioTransportOptions
{
    public bool BufferedOutput { get; set; } = false;
}
```

---

## Serialization

The `TransportMode` enum and `TransportOptions` class do not require custom serialization for this feature. They are used for configuration binding only and not exposed in MCP protocol responses.

**Configuration Format**: JSON (appsettings.json)
**Command-Line Format**: Colon-delimited key-value pairs
**Environment Variable Format**: Double-underscore separated

---

## Validation Summary

| Validation Rule | Enforcement Point | Severity |
|-----------------|-------------------|----------|
| Mode must be valid enum | Startup (config binding) | Fatal - exit |
| Mode defaults to Http if missing | Startup (default value) | Info - log |
| Command-line overrides appsettings | Configuration load | Info - log |
| Only one transport active | Runtime (MCP library) | N/A - enforced by design |

---

*Data model complete. Ready for contract generation (Phase 1 continued).*
