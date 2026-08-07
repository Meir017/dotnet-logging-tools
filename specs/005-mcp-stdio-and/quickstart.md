# Quickstart Guide: MCP STDIO and HTTP Transport Support

**Feature**: 005-mcp-stdio-and
**Target Audience**: Developers and testers
**Time Required**: 5 minutes

## Prerequisites

- .NET 10 SDK installed
- LoggerUsage repository cloned
- Basic familiarity with command-line tools

---

## Quick Start

### 1. Build the Project

```bash
cd d:\Repos\Meir017\dotnet-logging-usage
dotnet build
```

### 2. Run with HTTP Transport (Default)

```bash
dotnet run --project src/LoggerUsage.Mcp
```

**Expected Output**:
```
info: LoggerUsage.Mcp.Program[0]
      Starting MCP server with Http transport
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### 3. Run with STDIO Transport

```bash
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Stdio
```

**Expected Output**:
```
info: LoggerUsage.Mcp.Program[0]
      Starting MCP server with Stdio transport
```

The server now waits for MCP protocol messages on stdin.

---

## Testing Scenarios

### Scenario 1: Default HTTP Transport

**Goal**: Verify backward compatibility

**Steps**:
1. Start server without transport argument:
   ```bash
   dotnet run --project src/LoggerUsage.Mcp
   ```
2. Verify log shows "Http transport"
3. Verify server listens on port 5000
4. Send HTTP POST request (or use MCP client)

**Success Criteria**:
- ✅ Server starts without errors
- ✅ Log shows HTTP transport selected
- ✅ Server responds to HTTP requests

---

### Scenario 2: Explicit STDIO Transport

**Goal**: Verify STDIO mode activates correctly

**Steps**:
1. Start server with STDIO argument:
   ```bash
   dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Stdio
   ```
2. Verify log shows "Stdio transport"
3. Pipe MCP protocol message to stdin (or use MCP STDIO client)

**Success Criteria**:
- ✅ Server starts without errors
- ✅ Log shows STDIO transport selected
- ✅ Server reads from stdin and writes to stdout

---

### Scenario 3: Invalid Transport Value

**Goal**: Verify error handling

**Steps**:
1. Start server with invalid transport:
   ```bash
   dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Invalid
   ```
2. Observe error message
3. Verify exit code is non-zero

**Success Criteria**:
- ✅ Server logs: "Invalid transport mode 'Invalid'. Valid values: Http, Stdio"
- ✅ Server exits with code 1
- ✅ Error message is clear and actionable

---

### Scenario 4: Configuration File Override

**Goal**: Verify command-line overrides appsettings.json

**Setup**:
1. Edit `src/LoggerUsage.Mcp/appsettings.json`:
   ```json
   {
     "Transport": {
       "Mode": "Http"
     }
   }
   ```

**Steps**:
1. Start server with command-line override:
   ```bash
   dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Stdio
   ```
2. Verify STDIO transport is used (not HTTP from appsettings.json)

**Success Criteria**:
- ✅ Command-line argument wins
- ✅ Log shows "Stdio transport"
- ✅ Server uses STDIO, not HTTP

---

### Scenario 5: Environment-Specific Configuration

**Goal**: Verify Development environment can default to STDIO

**Setup**:
1. Edit `src/LoggerUsage.Mcp/appsettings.Development.json`:
   ```json
   {
     "Transport": {
       "Mode": "Stdio"
     }
   }
   ```

**Steps**:
1. Set environment to Development:
   ```bash
   # Windows PowerShell
   $env:ASPNETCORE_ENVIRONMENT = "Development"

   # Linux/macOS
   export ASPNETCORE_ENVIRONMENT=Development
   ```
2. Start server without command-line argument:
   ```bash
   dotnet run --project src/LoggerUsage.Mcp
   ```
3. Verify STDIO transport is used

**Success Criteria**:
- ✅ Development environment configuration is loaded
- ✅ Log shows "Stdio transport"
- ✅ No command-line argument needed

---

## Integration with Existing Tools

### VS Code Extension Integration

The VS Code extension (LoggerUsage.VSCode.Bridge) should use STDIO transport:

```bash
# VS Code will invoke the server like this:
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Stdio
```

### CLI Tool Integration

CLI tools can use HTTP transport for networked scenarios:

```bash
# Start HTTP server
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Http

# In another terminal, use HTTP client
curl -X POST http://localhost:5000/mcp -d '{"method":"analyze","params":{"csproj":"path/to/file.csproj"}}'
```

---

## Troubleshooting

### Problem: Server starts with HTTP when I wanted STDIO

**Solution**: Check configuration priority
1. Verify command-line argument is correct: `--Transport:Mode=Stdio`
2. Check for typos (case-insensitive, but must be valid enum value)
3. Review logs to see which configuration source was used

### Problem: "Invalid transport mode" error

**Solution**: Use valid transport value
- Valid values: `Http`, `Stdio` (case-insensitive)
- Check spelling
- Remove any extra spaces or quotes

### Problem: Server logs go to stdout and interfere with MCP protocol

**Solution**: This is expected in HTTP mode only
- In STDIO mode, logs go to stderr, not stdout
- MCP protocol messages on stdout are not mixed with logs
- Use logging configuration to reduce verbosity if needed

---

## Next Steps

After validating the quickstart scenarios:

1. **Integration Testing**: Run `dotnet test` to execute transport configuration tests
2. **MCP Client Testing**: Use an actual MCP client to send protocol messages
3. **Documentation**: Review README for command-line examples
4. **Deployment**: Update deployment scripts to specify transport mode

---

## Reference

- **Configuration Contract**: See `contracts/configuration-contract.md`
- **Data Model**: See `data-model.md`
- **Implementation Plan**: See `plan.md`

---

*Quickstart guide complete. Ready for implementation.*
