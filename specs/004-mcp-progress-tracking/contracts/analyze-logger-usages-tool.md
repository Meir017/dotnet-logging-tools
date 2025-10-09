# API Contract: analyze_logger_usages_in_csproj Tool

**Feature**: 005-mcp-progress-tracking
**Type**: MCP Tool Enhancement
**Date**: 2025-10-09

## Tool Signature

### Request

**Tool Name**: `analyze_logger_usages_in_csproj`

**Parameters**:

```json
{
  "fullPathToCsproj": "string (required)",
  "progressToken": "string | number | null (optional)"
}
```

**Parameter Specifications**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `fullPathToCsproj` | string | Yes | Absolute file path to the .csproj file to analyze |
| `progressToken` | ProgressToken? | No | Optional token for progress tracking. If provided, server sends progress notifications |

**Example Requests**:

```json
// Without progress tracking (backward compatible)
{
  "fullPathToCsproj": "C:\\Projects\\MyApp\\MyApp.csproj"
}

// With progress tracking (new feature)
{
  "fullPathToCsproj": "C:\\Projects\\MyApp\\MyApp.csproj",
  "progressToken": "progress-123"
}
```

### Response

**Type**: `LoggerUsageExtractionResult`

**Structure**:

```json
{
  "Usages": [
    {
      "MethodType": "string",
      "MethodName": "string",
      "MessageTemplate": "string",
      "Location": {
        "FilePath": "string",
        "LineNumber": 0,
        "CharacterPosition": 0
      },
      "EventId": "number | null",
      "LogLevel": "string | null",
      "MessageParameters": ["string"]
    }
  ],
  "Summary": {
    "TotalUsages": 0,
    "FileCount": 0,
    "MethodTypeCounts": {},
    "LogLevelCounts": {},
    "MostCommonLogLevel": "string | null",
    "UniqueEventIds": 0,
    "AverageParametersPerUsage": 0.0
  }
}
```

**Response Specification**:
- Response structure unchanged from existing behavior
- Progress tracking does NOT affect response body
- Progress is communicated via separate notifications (see below)

### Progress Notifications (New)

**When**: Sent during analysis if `progressToken` provided

**Notification Method**: `notifications/progress`

**Notification Parameters**:

```json
{
  "progressToken": "string | number",
  "progress": {
    "progress": 0,
    "total": 0,
    "message": "string | null"
  }
}
```

**Notification Structure**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `progressToken` | ProgressToken | Yes | Echo of token from request |
| `progress.progress` | int | Yes | Current progress value (files analyzed) |
| `progress.total` | int | Yes | Total progress value (total files) |
| `progress.message` | string? | No | Optional message (e.g., "Analyzing File.cs") |

**Example Notification Sequence**:

```json
// First notification (starting)
{
  "method": "notifications/progress",
  "params": {
    "progressToken": "progress-123",
    "progress": {
      "progress": 0,
      "total": 100,
      "message": "Starting analysis"
    }
  }
}

// Progress notification (during analysis)
{
  "method": "notifications/progress",
  "params": {
    "progressToken": "progress-123",
    "progress": {
      "progress": 25,
      "total": 100,
      "message": "Analyzing MyFile.cs"
    }
  }
}

// Final notification (completion)
{
  "method": "notifications/progress",
  "params": {
    "progressToken": "progress-123",
    "progress": {
      "progress": 100,
      "total": 100,
      "message": null
    }
  }
}
```

## Contract Validation Rules

### Request Validation

1. **fullPathToCsproj**:
   - Must not be null or empty
   - Must be a valid file path
   - File must exist and have .csproj extension
   - (Validation: existing behavior, unchanged)

2. **progressToken**:
   - Optional parameter (can be omitted or null)
   - If provided, must be non-empty string or valid number
   - No uniqueness constraint (client responsibility)

### Response Validation

1. **LoggerUsageExtractionResult**:
   - Must conform to existing schema
   - All fields must be populated correctly
   - (Validation: existing behavior, unchanged)

### Progress Notification Validation

1. **progressToken** in notification:
   - Must exactly match token from request
   - Type must match (string if request was string, number if number)

2. **progress values**:
   - `progress` >= 0
   - `total` > 0
   - `progress` <= `total`
   - Values must be monotonically increasing (or stay equal)

3. **message**:
   - Can be null or empty string
   - Should be meaningful when provided

## Backward Compatibility

### Existing Clients (No Changes)

- Clients that don't provide `progressToken` → No change in behavior
- Tool works exactly as before
- No progress notifications sent
- Response structure unchanged

### New Clients (Opt-In Feature)

- Clients that provide `progressToken` → Receive progress notifications
- Must register handler for `"notifications/progress"` method
- Response structure still unchanged

**Breaking Changes**: NONE

**API Version**: No version change required (additive change)

## Error Scenarios

### Scenario 1: Invalid csproj Path

**Request**:
```json
{
  "fullPathToCsproj": "C:\\NonExistent\\Project.csproj",
  "progressToken": "progress-123"
}
```

**Behavior**:
- Tool throws exception (existing behavior)
- No progress notifications sent
- Error returned to client

### Scenario 2: Progress Notification Failure

**Request**:
```json
{
  "fullPathToCsproj": "C:\\Projects\\MyApp\\MyApp.csproj",
  "progressToken": "progress-123"
}
```

**Behavior**:
- Analysis continues normally
- Failed notification logged (warning level)
- Remaining notifications attempted
- Response returned successfully

**Error NOT propagated to client**

### Scenario 3: Client Disconnects During Analysis

**Request**:
```json
{
  "fullPathToCsproj": "C:\\Projects\\MyApp\\MyApp.csproj",
  "progressToken": "progress-123"
}
```

**Behavior**:
- Analysis continues to completion
- Progress notifications fail (client unreachable)
- Failures logged
- Response would be returned (but client won't receive it)

**No server-side error**

## Performance Contract

### Requirements

1. **Progress Overhead**: <5% increase in analysis time
2. **Notification Latency**: Best effort, no blocking
3. **Memory**: No significant increase (stateless adapter)

### Measurement Points

- Benchmark test: Analyze project with 100 files
- Compare: with progressToken vs without progressToken
- Metric: Total analysis time
- Threshold: <5% difference

## Contract Tests

### Test Cases (to be implemented)

1. **CT-001: Tool with no progress token**
   - Request: `{ fullPathToCsproj: "valid.csproj" }`
   - Assert: Response valid, no progress notifications

2. **CT-002: Tool with progress token**
   - Request: `{ fullPathToCsproj: "valid.csproj", progressToken: "test-123" }`
   - Assert: Response valid, progress notifications sent

3. **CT-003: Progress notification structure**
   - Request: With progress token
   - Assert: Notifications match schema (progressToken, progress, total, message?)

4. **CT-004: Progress values correct**
   - Request: With progress token
   - Assert: progress starts at 0, ends at total, values increase

5. **CT-005: Progress token echoed correctly**
   - Request: `{ progressToken: "my-token" }`
   - Assert: All notifications include `progressToken: "my-token"`

6. **CT-006: Single file analysis**
   - Request: Project with 1 file, with progress token
   - Assert: Notifications show progress 0/1, then 1/1

7. **CT-007: Multiple files analysis**
   - Request: Project with 10 files, with progress token
   - Assert: Notifications show progress 0/10, 1/10, ..., 10/10

## OpenAPI Schema (Informational)

**Note**: MCP tools don't use OpenAPI, but this shows the logical structure

```yaml
openapi: 3.1.0
info:
  title: LoggerUsage MCP Tool
  version: 2.0.0
components:
  schemas:
    AnalyzeRequest:
      type: object
      required:
        - fullPathToCsproj
      properties:
        fullPathToCsproj:
          type: string
          description: Absolute path to .csproj file
        progressToken:
          oneOf:
            - type: string
            - type: number
            - type: "null"
          description: Optional progress tracking token
    
    ProgressNotification:
      type: object
      required:
        - progressToken
        - progress
      properties:
        progressToken:
          oneOf:
            - type: string
            - type: number
        progress:
          type: object
          required:
            - progress
            - total
          properties:
            progress:
              type: integer
              minimum: 0
            total:
              type: integer
              minimum: 1
            message:
              type: string
              nullable: true
```

## Contract Checklist

- [x] Request parameters defined
- [x] Response structure defined
- [x] Progress notification structure defined
- [x] Validation rules specified
- [x] Error scenarios documented
- [x] Backward compatibility verified
- [x] Performance contract stated
- [x] Test cases outlined
- [x] Breaking changes identified (none)
