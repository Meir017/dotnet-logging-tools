# Research: MCP Progress Tracking Support

**Feature**: 005-mcp-progress-tracking
**Date**: 2025-10-09
**Status**: Complete

## Research Overview

This document captures technical research for implementing MCP progress tracking in the LoggerUsage.Mcp server. The feature leverages existing infrastructure (`IProgress<ProgressReport>` in LoggerUsageExtractor) and the MCP C# SDK's progress notification capabilities.

## Key Research Areas

### 1. MCP C# SDK Progress API

**Decision**: Use `IMcpServer.SendNotificationAsync` with `"notifications/progress"` method

**Rationale**:
- Official MCP specification support for progress tracking
- C# SDK provides `SendNotificationAsync` extension method on `IMcpEndpoint`/`IMcpServer`
- `ProgressNotificationParams` and `ProgressNotificationValue` types already defined in SDK
- Server-side implementation pattern documented in official docs

**Alternatives Considered**:
1. ❌ Custom notification system → Would not be MCP-compliant, breaks client compatibility
2. ❌ Polling-based progress → Inefficient, not real-time, not MCP standard
3. ✅ MCP SDK progress notifications → Standard, efficient, documented

**References**:
- [MCP Progress Documentation](https://github.com/modelcontextprotocol/csharp-sdk/blob/f28639119b3596b0357ea4b979d3289cad54054a/docs/concepts/progress/progress.md)
- [IMcpServer API](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.IMcpServer.html)
- [SendNotificationAsync Extension](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.McpEndpointExtensions.html)

### 2. Progress Token Handling

**Decision**: Accept `ProgressToken?` as optional parameter in tool method signature

**Rationale**:
- MCP pattern: clients provide token in request, server echoes it in notifications
- Optional parameter maintains backward compatibility
- Null check before sending notifications = opt-in behavior
- Simple API surface, no wrapper classes needed

**Alternatives Considered**:
1. ❌ Separate parameter object → Over-engineering for single optional parameter
2. ❌ Required parameter → Breaks existing clients, violates backward compatibility
3. ✅ Optional nullable parameter → Standard C# pattern, backward compatible

**Implementation Pattern**:
```csharp
public async Task<LoggerUsageExtractionResult> AnalyzeLoggerUsagesInCsproj(
    string fullPathToCsproj,
    ProgressToken? progressToken = null) // Optional
{
    if (progressToken != null)
    {
        // Create progress adapter and wire to LoggerUsageExtractor
    }
    // ... existing logic
}
```

### 3. Progress Adapter Design

**Decision**: Create `McpProgressAdapter` class implementing `IProgress<ProgressReport>`

**Rationale**:
- Separation of concerns: adapter encapsulates MCP protocol details
- Testable: can unit test adapter in isolation
- Reusable: can be used by other MCP tools if added in future
- Clean bridge between domain model (`ProgressReport`) and protocol (`ProgressNotificationParams`)

**Alternatives Considered**:
1. ❌ Inline logic in tool method → Clutters tool code, harder to test
2. ❌ Modify ProgressReport model → Violates SRP, couples domain to MCP protocol
3. ✅ Dedicated adapter class → Clean separation, testable, reusable

**Class Structure**:
```csharp
internal class McpProgressAdapter : IProgress<ProgressReport>
{
    private readonly IMcpServer _mcpServer;
    private readonly ProgressToken _progressToken;
    private readonly ILogger<McpProgressAdapter> _logger;

    public McpProgressAdapter(
        IMcpServer mcpServer,
        ProgressToken progressToken,
        ILogger<McpProgressAdapter> logger)
    {
        _mcpServer = mcpServer;
        _progressToken = progressToken;
        _logger = logger;
    }

    public void Report(ProgressReport value)
    {
        try
        {
            // Map ProgressReport → ProgressNotificationParams
            // Call SendNotificationAsync
        }
        catch (Exception ex)
        {
            // Log but don't throw - progress is best-effort
            _logger.LogWarning(ex, "Failed to send progress notification");
        }
    }
}
```

### 4. Error Handling Strategy

**Decision**: Catch exceptions in progress reporting, log warnings, continue analysis

**Rationale**:
- Progress is non-critical: analysis result is primary concern
- Client disconnection shouldn't fail analysis
- Network issues, protocol errors shouldn't block work
- Follows Constitution Principle 4 (Graceful Degradation)

**Alternatives Considered**:
1. ❌ Propagate exceptions → Would fail analysis on progress errors, poor UX
2. ❌ Silent failures → No diagnostics, hard to debug
3. ✅ Catch + log + continue → Best effort, observable failures, reliable analysis

**Implementation Pattern**:
```csharp
public void Report(ProgressReport value)
{
    try
    {
        await _mcpServer.SendNotificationAsync(
            "notifications/progress",
            new ProgressNotificationParams { /* ... */ },
            cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, 
            "Failed to send progress notification for progress {Current}/{Total}", 
            value.CurrentStep, 
            value.TotalSteps);
        // Don't rethrow - continue analysis
    }
}
```

### 5. Progress Granularity

**Decision**: Report progress per-file (use existing `ProgressReport` granularity)

**Rationale**:
- `LoggerUsageExtractor` already reports per-file progress
- File count typically 10-500, not thousands (reasonable notification volume)
- Provides meaningful feedback without over-reporting
- No additional aggregation logic needed

**Alternatives Considered**:
1. ❌ Per-line progress → Too granular, notification spam
2. ❌ Batched updates (e.g., every 10 files) → More complex, delayed feedback
3. ✅ Per-file progress → Matches existing model, reasonable granularity

**Data Flow**:
```
LoggerUsageExtractor (per file)
    ↓ IProgress<ProgressReport>
McpProgressAdapter
    ↓ Map to ProgressNotificationParams
IMcpServer.SendNotificationAsync
    ↓ MCP protocol
Client receives notification
```

### 6. Testing Strategy

**Decision**: Integration tests in LoggerUsage.Mcp.Tests, benchmark test for performance

**Rationale**:
- Integration test: Verify actual MCP notifications sent (use mock server or capture)
- Cover: with token, without token, error scenarios
- Benchmark: Verify <5% overhead requirement
- Follows Constitution Principle 2 (Test-First Development)

**Test Cases**:
1. ✅ Tool with progress token → notifications sent
2. ✅ Tool without progress token → no notifications
3. ✅ Single file analysis → one notification
4. ✅ Multiple files analysis → multiple notifications with correct counts
5. ✅ Progress notification failure → analysis completes successfully
6. ✅ Benchmark: with vs without progress tracking (overhead <5%)

**Alternatives Considered**:
1. ❌ Unit tests only → Wouldn't verify MCP protocol integration
2. ❌ Manual testing only → Not repeatable, no regression protection
3. ✅ Integration + benchmark tests → Comprehensive, automated, performance-aware

### 7. Dependency Injection Pattern

**Decision**: Inject `IMcpServer` into tool constructor, create adapter in method

**Rationale**:
- `IMcpServer` is registered by MCP SDK in DI container
- Tool already uses DI (has constructor with logger, extractor, etc.)
- Adapter is request-scoped (tied to specific progress token)
- No new DI registrations needed

**Alternatives Considered**:
1. ❌ Register adapter as service → Wrong lifecycle, token is per-request
2. ❌ Service locator pattern → Anti-pattern, hides dependencies
3. ✅ Inject IMcpServer, instantiate adapter → Correct lifecycle, clear dependencies

**Updated Tool Constructor**:
```csharp
public class LoggerUsageExtractorTool(
    ILogger<LoggerUsageExtractorTool> logger,
    IWorkspaceFactory workspaceFactory,
    LoggerUsageExtractor loggerUsageExtractor,
    ILoggerReportGeneratorFactory loggerReportGeneratorFactory,
    IMcpServer mcpServer) // NEW: Inject MCP server
{
    // ... methods
}
```

## Technical Constraints Summary

| Constraint | Impact | Mitigation |
|------------|--------|-----------|
| MCP SDK API surface | Must use provided types/methods | Follow SDK patterns from documentation |
| Backward compatibility | Can't break existing clients | Optional parameter, feature opt-in |
| Performance overhead | <5% target | Async notifications, minimal mapping logic |
| Error resilience | Progress failures shouldn't block analysis | Try-catch + log in adapter |
| Thread safety | Progress during concurrent analysis | Stateless adapter, SDK handles concurrency |

## Best Practices Applied

1. **MCP Compliance**: Follow official SDK patterns and specification
2. **Separation of Concerns**: Adapter isolates protocol from domain logic
3. **Graceful Degradation**: Progress failures don't impact analysis
4. **Testability**: Design allows unit and integration testing
5. **Performance Awareness**: Measure and verify overhead constraint
6. **Backward Compatibility**: Optional feature preserves existing behavior
7. **Observability**: Structured logging for diagnostics

## Open Questions & Answers

### Q1: Should we support cancellation via progress token?
**A**: No. MCP progress is for reporting only. Cancellation is a separate concern handled by `CancellationToken` parameter (if we add it later).

### Q2: Should we buffer/throttle notifications?
**A**: No. File-level granularity is already reasonable. Throttling adds complexity without clear benefit for typical workloads (10-500 files).

### Q3: Should we report sub-file progress (e.g., percentage through large file)?
**A**: No. `LoggerUsageExtractor` reports at file level. Changing granularity is out of scope and would require changes to core library.

### Q4: Should we send a completion notification?
**A**: Yes, implicitly. When `progress == total`, client knows operation completed. No special "done" message needed per MCP pattern.

## Research Validation Checklist

- [x] All unknowns from Technical Context resolved
- [x] Technology choices justified with rationale
- [x] Alternatives evaluated and documented
- [x] Best practices identified for domain
- [x] Constraints and trade-offs explicit
- [x] Design decisions support testability
- [x] Performance considerations addressed
- [x] Constitutional principles validated

## References

- [MCP Progress Documentation](https://github.com/modelcontextprotocol/csharp-sdk/blob/f28639119b3596b0357ea4b979d3289cad54054a/docs/concepts/progress/progress.md)
- [MCP C# SDK API Reference](https://modelcontextprotocol.github.io/csharp-sdk/api/)
- [LoggerUsageExtractor IProgress Support (PR #141)](https://github.com/Meir017/dotnet-logging-usage/pull/141)
- [Constitution Principle 4: Graceful Degradation](../../.specify/memory/constitution.md)
- [Constitution Principle 2: Test-First Development](../../.specify/memory/constitution.md)
- [Constitution Principle 6: Performance Contracts](../../.specify/memory/constitution.md)
