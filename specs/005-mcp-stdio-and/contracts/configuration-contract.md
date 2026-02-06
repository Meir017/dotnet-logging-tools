# Configuration Contract: Transport Mode

**Contract Type**: Configuration API
**Audience**: Server administrators, deployment engineers, developers

## Overview

This contract defines how users configure the MCP server transport mechanism through command-line arguments, environment variables, and configuration files.

---

## Configuration Schema

### JSON Schema (appsettings.json)

```json
{
  "$schema": "https://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "Transport": {
      "type": "object",
      "properties": {
        "Mode": {
          "type": "string",
          "enum": ["Http", "Stdio"],
          "default": "Http",
          "description": "The MCP server transport mechanism"
        }
      }
    }
  }
}
```

### Configuration File Example

**appsettings.json** (Base configuration):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Transport": {
    "Mode": "Http"
  }
}
```

**appsettings.Development.json** (Development override):
```json
{
  "Transport": {
    "Mode": "Stdio"
  }
}
```

---

## Command-Line Interface

### Syntax

```bash
dotnet run --project src/LoggerUsage.Mcp [options]
```

### Options

| Option | Values | Default | Description |
|--------|--------|---------|-------------|
| `--Transport:Mode` | `Http`, `Stdio` | `Http` | Sets the MCP server transport mode |

### Examples

**Default (HTTP transport)**:
```bash
dotnet run --project src/LoggerUsage.Mcp
```

**Explicit HTTP transport**:
```bash
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Http
```

**STDIO transport**:
```bash
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Stdio
```

**Case-insensitive**:
```bash
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=stdio
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=HTTP
```

---

## Environment Variables

### Variable Name

`TRANSPORT__MODE` (double underscore separates hierarchy levels)

### Examples

**Windows (cmd.exe)**:
```cmd
set TRANSPORT__MODE=Stdio
dotnet run --project src/LoggerUsage.Mcp
```

**Windows (PowerShell)**:
```powershell
$env:TRANSPORT__MODE = "Stdio"
dotnet run --project src/LoggerUsage.Mcp
```

**Linux/macOS**:
```bash
export TRANSPORT__MODE=Stdio
dotnet run --project src/LoggerUsage.Mcp
```

---

## Configuration Priority

Configuration sources are applied in the following order (highest to lowest priority):

1. **Command-line arguments**: `--Transport:Mode=Stdio`
2. **Environment variables**: `TRANSPORT__MODE=Stdio`
3. **Environment-specific JSON**: `appsettings.Development.json`
4. **Base JSON**: `appsettings.json`
5. **Code default**: `TransportMode.Http`

**Example**: If `appsettings.json` sets `Http` but command-line provides `--Transport:Mode=Stdio`, the server uses `Stdio`.

---

## Validation Rules

### Valid Values

- `"Http"` (case-insensitive) - HTTP/REST transport
- `"Stdio"` (case-insensitive) - Standard input/output transport

### Invalid Values Behavior

When an invalid value is provided:

1. Server logs error message with valid options
2. Server exits with non-zero exit code
3. Error message format:
   ```
   Invalid transport mode '{value}'. Valid values: Http, Stdio
   ```

### Examples of Invalid Values

```bash
# Invalid: typo
dotnet run --Transport:Mode=Stdoi
# Output: Invalid transport mode 'Stdoi'. Valid values: Http, Stdio
# Exit code: 1

# Invalid: unsupported transport
dotnet run --Transport:Mode=WebSocket
# Output: Invalid transport mode 'WebSocket'. Valid values: Http, Stdio
# Exit code: 1
```

---

## Behavioral Contract

### HTTP Transport Behavior

When `Mode = Http`:
- Server listens on configured HTTP endpoint (default: `http://localhost:5000`)
- MCP protocol exposed via HTTP POST requests
- Supports multiple concurrent clients
- Responds with HTTP status codes (200, 400, 500)

### STDIO Transport Behavior

When `Mode = Stdio`:
- Server reads MCP protocol messages from `stdin`
- Server writes responses to `stdout`
- Server writes logs to `stderr` (not mixed with protocol)
- Single-client mode (one-to-one communication)
- Process-based lifecycle (terminates when stdin closes)

---

## Logging Contract

The server logs the selected transport mode during startup:

```
info: LoggerUsage.Mcp.Program[0]
      Starting MCP server with Stdio transport
```

Or:

```
info: LoggerUsage.Mcp.Program[0]
      Starting MCP server with Http transport
```

---

## Backward Compatibility

- **Default behavior preserved**: Servers without explicit configuration use HTTP transport
- **No breaking changes**: Existing deployments continue to work
- **Additive only**: New `--Transport:Mode` argument is optional

---

## Testing Contract

### Test Scenarios

1. **Default HTTP**: Server starts without arguments → uses HTTP
2. **Explicit HTTP**: `--Transport:Mode=Http` → uses HTTP
3. **STDIO mode**: `--Transport:Mode=Stdio` → uses STDIO
4. **Case insensitivity**: `--Transport:Mode=stdio` → uses STDIO
5. **Invalid value**: `--Transport:Mode=Invalid` → error + exit
6. **Command-line override**: appsettings.json=Http + CLI=Stdio → uses STDIO
7. **Environment variable**: `TRANSPORT__MODE=Stdio` → uses STDIO

---

## Error Codes

| Exit Code | Condition | Message Pattern |
|-----------|-----------|-----------------|
| 0 | Success | Normal operation |
| 1 | Invalid transport mode | `Invalid transport mode '{value}'. Valid values: ...` |
| 1 | Configuration binding error | `Failed to bind configuration: {details}` |

---

*Configuration contract complete.*
