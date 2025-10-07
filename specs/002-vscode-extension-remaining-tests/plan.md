# Implementation Plan: VS Code Extension - Remaining Integration Tests

**Branch**: `002-vscode-extension-remaining-tests` | **Date**: 2025-10-08
**Parent Feature**: `001-a-vscode-extension`
**Input**: Integration test files from `test/LoggerUsage.VSCode.Tests/integration/`

---

## Summary

Complete the VS Code extension by implementing features needed to make all integration tests pass. The core extension from 001-a-vscode-extension is functional but many integration tests are skipped with TODOs. This phase adds:

1. **Error Handling**: Bridge crash recovery, invalid solutions, compilation errors, missing dependencies, file system errors, concurrent requests
2. **Incremental Analysis**: File save watching, debouncing, state management, preserving other file insights
3. **Multi-Solution Support**: Detection, selection, switching, solution-specific data isolation
4. **Complete UI Features**: Tree view, webview panel, diagnostics, status bar, navigation, export
5. **Test Completion**: Enable all 43 skipped integration tests and make them pass

---

## Technical Context

**Language/Version**: TypeScript 5.x (Extension), C# .NET 10 (Bridge)
**Dependencies**: VS Code Extension API, existing LoggerUsage library
**Testing**: VS Code Extension Test Runner (Mocha)
**Test Coverage**: 43 integration tests across 4 suites (currently 1 passing, 42 skipped/TODOs)
**Project Type**: Continuation of VS Code Extension from 001-a-vscode-extension
**User Input Context**: Implement remaining integration test scenarios

---

## Constitution Check

*All constitutional gates were validated in parent feature 001-a-vscode-extension. This phase adds functionality without violating principles.*

### Additional Considerations for This Phase

- [x] **Error Handling (Principle 4)**: Extensive error handling added for all failure scenarios
  - Rationale: Tests explicitly verify graceful degradation, retry mechanisms, user-friendly errors
- [x] **Performance (Principle 6)**: Incremental analysis optimizes re-analysis performance
  - Rationale: Only re-analyze changed files, debounce rapid saves, cache solution loading
- [x] **Thread Safety (Principle 3)**: Concurrent analysis prevention added
  - Rationale: Tests verify no crashes on concurrent requests, proper queueing
- [x] **Test Coverage (Principle 2)**: 42 additional integration tests implemented
  - Rationale: Comprehensive E2E coverage for all user-facing features

**Constitution Check Result**: ✅ PASS - Enhances existing compliant architecture

---

## Project Structure

### New Files (This Phase)

```
src/LoggerUsage.VSCode/
├── src/
│   ├── analysisEvents.ts               # NEW: Event emitter for analysis lifecycle
│   ├── providers/
│   │   ├── LoggerTreeViewProvider.ts   # NEW: Tree view implementation
│   │   ├── InsightsPanelProvider.ts    # NEW: Webview panel provider
│   │   └── DiagnosticsProvider.ts      # NEW: Problems panel integration
│   ├── services/
│   │   └── ExportService.ts            # NEW: Export to JSON/CSV/Markdown
│   ├── state/
│   │   ├── InsightsState.ts            # NEW: Insights state management
│   │   └── SolutionState.ts            # NEW: Multi-solution state
│   └── utils/
│       ├── dotnetDetector.ts           # NEW: .NET SDK detection
│       ├── solutionDetector.ts         # NEW: Solution discovery
│       ├── debounce.ts                 # NEW: Debounce utility
│       └── logger.ts                   # NEW: Output channel logging
├── views/
│   └── insights.html                   # NEW: Webview HTML template

test/LoggerUsage.VSCode.Tests/
├── helpers/
│   ├── testFixtures.ts                 # NEW: Test workspace creation
│   ├── vscodeHelpers.ts                # NEW: VS Code API test helpers
│   └── eventListeners.ts               # NEW: Event capture for tests
└── integration/
    ├── fullWorkflow.test.ts            # UPDATED: Remove skips, add assertions
    ├── errorHandling.test.ts           # UPDATED: Remove skips, implement tests
    ├── incrementalAnalysis.test.ts     # UPDATED: Remove skips, implement tests
    └── multiSolution.test.ts           # UPDATED: Remove skips, implement tests

src/LoggerUsage.VSCode.Bridge/
├── WorkspaceAnalyzer.cs                # UPDATED: Add AnalyzeFile method
└── Models/
    └── Responses.cs                    # UPDATED: Add error codes
```

---

## Phase Breakdown

### Phase 1: Test Infrastructure (T001-T003)

Create helpers and fixtures to enable integration tests to run properly.

**Deliverables**:
- Test workspace creation utilities
- VS Code API mocking helpers
- Event listeners for async testing

### Phase 2: Full Workflow Tests (T004-T024)

Implement all UI components and features tested by fullWorkflow.test.ts.

**Deliverables**:
- Analysis event system
- Tree view provider (solution → projects → files → insights)
- Webview insights panel with filtering
- Diagnostics provider (Problems panel)
- Navigate to insight command
- Export service (JSON/CSV/Markdown)
- Status bar item with solution name
- Search functionality

### Phase 3: Error Handling Tests (T025-T039)

Implement robust error handling for all failure scenarios tested by errorHandling.test.ts.

**Deliverables**:
- Bridge process crash recovery with retry
- Invalid solution file handling
- Compilation errors with partial results
- .NET SDK detection and missing SDK errors
- File system error handling
- Analysis timeout (optional)
- Bridge communication error recovery
- Concurrent analysis prevention/queueing
- Missing dependency detection
- Output channel logging for debugging

### Phase 4: Incremental Analysis Tests (T040-T049)

Implement incremental file analysis tested by incrementalAnalysis.test.ts.

**Deliverables**:
- File save watcher with C# file filtering
- Incremental analysis in bridge (single file)
- Debouncing for rapid saves
- Insights state manager (per-file storage)
- UI updates after incremental analysis
- Diagnostics updates for single file
- Auto-analyze configuration option
- File deletion handling
- Full re-analysis on .csproj/.sln changes

### Phase 5: Multi-Solution Tests (T050-T059)

Implement multi-solution workspace support tested by multiSolution.test.ts.

**Deliverables**:
- Solution detection (all .sln files in workspace)
- Solution state manager with active solution tracking
- Select solution command with QuickPick
- Re-analysis on solution switch
- Solution-specific insights isolation
- Diagnostics clearing on switch
- Status bar updates with solution count
- Active solution detection from active editor file

### Phase 6: Test Completion (T060-T064)

Enable all skipped tests and verify they pass.

**Deliverables**:
- Remove `suite.skip` from all test suites
- Remove `test.skip` from all tests
- Implement assertions for all TODO comments
- Fix flaky tests
- All 43 integration tests passing

### Phase 7: Documentation (T065-T068)

Update all documentation to reflect new features.

**Deliverables**:
- Updated README with features and screenshots
- Updated CHANGELOG
- Updated parent plan.md
- Integration test documentation

---

## Task Generation Approach (Phase 2)

The `/tasks` command already executed - see tasks.md for complete breakdown.

**Task Summary**:
- Phase 1 (Setup): 3 tasks
- Phase 2 (Full Workflow): 21 tasks
- Phase 3 (Error Handling): 15 tasks
- Phase 4 (Incremental Analysis): 10 tasks
- Phase 5 (Multi-Solution): 10 tasks
- Phase 6 (Test Completion): 5 tasks
- Phase 7 (Documentation): 4 tasks

**Total**: 68 tasks

**Parallel Execution**: Many tasks marked [P] can run in parallel (different files/components)

---

## Key Design Decisions

### 1. Event-Driven Architecture
- **Decision**: Use EventEmitter for analysis lifecycle (started, progress, complete, error)
- **Rationale**: Enables loose coupling between analysis service and UI components
- **Impact**: Tree view, webview, diagnostics all subscribe to same events

### 2. State Management Pattern
- **Decision**: Separate state managers for Insights and Solutions
- **Rationale**: Clean separation of concerns, easier testing
- **Files**: `InsightsState.ts` (Map<filePath, insights[]>), `SolutionState.ts` (active solution tracking)

### 3. Incremental Analysis Strategy
- **Decision**: Bridge has separate `AnalyzeFile` method, extension maintains per-file insights map
- **Rationale**: Faster re-analysis (only changed file), preserves insights for other files
- **Performance**: Sub-second analysis for single file changes

### 4. Multi-Solution Model
- **Decision**: Only one active solution at a time, explicit switching via command or auto-detect from editor
- **Rationale**: Simplifies UI (one tree view, one set of diagnostics), matches user mental model
- **Trade-off**: Can't view insights from multiple solutions simultaneously (acceptable for MVP)

### 5. Error Handling Philosophy
- **Decision**: User-friendly messages, no stack traces, actionable suggestions, retry options
- **Rationale**: Extension users are developers but shouldn't need to debug extension internals
- **Examples**: "Missing .NET SDK" → "Download" button, Bridge crash → "Retry" button

### 6. Testing Strategy
- **Decision**: Tests already written, implement features to make them pass (TDD)
- **Rationale**: Tests define acceptance criteria, reduce implementation uncertainty
- **Coverage**: 43 E2E scenarios covering happy paths and edge cases

---

## Complexity Tracking

*No constitutional violations. This phase stays within established architecture.*

---

## Progress Tracking

**Phase Status**:
- [x] Phase 0: Research (inherited from parent feature)
- [x] Phase 1: Design (this document + tasks.md)
- [ ] Phase 2: Task execution (68 tasks in tasks.md)
- [ ] Phase 3: Test validation (all 43 tests passing)
- [ ] Phase 4: Documentation complete

**Gate Status**:
- [x] Initial Constitution Check: PASS (inherited)
- [x] Design Constitution Check: PASS (no violations introduced)
- [ ] All tasks complete
- [ ] All tests passing

---

## Integration with Parent Feature

This phase extends `001-a-vscode-extension` by:
- Adding error handling throughout (Tests: errorHandling.test.ts)
- Adding incremental analysis (Tests: incrementalAnalysis.test.ts)
- Adding multi-solution support (Tests: multiSolution.test.ts)
- Completing UI features (Tests: fullWorkflow.test.ts)

Core architecture from parent feature remains unchanged:
- TypeScript extension + C# bridge model preserved
- Existing LoggerUsage library integration unchanged
- Command structure extended, not replaced
- Configuration system extended, not replaced

---

## Success Criteria

This phase is complete when:
1. All 68 tasks in tasks.md are completed
2. All 43 integration tests pass (0 skipped)
3. Extension can be installed and used without errors
4. All error scenarios handled gracefully with user notifications
5. Incremental analysis working and performant (< 2s for single file)
6. Multi-solution workspaces fully supported
7. Documentation updated to reflect all features

---

*Based on Constitution v2.0.0 and parent feature 001-a-vscode-extension*
*Execution ready: Run tasks from tasks.md*
