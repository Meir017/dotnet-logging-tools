# Quickstart: MCP Progress Tracking

**Feature**: 005-mcp-progress-tracking
**Date**: 2025-10-09
**Time to Complete**: ~5 minutes

## Overview

This quickstart demonstrates how to use MCP progress tracking with the `analyze_logger_usages_in_csproj` tool. You'll see progress notifications during analysis of a C# project.

## Prerequisites

- LoggerUsage.Mcp server running
- MCP client that supports progress notifications
- Test C# project with multiple .cs files

## Step 1: Prepare Test Project

Create or use an existing C# project with at least 10 C# files to see meaningful progress updates.

**Example structure**:
```
TestProject/
├── TestProject.csproj
├── File1.cs
├── File2.cs
├── ...
└── File10.cs
```

## Step 2: Call Tool Without Progress (Baseline)

**Request**:
```json
{
  "tool": "analyze_logger_usages_in_csproj",
  "parameters": {
    "fullPathToCsproj": "C:\\TestProject\\TestProject.csproj"
  }
}
```

**Expected Outcome**:
- ✅ Analysis completes
- ✅ Returns `LoggerUsageExtractionResult`
- ❌ No progress notifications (because no token provided)

**Validation**:
- Response contains `Usages` array
- Response contains `Summary` object
- No `notifications/progress` received

## Step 3: Call Tool With Progress Token

**Request**:
```json
{
  "tool": "analyze_logger_usages_in_csproj",
  "parameters": {
    "fullPathToCsproj": "C:\\TestProject\\TestProject.csproj",
    "progressToken": "quickstart-test-123"
  }
}
```

**Expected Outcome**:
- ✅ Progress notifications received during analysis
- ✅ Same result as Step 2 (data unchanged)
- ✅ Notifications include token `"quickstart-test-123"`

## Step 4: Observe Progress Notifications

**Notification Pattern**:

### Initial Notification
```json
{
  "method": "notifications/progress",
  "params": {
    "progressToken": "quickstart-test-123",
    "progress": {
      "progress": 0,
      "total": 10,
      "message": "Starting analysis"
    }
  }
}
```

### Progress Notifications (one per file)
```json
{
  "method": "notifications/progress",
  "params": {
    "progressToken": "quickstart-test-123",
    "progress": {
      "progress": 1,
      "total": 10,
      "message": "Analyzing File1.cs"
    }
  }
}
```

### Final Notification
```json
{
  "method": "notifications/progress",
  "params": {
    "progressToken": "quickstart-test-123",
    "progress": {
      "progress": 10,
      "total": 10,
      "message": null
    }
  }
}
```

**Validation Checklist**:
- [ ] Received notifications with method `"notifications/progress"`
- [ ] `progressToken` in all notifications matches request token
- [ ] `progress.progress` starts at 0
- [ ] `progress.total` equals number of files
- [ ] `progress.progress` increases (0, 1, 2, ..., total)
- [ ] Final notification has `progress == total`
- [ ] `progress.message` describes current activity (or null)

## Step 5: Verify Response Unchanged

Compare responses from Step 2 (no token) and Step 3 (with token):

**Expected**:
- Response structure identical
- All fields match
- Progress tracking is transparent to result

**Validation**:
```csharp
// Pseudocode
var resultWithoutProgress = Step2Response;
var resultWithProgress = Step3Response;

Assert.Equal(resultWithoutProgress.Usages.Count, resultWithProgress.Usages.Count);
Assert.Equal(resultWithoutProgress.Summary.TotalUsages, resultWithProgress.Summary.TotalUsages);
// ... all fields should match
```

## Step 6: Test Error Resilience

Simulate progress notification failure (e.g., disconnect client after request sent):

**Request**:
```json
{
  "tool": "analyze_logger_usages_in_csproj",
  "parameters": {
    "fullPathToCsproj": "C:\\TestProject\\TestProject.csproj",
    "progressToken": "disconnect-test"
  }
}
```

**Client Action**: Disconnect immediately after sending request

**Expected Server Behavior**:
- ✅ Analysis continues to completion
- ✅ Progress notifications attempted (will fail)
- ✅ Failures logged on server
- ✅ No server exception/crash

**Server Log Validation**:
```
[Warning] Failed to send progress notification for progress 1/10
[Warning] Failed to send progress notification for progress 2/10
...
[Information] Analysis completed successfully (results would be returned if client connected)
```

## Step 7: Benchmark Performance

Measure analysis time with/without progress:

**Without Progress**:
```json
{
  "fullPathToCsproj": "C:\\LargeProject\\LargeProject.csproj"
}
```
**Time**: `T1` seconds

**With Progress**:
```json
{
  "fullPathToCsproj": "C:\\LargeProject\\LargeProject.csproj",
  "progressToken": "benchmark-test"
}
```
**Time**: `T2` seconds

**Validation**:
- `(T2 - T1) / T1 < 0.05` → Overhead < 5% ✅
- If overhead >= 5% → Performance requirement FAILED ❌

## Common Issues & Solutions

### Issue 1: No Progress Notifications Received

**Symptoms**:
- Request sent with `progressToken`
- No `notifications/progress` received

**Possible Causes**:
1. Client not registered handler for `"notifications/progress"`
2. Server doesn't support progress (old version)
3. Network issue preventing notifications

**Solution**:
- Verify client registered notification handler BEFORE sending request
- Check server logs for notification send attempts
- Verify MCP SDK version supports progress

### Issue 2: Progress Values Incorrect

**Symptoms**:
- `progress` > `total`
- `progress` decreases
- `total` changes mid-analysis

**Possible Causes**:
- Bug in adapter or extractor
- Concurrent requests with same token (client error)

**Solution**:
- Ensure unique progress tokens per request
- Check server logs for warnings
- Report bug if values violate constraints

### Issue 3: Notifications After Completion

**Symptoms**:
- Receive progress notifications after response returned

**Possible Causes**:
- Race condition (notification sent after response)
- Async notification not awaited

**Solution**:
- This should not happen if implementation correct
- File bug if observed

## Success Criteria

Complete quickstart is successful when:

- [x] Tool works without progress token (backward compatibility)
- [x] Tool sends progress notifications when token provided
- [x] Progress notifications match expected structure
- [x] Progress values are valid (0 <= progress <= total)
- [x] Progress token echoed correctly
- [x] Response unchanged with/without progress
- [x] Server handles notification errors gracefully
- [x] Performance overhead < 5%

## Next Steps

After completing quickstart:

1. **Integration**: Integrate progress tracking in your client application
2. **UI**: Display progress bar or status updates to end users
3. **Monitoring**: Monitor server logs for progress-related warnings
4. **Feedback**: Report any issues or unexpected behavior

## Reference

- **Feature Spec**: [spec.md](./spec.md)
- **API Contract**: [contracts/analyze-logger-usages-tool.md](./contracts/analyze-logger-usages-tool.md)
- **MCP Progress Docs**: https://github.com/modelcontextprotocol/csharp-sdk/blob/f28639119b3596b0357ea4b979d3289cad54054a/docs/concepts/progress/progress.md
