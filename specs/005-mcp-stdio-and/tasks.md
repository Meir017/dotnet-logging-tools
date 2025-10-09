# Tasks: MCP STDIO and HTTP Transport Support

**Input**: Design documents from `/specs/005-mcp-stdio-and/`
**Prerequisites**: plan.md (✓), research.md (✓), data-model.md (✓), contracts/ (✓), quickstart.md (✓)

## Execution Summary

This feature adds STDIO transport support to the LoggerUsage.Mcp server alongside the existing HTTP transport. Users can select the transport via `--Transport:Mode=Stdio` command-line argument. The implementation follows TDD principles with tests written first to verify configuration binding and transport selection.

**Tech Stack**: C# / .NET 10, ModelContextProtocol.Server, Microsoft.Extensions.Configuration.CommandLine
**Project Type**: Single project modification (src/LoggerUsage.Mcp)
**Files Modified**: 5 files (~200 lines of code)

---

## Phase 3.1: Setup & Models

### T001 [P] Create TransportMode enum in src/LoggerUsage.Mcp/TransportOptions.cs

**Description**: Create the `TransportMode` enum with `Http` and `Stdio` values.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\src\LoggerUsage.Mcp\TransportOptions.cs`

**Implementation**:
```csharp
namespace LoggerUsage.Mcp;

/// <summary>
/// Defines the available MCP server transport mechanisms.
/// </summary>
public enum TransportMode
{
    /// <summary>
    /// HTTP transport (default, backward compatible).
    /// </summary>
    Http = 0,

    /// <summary>
    /// Standard Input/Output transport.
    /// </summary>
    Stdio = 1
}
```

**Acceptance Criteria**:
- Enum has two values: Http (0) and Stdio (1)
- XML documentation on enum and values
- Http is explicitly set to 0 (default value)

**Dependencies**: None - can run in parallel

---

### T002 [P] Create TransportOptions configuration class in src/LoggerUsage.Mcp/TransportOptions.cs

**Description**: Create the `TransportOptions` class for strongly-typed configuration binding.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\src\LoggerUsage.Mcp\TransportOptions.cs` (same file as T001)

**Implementation**:
```csharp
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
    /// Gets or sets the transport mode. Defaults to HTTP for backward compatibility.
    /// </summary>
    public TransportMode Mode { get; set; } = TransportMode.Http;
}
```

**Acceptance Criteria**:
- Class has `Mode` property with default value `TransportMode.Http`
- SectionName constant is "Transport"
- XML documentation on class and properties
- Default value ensures backward compatibility

**Dependencies**: T001 (needs TransportMode enum)

---

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation in Phase 3.3**

### T003 [P] Test default HTTP transport in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs

**Description**: Write test to verify server defaults to HTTP transport when no configuration provided.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\test\LoggerUsage.Mcp.Tests\TransportConfigurationTests.cs`

**Test Method**:
```csharp
[Fact]
public async Task ServerStartup_WithNoTransportConfig_DefaultsToHttp()
{
    // Arrange: No transport configuration provided

    // Act: Read configuration
    var transportOptions = new TransportOptions();

    // Assert: Should default to HTTP
    Assert.Equal(TransportMode.Http, transportOptions.Mode);
}
```

**Acceptance Criteria**:
- Test initially FAILS (implementation not done yet)
- Test verifies default Mode is Http
- Test uses xUnit assertions
- Test is independent (no shared state)

**Dependencies**: T001, T002 (needs model classes) - Can run in parallel with other test tasks

---

### T004 [P] Test explicit HTTP transport in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs

**Description**: Write test to verify server uses HTTP when explicitly configured via command-line.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\test\LoggerUsage.Mcp.Tests\TransportConfigurationTests.cs`

**Test Method**:
```csharp
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
```

**Acceptance Criteria**:
- Test initially FAILS (binding not implemented)
- Test uses IConfiguration with in-memory values
- Test verifies HTTP mode binding
- Test is independent

**Dependencies**: T001, T002 - Can run in parallel with other test tasks

---

### T005 [P] Test STDIO transport in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs

**Description**: Write test to verify server uses STDIO when configured via command-line.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\test\LoggerUsage.Mcp.Tests\TransportConfigurationTests.cs`

**Test Method**:
```csharp
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
```

**Acceptance Criteria**:
- Test initially FAILS
- Test verifies STDIO mode binding
- Test uses IConfiguration
- Test is independent

**Dependencies**: T001, T002 - Can run in parallel with other test tasks

---

### T006 [P] Test invalid transport value in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs

**Description**: Write test to verify server handles invalid transport values gracefully.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\test\LoggerUsage.Mcp.Tests\TransportConfigurationTests.cs`

**Test Method**:
```csharp
[Theory]
[InlineData("Invalid")]
[InlineData("WebSocket")]
[InlineData("")]
public void ConfigurationBinding_WithInvalidMode_ThrowsOrDefaultsToHttp(string invalidMode)
{
    // Arrange: Configuration with invalid mode
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Transport:Mode"] = invalidMode
        })
        .Build();

    // Act & Assert: Should either throw or default to Http
    // Exact behavior TBD during implementation
    var transportOptions = configuration
        .GetSection(TransportOptions.SectionName)
        .Get<TransportOptions>();

    // For now, expect default (Http) behavior
    Assert.NotNull(transportOptions);
    Assert.Equal(TransportMode.Http, transportOptions.Mode);
}
```

**Acceptance Criteria**:
- Test initially FAILS
- Test covers multiple invalid values
- Test verifies error handling or default behavior
- Test is independent

**Dependencies**: T001, T002 - Can run in parallel with other test tasks

---

### T007 [P] Test case-insensitive parsing in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs

**Description**: Write test to verify transport mode parsing is case-insensitive.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\test\LoggerUsage.Mcp.Tests\TransportConfigurationTests.cs`

**Test Method**:
```csharp
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
```

**Acceptance Criteria**:
- Test initially FAILS
- Test covers multiple case variations
- Test uses Theory with InlineData
- Test is independent

**Dependencies**: T001, T002 - Can run in parallel with other test tasks

---

### T008 [P] Test command-line priority over appsettings in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs

**Description**: Write test to verify command-line arguments override appsettings.json.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\test\LoggerUsage.Mcp.Tests\TransportConfigurationTests.cs`

**Test Method**:
```csharp
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
```

**Acceptance Criteria**:
- Test initially FAILS
- Test simulates configuration priority
- Test verifies command-line wins
- Test is independent

**Dependencies**: T001, T002 - Can run in parallel with other test tasks

---

## Phase 3.3: Core Implementation (ONLY after tests are failing)

**GATE: Do NOT proceed until T003-T008 are written and failing**

### T009 Add Transport configuration section to src/LoggerUsage.Mcp/appsettings.json

**Description**: Add the Transport configuration section to appsettings.json with default HTTP mode.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\src\LoggerUsage.Mcp\appsettings.json`

**Changes**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Transport": {
    "Mode": "Http"
  },
  "AllowedHosts": "*"
}
```

**Acceptance Criteria**:
- Transport section added
- Mode defaults to "Http"
- JSON is valid
- File follows existing formatting conventions

**Dependencies**: T002 (needs TransportOptions constant)

---

### T010 Add STDIO default to src/LoggerUsage.Mcp/appsettings.Development.json

**Description**: Add optional STDIO default for Development environment.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\src\LoggerUsage.Mcp\appsettings.Development.json`

**Changes**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Transport": {
    "Mode": "Stdio"
  }
}
```

**Acceptance Criteria**:
- Transport section added
- Mode set to "Stdio" for development
- JSON is valid
- Optional configuration for dev convenience

**Dependencies**: T002

---

### T011 Implement conditional transport registration in src/LoggerUsage.Mcp/Program.cs

**Description**: Modify Program.cs to read TransportOptions and conditionally register HTTP or STDIO transport.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\src\LoggerUsage.Mcp\Program.cs`

**Implementation Strategy**:
1. Read Transport:Mode from configuration
2. Bind to TransportOptions
3. Log selected transport mode
4. Conditionally call WithHttpTransport() or WithStdioTransport()
5. Validate transport mode, fail fast on invalid values

**Acceptance Criteria**:
- Configuration binding works
- Logs show selected transport at startup
- HTTP transport used when Mode=Http
- STDIO transport used when Mode=Stdio
- Invalid modes logged and server exits with code 1
- All tests in T003-T008 now PASS

**Dependencies**: T003-T008 (tests must exist and fail), T009-T010 (configuration files)

**Expected Code**:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Bind transport configuration
var transportOptions = builder.Configuration
    .GetSection(TransportOptions.SectionName)
    .Get<TransportOptions>() ?? new TransportOptions();

// Log selected transport
builder.Services.AddLogging();
var logger = LoggerFactory.Create(b => b.AddConsole())
    .CreateLogger<Program>();
logger.LogInformation("Starting MCP server with {Transport} transport",
    transportOptions.Mode);

// Conditional transport registration
var mcpBuilder = builder.Services.AddMcpServer();

if (transportOptions.Mode == TransportMode.Http)
{
    mcpBuilder.WithHttpTransport();
}
else if (transportOptions.Mode == TransportMode.Stdio)
{
    mcpBuilder.WithStdioTransport();
}
else
{
    logger.LogError("Invalid transport mode {Mode}", transportOptions.Mode);
    return 1;
}

mcpBuilder.WithTools<LoggerUsageExtractorTool>();

// Rest of existing code...
```

---

## Phase 3.4: Integration & Validation

### T012 Run all tests and verify they pass

**Description**: Execute `dotnet test` to verify all transport configuration tests pass.

**Command**:
```bash
dotnet test test/LoggerUsage.Mcp.Tests/LoggerUsage.Mcp.Tests.csproj --filter "FullyQualifiedName~TransportConfigurationTests"
```

**Acceptance Criteria**:
- All tests in TransportConfigurationTests pass
- No test failures or warnings
- Tests run in <5 seconds

**Dependencies**: T011 (implementation must be complete)

---

### T013 Manual validation: Test Scenario 1 (Default HTTP)

**Description**: Manually verify Scenario 1 from quickstart.md - default HTTP transport.

**Steps**:
1. `dotnet run --project src/LoggerUsage.Mcp`
2. Verify log shows "Starting MCP server with Http transport"
3. Verify server listens on port 5000

**Acceptance Criteria**:
- Server starts without errors
- Log shows HTTP transport
- Server responds to HTTP requests

**Dependencies**: T011

---

### T014 Manual validation: Test Scenario 2 (Explicit STDIO)

**Description**: Manually verify Scenario 2 from quickstart.md - STDIO transport.

**Steps**:
1. `dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Stdio`
2. Verify log shows "Starting MCP server with Stdio transport"
3. Server waits for stdin input

**Acceptance Criteria**:
- Server starts without errors
- Log shows STDIO transport
- Server reads from stdin

**Dependencies**: T011

---

### T015 Manual validation: Test Scenario 3 (Invalid transport)

**Description**: Manually verify Scenario 3 from quickstart.md - invalid transport error.

**Steps**:
1. `dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Invalid`
2. Observe error message
3. Verify exit code is non-zero

**Acceptance Criteria**:
- Server logs error about invalid transport
- Error message lists valid values
- Server exits with code 1

**Dependencies**: T011

---

## Phase 3.5: Polish & Documentation

### T016 [P] Update README.md with transport mode examples

**Description**: Add command-line usage examples for transport modes to README.md.

**File**: `d:\Repos\Meir017\dotnet-logging-usage\README.md`

**Content to Add**:
```markdown
### MCP Server Transport Modes

The LoggerUsage.Mcp server supports two transport mechanisms:

**HTTP Transport (Default)**:
```bash
dotnet run --project src/LoggerUsage.Mcp
# or explicitly
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Http
```

**STDIO Transport** (for CLI tools, VS Code extensions):
```bash
dotnet run --project src/LoggerUsage.Mcp --Transport:Mode=Stdio
```

See [MCP Documentation](specs/005-mcp-stdio-and/) for detailed configuration options.
```

**Acceptance Criteria**:
- README updated with transport examples
- Links to feature documentation
- Examples are correct and tested
- Formatting matches existing README style

**Dependencies**: T011 (implementation complete) - Can run in parallel with T017

---

### T017 [P] Add XML documentation comments to public APIs

**Description**: Ensure all public types (TransportMode, TransportOptions) have complete XML documentation.

**Files**: `d:\Repos\Meir017\dotnet-logging-usage\src\LoggerUsage.Mcp\TransportOptions.cs`

**Acceptance Criteria**:
- All public types have `<summary>` tags
- All public properties have `<summary>` tags
- Enum values have `<summary>` tags
- Documentation is clear and accurate

**Dependencies**: T001, T002 - Can run in parallel with T016

---

## Dependencies Graph

```
Setup Phase:
  T001 (TransportMode enum) [P]
  └─> T002 (TransportOptions class)

Test Phase (All [P] after T002):
  T002 ──┬─> T003 (Test default HTTP) [P]
         ├─> T004 (Test explicit HTTP) [P]
         ├─> T005 (Test STDIO) [P]
         ├─> T006 (Test invalid) [P]
         ├─> T007 (Test case-insensitive) [P]
         └─> T008 (Test CLI priority) [P]

Implementation Phase:
  T002 ──┬─> T009 (appsettings.json)
         └─> T010 (appsettings.Development.json)

  T003-T010 ──> T011 (Program.cs implementation)

Validation Phase:
  T011 ──┬─> T012 (Run tests)
         ├─> T013 (Manual: HTTP)
         ├─> T014 (Manual: STDIO)
         └─> T015 (Manual: Invalid)

Polish Phase (All [P]):
  T011 ──┬─> T016 (README) [P]
         └─> T017 (XML docs) [P]
```

---

## Parallel Execution Examples

**Setup Phase** (T001 can run alone, T002 depends on T001):
```bash
# T001 creates the enum
Task: "Create TransportMode enum in src/LoggerUsage.Mcp/TransportOptions.cs"

# Then T002 adds the class to the same file
Task: "Create TransportOptions class in src/LoggerUsage.Mcp/TransportOptions.cs"
```

**Test Phase** (All tests can run in parallel after T002):
```bash
Task: "Test default HTTP transport in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs"
Task: "Test explicit HTTP transport in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs"
Task: "Test STDIO transport in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs"
Task: "Test invalid transport in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs"
Task: "Test case-insensitive parsing in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs"
Task: "Test command-line priority in test/LoggerUsage.Mcp.Tests/TransportConfigurationTests.cs"
```

**Polish Phase** (README and XML docs can run in parallel):
```bash
Task: "Update README.md with transport mode examples"
Task: "Add XML documentation comments to public APIs"
```

---

## Task Summary

**Total Tasks**: 17
**Parallel Tasks**: 9 (T001, T003-T008, T016-T017)
**Sequential Tasks**: 8 (T002, T009-T015)
**Estimated Time**: 3-4 hours for experienced developer

**Task Breakdown**:
- Setup & Models: 2 tasks (T001-T002)
- Tests (TDD): 6 tasks (T003-T008) - All [P]
- Implementation: 3 tasks (T009-T011)
- Validation: 4 tasks (T012-T015)
- Polish: 2 tasks (T016-T017) - All [P]

---

## Validation Checklist

- [x] All configuration contract scenarios have tests (T003-T008)
- [x] All entities from data-model.md have model tasks (T001-T002)
- [x] All tests come before implementation (T003-T008 before T011)
- [x] Parallel tasks are truly independent (different files or different test methods)
- [x] Each task specifies exact file path
- [x] No task modifies same file as another [P] task (except test methods in same class)
- [x] Manual validation tasks cover all quickstart.md scenarios (T013-T015)
- [x] Documentation tasks included (T016-T017)

---

*Tasks generated successfully. Ready for execution following TDD principles.*
