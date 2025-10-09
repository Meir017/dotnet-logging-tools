# Feature Specification: MCP Progress Tracking Support

**Feature Branch**: `005-mcp-progress-tracking`  
**Created**: 2025-10-09  
**Status**: Draft  
**Input**: Support progress tracking in the MCP server using the MCP C# SDK's progress notification capabilities

## Overview

Add support for Model Context Protocol (MCP) progress tracking to the LoggerUsage.Mcp server, enabling clients to receive real-time progress updates during long-running analysis operations. This leverages the existing `IProgress<T>` support in `LoggerUsageExtractor` and the MCP C# SDK's progress notification system.

**Reference**: https://github.com/modelcontextprotocol/csharp-sdk/blob/f28639119b3596b0357ea4b979d3289cad54054a/docs/concepts/progress/progress.md

## User Scenarios & Testing

### Primary User Story
An AI assistant uses the MCP server to analyze a large .NET project with 100+ C# files. The analysis takes 10 seconds. During this time, the assistant wants to show the user progress updates (e.g., "Analyzing file 25 of 100") so the user knows the operation is progressing and hasn't hung.

### Acceptance Scenarios

1. **Given** a client calls `analyze_logger_usages_in_csproj` with a progress token, **When** the analysis processes multiple files, **Then** the client receives progress notifications for each file showing current count and total count

2. **Given** a client calls `analyze_logger_usages_in_csproj` without a progress token, **When** the analysis completes, **Then** the analysis succeeds and returns results without sending any progress notifications

3. **Given** analysis is processing files, **When** a progress notification is sent, **Then** the notification includes the progress token, current file number, total files, and an optional message describing what's being analyzed

4. **Given** analysis encounters an error while sending progress notifications, **When** the error occurs, **Then** the analysis continues and completes successfully, logging the progress error for diagnostics

### Edge Cases

- **What happens when there's only 1 file to analyze?** → Single progress notification sent (1 of 1) before completion
- **What happens if progress notification sending fails?** → Analysis continues, error logged, no impact to client result
- **What happens when client disconnects during analysis?** → Best effort: notifications may fail, analysis completes normally
- **What happens if no files found in project?** → No progress notifications sent (0 total), empty result returned

## Requirements

### Functional Requirements

- **FR-001**: Tool MUST accept an optional `progressToken` parameter that clients can provide to request progress updates
- **FR-002**: Tool MUST send progress notifications when `progressToken` is provided, reporting current file count and total file count
- **FR-003**: Progress notifications MUST include the client's progress token, current progress value, total value, and optional message
- **FR-004**: Tool MUST work correctly when `progressToken` is not provided, analyzing files without sending progress notifications
- **FR-005**: Tool MUST send progress updates at meaningful intervals (per-file granularity based on existing `ProgressReport` model)
- **FR-006**: Progress notifications MUST follow MCP specification format using `ProgressNotificationParams` structure
- **FR-007**: Tool MUST handle progress notification errors gracefully without failing the analysis operation
- **FR-008**: Progress messages MUST include context about what's being analyzed when available (e.g., file name)

### Key Entities

- **ProgressToken**: MCP protocol type identifying a specific progress tracking session, provided by client
- **ProgressReport**: Existing model from LoggerUsageExtractor containing current progress state (current count, total count, message)
- **ProgressNotificationParams**: MCP protocol type containing the progress token and progress details to send to client
- **ProgressNotificationValue**: MCP protocol type containing the numeric progress values and optional message

## Review & Acceptance Checklist

### Content Quality
- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

### Requirement Completeness
- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous  
- [x] Success criteria are measurable
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Execution Status

- [x] User description parsed
- [x] Key concepts extracted
- [x] Ambiguities marked (none remaining)
- [x] User scenarios defined
- [x] Requirements generated
- [x] Entities identified
- [x] Review checklist passed

## Clarifications

### Session 1: Initial Feature Definition (2025-10-09)

**Q1: Should progress notifications be sent for every file or batched?**
**A**: Send per-file for simplicity. The `ProgressReport` model already provides per-file granularity. Performance overhead should be minimal since file count is typically in the hundreds, not thousands.

**Q2: What should the progress message contain?**
**A**: Use the file name being analyzed when available (e.g., "Analyzing MyFile.cs"). The `ProgressReport` model includes a `Message` property that can be passed through. If message is not available, send `null` per MCP specification.

**Q3: Should we create a progress adapter/wrapper class?**
**A**: Yes, encapsulate the bridging logic between `IProgress<ProgressReport>` and MCP notifications in a dedicated class. This improves testability and follows separation of concerns.

**Q4: How should we handle errors during progress notification sending?**
**A**: Catch and log exceptions from `SendNotificationAsync` but don't fail the analysis. Progress is best-effort. Use structured logging with exception details.

**Q5: Should progress token be part of the tool method signature or in a separate parameter object?**
**A**: Add directly to the method signature as an optional parameter. This follows MCP C# SDK patterns and keeps the API simple.

**Q6: Do we need to update the JSON schema version?**
**A**: No. The output type (`LoggerUsageExtractionResult`) is unchanged. Progress is transmitted via notifications, not in the response body.

**Q7: Should we test that progress notifications stop after analysis completes?**
**A**: Yes. Integration tests should verify that no progress notifications are sent after the tool method returns, ensuring clean completion
