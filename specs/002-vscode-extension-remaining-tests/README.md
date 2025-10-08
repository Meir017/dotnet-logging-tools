# Feature 002: VS Code Extension - Remaining Integration Tests

**Status**: 🚧 In Progress  
**Parent Feature**: [001-a-vscode-extension](../001-a-vscode-extension/)  
**Branch**: `002-vscode-extension-remaining-tests`  
**Created**: 2025-10-08

---

## Overview

This feature completes the VS Code extension by implementing functionality needed to make all integration tests pass. The core extension from feature 001 is operational, but 42 out of 43 integration tests are skipped with TODOs.

---

## Scope

### What's Included

1. **Error Handling Suite** (11 tests)
   - Bridge process crash recovery
   - Invalid solution handling
   - Compilation error handling with partial results
   - Missing .NET SDK detection
   - File system error handling
   - Analysis timeout support
   - Bridge communication error recovery
   - Concurrent analysis prevention
   - Missing dependency detection
   - Retry mechanisms
   - Output channel logging

2. **Full Workflow Completion** (11 skipped tests)
   - Auto-analysis on activation
   - Tree view implementation
   - Webview insights panel
   - Filter application
   - Navigate to insight
   - Problems panel (diagnostics)
   - Export functionality
   - Status bar updates
   - Search functionality

3. **Incremental Analysis** (10 tests)
   - File save watching
   - Single-file re-analysis
   - Debouncing rapid saves
   - State preservation for unchanged files
   - Configuration respect (autoAnalyzeOnSave)
   - Tree view & diagnostics updates
   - Progress feedback
   - File deletion handling
   - .csproj change detection

---

## Test Status

| Test Suite | Total | Passing | Skipped | Status |
|------------|-------|---------|---------|--------|
| fullWorkflow.test.ts | 12 | 1 | 11 | 🔄 In Progress |
| errorHandling.test.ts | 11 | 0 | 11 | 🔴 Not Started |
| incrementalAnalysis.test.ts | 10 | 0 | 10 | 🔴 Not Started |
| **TOTAL** | **33** | **1** | **32** | **3% Complete** |

---

## Documents

- [plan.md](./plan.md) - Implementation plan and architecture
- [tasks.md](./tasks.md) - 68 tasks organized by phase with dependencies

---

## Key Changes to Extension

### New Files Created

**Providers (UI Components)**:
- `LoggerTreeViewProvider.ts` - Hierarchical tree view
- `InsightsPanelProvider.ts` - Webview panel
- `DiagnosticsProvider.ts` - Problems panel integration

**State Management**:
- `InsightsState.ts` - Per-file insights storage

**Services**:
- `ExportService.ts` - JSON/CSV/Markdown export
- `analysisEvents.ts` - Event emitter for analysis lifecycle

**Utilities**:
- `dotnetDetector.ts` - .NET SDK detection
- `debounce.ts` - Debounce utility
- `logger.ts` - Output channel logging

**Bridge Updates**:
- `WorkspaceAnalyzer.AnalyzeFile()` - Single-file analysis method
- Error codes and structured error responses

---

## Dependencies

### Prerequisites from Parent Feature

The following must be complete from 001-a-vscode-extension:
- ✅ Extension activation and command registration
- ✅ Basic analysis service (bridge communication)
- ✅ Configuration system
- ✅ Bridge project (LoggerUsage.VSCode.Bridge)
- ✅ Basic models and DTOs

### External Dependencies

- VS Code 1.85+
- .NET 10 SDK
- LoggerUsage library
- LoggerUsage.MSBuild

---

## Implementation Phases

1. **Phase 1**: Test Infrastructure (3 tasks) - Helpers and fixtures
2. **Phase 2**: Full Workflow (21 tasks) - UI components and features
3. **Phase 3**: Error Handling (15 tasks) - Robust error handling
4. **Phase 4**: Incremental Analysis (10 tasks) - File watching and state
5. **Phase 5**: Test Completion (4 tasks) - Enable and verify tests
6. **Phase 6**: Documentation (4 tasks) - Update docs and changelog

**Total**: 57 tasks over ~11-15 days

---

## Success Criteria

✅ Feature is complete when:
- [ ] All 57 tasks in tasks.md completed
- [ ] All 32 integration tests passing (0 skipped)
- [ ] Extension installable and functional
- [ ] Error scenarios handled gracefully
- [ ] Incremental analysis < 2s for single file
- [ ] Documentation updated

---

## How to Use This Feature Spec

1. **Understand scope**: Read this README and plan.md
2. **Review tests**: Check integration test files to see what needs to pass
3. **Follow tasks**: Work through tasks.md sequentially or pick [P] tasks for parallel work
4. **Test frequently**: Run integration tests after implementing features
5. **Update docs**: Mark tasks complete, update this README

---

## Related Issues

- See integration test files in `test/LoggerUsage.VSCode.Tests/integration/`
- Each test contains TODO comments describing expected behavior

---

## Architecture Notes

This feature maintains the architecture from parent feature 001:
- TypeScript extension + C# bridge
- Event-driven UI updates
- State management pattern for insights
- VS Code API best practices

New patterns introduced:
- EventEmitter for analysis lifecycle
- Per-file insights storage (Map<filePath, insights[]>)
- Debouncing for file save events
- Output channel for debugging

---

**Last Updated**: 2025-10-08  
**Next Steps**: Start with Phase 1 tasks (test infrastructure)
