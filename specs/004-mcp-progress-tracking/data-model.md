# Data Model: MCP Progress Tracking Support

**Feature**: 005-mcp-progress-tracking
**Date**: 2025-10-09
**Status**: Complete

## Overview

This document defines the data entities and their relationships for MCP progress tracking support. Since this feature bridges existing models (`ProgressReport`) to MCP protocol types (`ProgressNotificationParams`), most entities already exist. The primary addition is the `McpProgressAdapter` class that performs the mapping.

## Entities

### 1. ProgressReport (Existing)

**Source**: `LoggerUsage.Models.ProgressReport`
**Status**: Existing - No Changes
**Purpose**: Domain model representing progress state during analysis

**Structure**:
```csharp
public class ProgressReport
{
    public required int CurrentStep { get; init; }
    public required int TotalSteps { get; init; }
    public string? Message { get; init; }
}
```

**Fields**:
- `CurrentStep` (int, required): Current progress value (e.g., files analyzed so far)
- `TotalSteps` (int, required): Total progress value (e.g., total files to analyze)
- `Message` (string?, optional): Optional human-readable message describing current activity

**Relationships**:
- Reported by `LoggerUsageExtractor` via `IProgress<ProgressReport>`
- Consumed by `McpProgressAdapter` to create MCP notifications

**Validation Rules**:
- `CurrentStep` >= 0
- `TotalSteps` > 0
- `CurrentStep` <= `TotalSteps`

**State Transitions**:
1. Initial: `CurrentStep = 0, TotalSteps = N`
2. Progress: `CurrentStep = 1..N-1, TotalSteps = N`
3. Complete: `CurrentStep = N, TotalSteps = N`

### 2. ProgressToken (MCP SDK Type)

**Source**: `ModelContextProtocol.Protocol.ProgressToken`
**Status**: Existing - MCP SDK
**Purpose**: MCP protocol type identifying a progress tracking session

**Structure**: Opaque type (string or number, MCP specification allows both)

**Usage**:
- Provided by client in request parameters
- Echoed by server in progress notifications
- Ties notifications to specific requests

**Validation Rules**:
- Must not be null if progress tracking requested
- Should be unique per request (client responsibility)

### 3. ProgressNotificationParams (MCP SDK Type)

**Source**: `ModelContextProtocol.Protocol.ProgressNotificationParams`
**Status**: Existing - MCP SDK
**Purpose**: MCP protocol message for progress notifications

**Structure**:
```csharp
public class ProgressNotificationParams
{
    public ProgressToken ProgressToken { get; set; }
    public ProgressNotificationValue Progress { get; set; }
}
```

**Fields**:
- `ProgressToken` (ProgressToken, required): Token from client request
- `Progress` (ProgressNotificationValue, required): Actual progress data

**Relationships**:
- Created by `McpProgressAdapter` from `ProgressReport`
- Sent via `IMcpServer.SendNotificationAsync`

### 4. ProgressNotificationValue (MCP SDK Type)

**Source**: `ModelContextProtocol.Protocol.ProgressNotificationValue`
**Status**: Existing - MCP SDK
**Purpose**: MCP protocol type containing progress values

**Structure**:
```csharp
public class ProgressNotificationValue
{
    public int Progress { get; set; }
    public int Total { get; set; }
    public string? Message { get; set; }
}
```

**Fields**:
- `Progress` (int, required): Current progress value
- `Total` (int, required): Total progress value
- `Message` (string?, optional): Optional descriptive message

**Relationships**:
- Nested within `ProgressNotificationParams`
- Mapped from `ProgressReport` fields

### 5. McpProgressAdapter (New)

**Source**: `LoggerUsage.Mcp.McpProgressAdapter` (to be created)
**Status**: New - This Feature
**Purpose**: Adapter that bridges `IProgress<ProgressReport>` to MCP progress notifications

**Structure**:
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
        _mcpServer = mcpServer ?? throw new ArgumentNullException(nameof(mcpServer));
        _progressToken = progressToken ?? throw new ArgumentNullException(nameof(progressToken));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Report(ProgressReport value)
    {
        // Map ProgressReport → ProgressNotificationParams
        // Send via IMcpServer.SendNotificationAsync
        // Catch and log errors (don't throw)
    }
}
```

**Fields**:
- `_mcpServer` (IMcpServer, required): MCP server for sending notifications
- `_progressToken` (ProgressToken, required): Token to include in notifications
- `_logger` (ILogger, required): Logger for error diagnostics

**Relationships**:
- Implements `IProgress<ProgressReport>` (domain interface)
- Uses `IMcpServer` (MCP SDK)
- Depends on `ILogger<T>` (ASP.NET Core logging)

**Behavior**:
- Receives `ProgressReport` via `Report(value)` method
- Maps fields: `value.CurrentStep` → `Progress`, `value.TotalSteps` → `Total`, `value.Message` → `Message`
- Sends notification via `SendNotificationAsync("notifications/progress", params)`
- Catches exceptions and logs warnings (graceful degradation)

**Validation Rules**:
- Constructor parameters must not be null
- `Report` method must not throw exceptions (catch internally)

**Lifecycle**:
- Created per-request when progress token provided
- Lives for duration of analysis operation
- No explicit disposal needed (stateless)

## Entity Relationships

```
┌─────────────────────────┐
│ Client Request          │
│ (with progressToken)    │
└───────────┬─────────────┘
            │
            v
┌─────────────────────────────────────┐
│ LoggerUsageExtractorTool            │
│ - Accept progressToken parameter     │
│ - Create McpProgressAdapter if token │
└───────────┬─────────────────────────┘
            │
            v
┌───────────────────────────────────────┐
│ LoggerUsageExtractor                  │
│ - Reports IProgress<ProgressReport>   │
└───────────┬───────────────────────────┘
            │ IProgress<ProgressReport>
            v
┌───────────────────────────────────────┐
│ McpProgressAdapter                    │
│ - Implements IProgress<ProgressReport>│
│ - Maps to ProgressNotificationParams  │
│ - Sends via IMcpServer                │
└───────────┬───────────────────────────┘
            │ ProgressNotificationParams
            v
┌───────────────────────────────────────┐
│ IMcpServer                            │
│ - SendNotificationAsync               │
│ - Method: "notifications/progress"    │
└───────────┬───────────────────────────┘
            │ MCP Protocol
            v
┌───────────────────────────────────────┐
│ Client                                │
│ - Receives progress notifications     │
│ - Updates UI/status                   │
└───────────────────────────────────────┘
```

## Mapping Logic

### ProgressReport → ProgressNotificationParams

```csharp
// Input: ProgressReport
public record ProgressReport
{
    public required int CurrentStep { get; init; }
    public required int TotalSteps { get; init; }
    public string? Message { get; init; }
}

// Output: ProgressNotificationParams
var notificationParams = new ProgressNotificationParams
{
    ProgressToken = _progressToken,
    Progress = new ProgressNotificationValue
    {
        Progress = value.CurrentStep,    // Direct mapping
        Total = value.TotalSteps,        // Direct mapping
        Message = value.Message          // Pass through (nullable)
    }
};
```

**Mapping Rules**:
1. `ProgressReport.CurrentStep` → `ProgressNotificationValue.Progress` (1:1)
2. `ProgressReport.TotalSteps` → `ProgressNotificationValue.Total` (1:1)
3. `ProgressReport.Message` → `ProgressNotificationValue.Message` (1:1, nullable preserved)
4. `_progressToken` (from constructor) → `ProgressNotificationParams.ProgressToken` (constant per request)

**Validation**:
- No validation needed: `ProgressReport` already validated by `LoggerUsageExtractor`
- MCP SDK types handle their own validation

## State Diagrams

### Progress Tracking State Machine

```
[Client Request] 
    │
    ├─ progressToken == null ──> [No Progress Tracking] ──> [Analysis Completes]
    │                                                           │
    │                                                           v
    └─ progressToken != null ──> [Create Adapter]          [Return Result]
                                      │
                                      v
                                [Analysis Starts]
                                      │
                                      v
                           ┌─────────────────────┐
                           │ For Each File       │
                           │ (reported by        │
                           │  LoggerUsageExtr)   │
                           └──────────┬──────────┘
                                      │
                                      v
                           [Adapter.Report(progress)]
                                      │
                                      ├─ Success ─────> [Send Notification]
                                      │                        │
                                      │                        v
                                      │                 [Client Receives]
                                      │
                                      └─ Exception ───> [Log Warning]
                                                              │
                                                              v
                                                     [Continue Analysis]
                                                              │
                                                              v
                                                     [Next File]
```

### Notification Sequence

```
Time │ Event
═════╪═══════════════════════════════════════════════════════════════
  1  │ Client sends request with progressToken="abc123"
  2  │ Server creates McpProgressAdapter(token="abc123")
  3  │ LoggerUsageExtractor starts, reports Progress(0/100, "Starting")
  4  │ Adapter sends notification {token="abc123", progress=0, total=100}
  5  │ Client updates UI: "Starting... 0%"
  6  │ LoggerUsageExtractor reports Progress(1/100, "Analyzing File1.cs")
  7  │ Adapter sends notification {token="abc123", progress=1, total=100}
  8  │ Client updates UI: "Analyzing File1.cs... 1%"
 ... │ ... (repeat for each file) ...
 99  │ LoggerUsageExtractor reports Progress(100/100, "Complete")
100  │ Adapter sends notification {token="abc123", progress=100, total=100}
101  │ Client updates UI: "Complete... 100%"
102  │ Server returns LoggerUsageExtractionResult
103  │ Client processes result
```

## Data Model Validation Checklist

- [x] All entities identified from requirements
- [x] Relationships between entities clear
- [x] Validation rules defined
- [x] State transitions documented
- [x] Mapping logic explicit
- [x] No unnecessary complexity (reuse existing types)
- [x] Follows existing patterns (IProgress<T> adapter)
- [x] Supports testability (adapter can be unit tested)

## Notes

**Why no new models?**
- `ProgressReport` already exists in LoggerUsage.Models
- MCP SDK provides `ProgressToken`, `ProgressNotificationParams`, `ProgressNotificationValue`
- Only need adapter class to bridge the two domains

**Design Rationale**:
- Adapter pattern keeps MCP concerns out of core library
- Existing `ProgressReport` model is domain-pure (no protocol coupling)
- `McpProgressAdapter` is protocol-specific (correct layering)
- Clear separation enables independent evolution of core library and MCP server

**Performance Considerations**:
- Adapter is lightweight (no allocations except notification params)
- Mapping is trivial (direct field copies)
- Async notification doesn't block analysis
- No buffering/caching needed at this layer
