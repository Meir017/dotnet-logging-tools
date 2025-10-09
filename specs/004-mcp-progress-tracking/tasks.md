# Tasks: MCP Progress Tracking Support

**Feature**: 004-mcp-progress-tracking
**Input**: Design documents from `specs/004-mcp-progress-tracking/`
**Prerequisites**: plan.md, research.md, data-model.md, contracts/, quickstart.md

## Overview

This feature adds MCP progress tracking support to the `analyze_logger_usages_in_csproj` tool in LoggerUsage.Mcp server. Tasks follow TDD approach: tests first, implementation second, validation third.

## Path Conventions
- Source: `src/LoggerUsage.Mcp/`
- Tests: `test/LoggerUsage.Mcp.Tests/`

## Phase 3.1: Setup
- [ ] T001 Add XML documentation for progressToken parameter to existing tool method in `src/LoggerUsage.Mcp/Program.cs`
- [ ] T002 Verify IMcpServer is available in DI container (check existing registrations in `src/LoggerUsage.Mcp/Program.cs`)

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

- [ ] T003 [P] Contract test: Tool with no progress token in `test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs`
  - Create test fixture with sample .csproj
  - Call tool without progressToken parameter
  - Assert: Response valid, no progress notifications sent
  - Expected: FAIL (implementation doesn't exist yet)

- [ ] T004 [P] Contract test: Tool with progress token in `test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs`
  - Call tool with progressToken = "test-123"
  - Assert: Response valid, progress notifications sent
  - Assert: notifications include progressToken "test-123"
  - Expected: FAIL (implementation doesn't exist yet)

- [ ] T005 [P] Contract test: Progress notification structure in `test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs`
  - Call tool with progressToken
  - Capture notifications
  - Assert: notifications match schema (progressToken, progress, total, message?)
  - Assert: progress values valid (0 <= progress <= total)
  - Expected: FAIL (implementation doesn't exist yet)

- [ ] T006 [P] Contract test: Progress values correctness in `test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs`
  - Call tool with progress token on multi-file project
  - Assert: progress starts at 0
  - Assert: progress increases monotonically
  - Assert: progress ends at total
  - Assert: total equals file count
  - Expected: FAIL (implementation doesn't exist yet)

- [ ] T007 [P] Contract test: Single file analysis in `test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs`
  - Create project with 1 .cs file
  - Call tool with progress token
  - Assert: Notifications show progress 0/1, then 1/1
  - Expected: FAIL (implementation doesn't exist yet)

- [ ] T008 [P] Integration test: Error handling in `test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs`
  - Mock IMcpServer.SendNotificationAsync to throw exception
  - Call tool with progress token
  - Assert: Analysis completes successfully (no exception propagated)
  - Assert: Result returned correctly
  - Verify: Exception logged (check logs)
  - Expected: FAIL (implementation doesn't exist yet)

## Phase 3.3: Core Implementation (ONLY after tests are failing)

- [ ] T009 Create McpProgressAdapter class in `src/LoggerUsage.Mcp/McpProgressAdapter.cs`
  - Implement IProgress<ProgressReport> interface
  - Constructor: Accept IMcpServer, ProgressToken, ILogger<McpProgressAdapter>
  - Report method: Map ProgressReport → ProgressNotificationParams
  - Report method: Call SendNotificationAsync with "notifications/progress"
  - Report method: Wrap in try-catch, log warnings on error
  - Add XML documentation to all public members
  - **Verify**: T003-T008 now pass (progress adapter functional)

- [ ] T010 Update AnalyzeLoggerUsagesInCsproj tool method in `src/LoggerUsage.Mcp/Program.cs`
  - Add optional parameter: `ProgressToken? progressToken = null`
  - Inject IMcpServer in tool constructor (if not already injected)
  - Create McpProgressAdapter when progressToken is provided
  - Pass adapter to LoggerUsageExtractor.ExtractLoggerUsagesWithSolutionAsync
  - Handle null progressToken (skip adapter creation)
  - **Verify**: All contract tests (T003-T007) now pass
  - **Verify**: Error handling test (T008) passes

## Phase 3.4: Integration & Validation

- [ ] T011 Run all existing LoggerUsage.Mcp.Tests to ensure no regressions
  - Expected: All existing tests pass (backward compatibility)
  - If failures: Fix regressions before proceeding

- [ ] T012 Create benchmark test in `test/LoggerUsage.Mcp.Tests/ProgressPerformanceBenchmarkTests.cs`
  - Analyze large project (100+ files) WITHOUT progress token
  - Measure time: T1
  - Analyze same project WITH progress token
  - Measure time: T2
  - Assert: (T2 - T1) / T1 < 0.05 (overhead < 5%)
  - Expected: PASS (progress should be lightweight)

- [ ] T013 Execute quickstart validation in `specs/004-mcp-progress-tracking/quickstart.md`
  - Follow all steps in quickstart guide
  - Verify: Step 2 works (no progress token)
  - Verify: Step 3 works (with progress token)
  - Verify: Step 4 notifications match expected structure
  - Verify: Step 5 responses identical
  - Verify: Step 6 error resilience works
  - Verify: Step 7 performance < 5% overhead
  - Expected: All steps pass

## Phase 3.5: Polish & Documentation

- [ ] T014 [P] Add XML documentation to all public APIs
  - Ensure McpProgressAdapter has XML docs (done in T009)
  - Ensure progressToken parameter documented (done in T001)
  - Review all XML docs for clarity
  - Expected: No warnings from compiler

- [ ] T015 [P] Update README or MCP server documentation
  - Document progress tracking feature
  - Add example usage with progressToken
  - Link to MCP progress specification
  - Document error handling behavior
  - Location: `src/LoggerUsage.Mcp/README.md` (if exists) or repository README

- [ ] T016 Verify no code duplication
  - Review McpProgressAdapter for reusability
  - Check for repeated mapping logic
  - Refactor if needed

- [ ] T017 Final validation: Run all tests
  - Run: `dotnet test`
  - Expected: All tests pass
  - Expected: No warnings
  - Expected: Code coverage adequate

## Dependencies

### Phase Order
```
Setup (T001-T002)
    ↓
Tests First (T003-T008) [ALL MUST FAIL]
    ↓
Implementation (T009-T010)
    ↓
Integration (T011-T013)
    ↓
Polish (T014-T017)
```

### Task Dependencies
- **T009** (adapter) blocks **T010** (tool update)
- **T003-T008** must complete before **T009** (TDD principle)
- **T010** must complete before **T011-T013** (integration needs implementation)
- **T012** requires **T010** (benchmark needs feature complete)
- **T013** requires **T010** (quickstart needs feature complete)

## Parallel Execution Examples

### Phase 3.2: All Contract Tests in Parallel
```powershell
# These tests can be written simultaneously (different test methods)
# Agent 1:
Task: "Contract test: Tool with no progress token in test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs"

# Agent 2:
Task: "Contract test: Tool with progress token in test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs"

# Agent 3:
Task: "Contract test: Progress notification structure in test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs"

# Agent 4:
Task: "Contract test: Progress values correctness in test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs"

# Agent 5:
Task: "Contract test: Single file analysis in test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs"

# Agent 6:
Task: "Integration test: Error handling in test/LoggerUsage.Mcp.Tests/ProgressTrackingTests.cs"
```

**Note**: While tasks are marked [P], they're all in same file (ProgressTrackingTests.cs), so different test methods can be written concurrently if file merging is handled.

### Phase 3.5: Documentation Tasks in Parallel
```powershell
# Agent 1:
Task: "Add XML documentation to all public APIs"

# Agent 2:
Task: "Update README or MCP server documentation"
```

## Task Validation Checklist

### Coverage
- [x] Contract has corresponding tests (analyze-logger-usages-tool.md → T003-T007)
- [x] Data entities covered (ProgressReport exists, McpProgressAdapter → T009)
- [x] Error scenarios tested (T008)
- [x] Performance validated (T012)
- [x] Integration scenarios covered (T013 quickstart)

### TDD Principles
- [x] All tests written before implementation (T003-T008 before T009-T010)
- [x] Tests expected to fail initially (documented in task descriptions)
- [x] Implementation makes tests pass (verified in T009-T010)

### Constitutional Compliance
- [x] Symbol Fidelity: N/A (no Roslyn analysis)
- [x] Thread Safety: N/A (stateless adapter)
- [x] Error Handling: T008 validates graceful degradation
- [x] Performance: T012 validates <5% overhead constraint
- [x] Test-First: T003-T008 before T009-T010
- [x] Documentation: T014-T015 ensure XML docs and README updates

### Completeness
- [x] Setup tasks defined (T001-T002)
- [x] Test tasks defined (T003-T008)
- [x] Implementation tasks defined (T009-T010)
- [x] Integration tasks defined (T011-T013)
- [x] Polish tasks defined (T014-T017)
- [x] Dependencies explicit
- [x] Parallel tasks identified
- [x] File paths specified

## Estimated Timeline

- **Phase 3.1 (Setup)**: 15 minutes
- **Phase 3.2 (Tests)**: 2-3 hours (6 test cases)
- **Phase 3.3 (Implementation)**: 2-3 hours (adapter + tool update)
- **Phase 3.4 (Integration)**: 1-2 hours (validation + benchmark)
- **Phase 3.5 (Polish)**: 1 hour (docs + review)
- **Total**: 6-9 hours

## Notes

- **Test file location**: All tests in single file (`ProgressTrackingTests.cs`) for cohesion
- **Backward compatibility**: Verified by T011 (existing tests must pass)
- **Performance**: Strict requirement (<5% overhead) validated by T012
- **Error resilience**: Critical requirement validated by T008
- **TDD enforcement**: Tests T003-T008 MUST fail before implementing T009-T010

## Success Criteria

Feature is complete when:
- [x] All tasks T001-T017 marked complete
- [x] All tests pass (including existing tests)
- [x] Performance benchmark < 5% overhead
- [x] Quickstart validation passes all steps
- [x] XML documentation complete
- [x] README updated
- [x] No code duplication
- [x] Constitutional principles verified
