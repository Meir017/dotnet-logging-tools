# Tasks: VS Code Extension - Remaining Integration Tests

**Branch**: `002-vscode-extension-remaining-tests` | **Date**: 2025-10-08
**Input**: Integration test files from `test/LoggerUsage.VSCode.Tests/integration/`
**Prerequisites**: Core extension implementation complete (from 001-a-vscode-extension)

---

## Overview

This task list covers implementing the remaining features needed to make all integration tests pass. The tests are currently skipped or contain TODOs. Tasks are organized by test suite and follow TDD principles - implement features to make tests pass.

**Total Test Cases**: 43 tests across 4 suites
- fullWorkflow.test.ts: 12 tests (1 passing, 11 skipped)
- errorHandling.test.ts: 11 tests (all skipped)
- incrementalAnalysis.test.ts: 10 tests (all skipped)
- multiSolution.test.ts: 11 tests (all skipped)

---

## Task Format

`- [ ] T### [P?] Description in file/path`

- **[P]**: Can run in parallel (different files, no dependencies)
- **No [P]**: Must run sequentially (dependencies or same file)

---

## Phase 1: Test Infrastructure & Setup

These tasks enable the integration tests to run properly.

- [X] **T001** Create test fixture infrastructure
  - File: `test/LoggerUsage.VSCode.Tests/helpers/testFixtures.ts`
  - Function: `createTestWorkspace()` - creates temp workspace with sample .sln
  - Function: `createSampleCSharpFile(logCalls)` - generates C# file with logging
  - Function: `createCorruptedSolution()` - generates invalid .sln for error tests
  - Function: `cleanupTestWorkspace()` - removes temp files
  - Export all functions

- [X] **T002** Create test helpers for VS Code API mocking
  - File: `test/LoggerUsage.VSCode.Tests/helpers/vscodeHelpers.ts`
  - Function: `waitForCommand(commandId)` - waits for command to be registered
  - Function: `waitForAnalysis()` - waits for analysis completion event
  - Function: `getTreeViewItems()` - retrieves tree view provider items
  - Function: `getWebviewPanel()` - retrieves active webview panel
  - Function: `getDiagnostics(uri?)` - gets diagnostics from collection
  - Function: `sendWebviewMessage(message)` - sends message to webview
  - Export all functions

- [X] **T003** Create test event listeners
  - File: `test/LoggerUsage.VSCode.Tests/helpers/eventListeners.ts`
  - Class: `AnalysisEventCapture` - captures analysis start/complete events
  - Method: `waitForEvent(eventName, timeout)` - promise-based event waiting
  - Method: `getProgressMessages()` - returns captured progress messages
  - Method: `reset()` - clears captured events
  - Export class

---

## Phase 2: Full Workflow Tests (fullWorkflow.test.ts)

Implement features to make these tests pass. Tests are in `test/LoggerUsage.VSCode.Tests/integration/fullWorkflow.test.ts`.

### T004-T006: Analysis Event Infrastructure

- [X] **T004** [P] Implement analysis events in extension
  - File: `src/LoggerUsage.VSCode/src/analysisEvents.ts`
  - Create EventEmitter for analysis lifecycle events
  - Events: `analysisStarted`, `analysisProgress`, `analysisComplete`, `analysisError`
  - Export event emitter singleton
  - Test: fullWorkflow.test.ts - "Should run analysis automatically on activation"

- [X] **T005** [P] Add event emission to analysis service
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - Import analysisEvents emitter
  - Emit `analysisStarted` when analysis begins
  - Emit `analysisProgress` for each progress update
  - Emit `analysisComplete` with results when done
  - Emit `analysisError` on failure
  - Test: fullWorkflow.test.ts - "Should run analysis automatically on activation"

- [X] **T006** Update test helper to listen for events
  - File: `test/LoggerUsage.VSCode.Tests/helpers/vscodeHelpers.ts`
  - Update `waitForAnalysis()` to listen to analysisEvents
  - Return promise that resolves on `analysisComplete` or rejects on `analysisError`
  - Add timeout parameter (default 30000ms)
  - Test: fullWorkflow.test.ts - "Should run analysis automatically on activation"

### T007-T009: Tree View Implementation

- [X] **T007** [P] Implement tree view data provider
  - File: `src/LoggerUsage.VSCode/src/providers/LoggerTreeViewProvider.ts`
  - Implement `vscode.TreeDataProvider<TreeNode>` interface
  - Method: `getTreeItem(element)` - returns TreeItem with label, icon, command
  - Method: `getChildren(element?)` - returns hierarchy: solution → projects → files → insights
  - Property: `onDidChangeTreeData` - EventEmitter for refresh
  - Method: `refresh(insights)` - updates tree data, fires change event
  - Test: fullWorkflow.test.ts - "Should display insights in tree view after analysis"

- [X] **T008** Register tree view in extension activation
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - Import LoggerTreeViewProvider
  - Create provider instance
  - Register with `vscode.window.registerTreeDataProvider('loggerUsage.insights', provider)`
  - Call `provider.refresh(insights)` after analysis complete
  - Store provider reference for disposal
  - Test: fullWorkflow.test.ts - "Should display insights in tree view after analysis"

- [X] **T009** Update package.json contributions for tree view
  - File: `src/LoggerUsage.VSCode/package.json`
  - Add views contribution: `loggerUsage.insights` in `explorer` container
  - Add view title: "Logger Usage"
  - Add view icon (optional)
  - Test: fullWorkflow.test.ts - "Should display insights in tree view after analysis"

### T010-T013: Insights Panel (Webview) Implementation

- [X] **T010** [P] Implement insights panel webview provider
  - File: `src/LoggerUsage.VSCode/src/providers/InsightsPanelProvider.ts`
  - Class: `InsightsPanelProvider`
  - Method: `createOrShow(insights, summary)` - creates or reveals webview panel
  - Method: `generateHtml()` - loads HTML template, injects data
  - Method: `handleMessage(message)` - routes webview messages
  - Method: `dispose()` - cleanup
  - Test: fullWorkflow.test.ts - "Should show insights panel on command execution"

- [X] **T011** Create webview HTML template
  - File: `src/LoggerUsage.VSCode/views/insights.html`
  - Table structure with columns: Method Type, Message, Log Level, Event ID, Location
  - Filter controls: search input, log level checkboxes, method type dropdown
  - Summary statistics section
  - Empty state message
  - Inline CSS using VS Code theme variables
  - Inline JavaScript for filtering and message passing
  - Test: fullWorkflow.test.ts - "Should show insights panel on command execution"

- [X] **T012** Register showInsightsPanel command
  - File: `src/LoggerUsage.VSCode/src/commands/index.ts`
  - Command: `loggerUsage.showInsightsPanel`
  - Handler: calls `InsightsPanelProvider.createOrShow(currentInsights, summary)`
  - Register in extension activation
  - Test: fullWorkflow.test.ts - "Should show insights panel on command execution"

- [X] **T013** Implement filter application in webview
  - File: `src/LoggerUsage.VSCode/views/insights.html` (inline JavaScript)
  - Function: `applyFilters()` - filters insights array based on current filter state
  - Function: `renderTable(filteredInsights)` - updates table DOM
  - Event: listen for filter control changes, call applyFilters()
  - Event: listen for messages from extension, update insights
  - Test: fullWorkflow.test.ts - "Should apply filters and update table"

### T014-T015: Navigate to Insight

- [X] **T014** [P] Implement navigateToInsight command
  - File: `src/LoggerUsage.VSCode/src/commands/index.ts`
  - Command: `loggerUsage.navigateToInsight`
  - Handler: accepts insight object or ID
  - Parse location (filePath, startLine, startColumn)
  - Open document: `vscode.workspace.openTextDocument(filePath)`
  - Show editor: `vscode.window.showTextDocument(doc)`
  - Reveal range: `editor.revealRange(range, vscode.TextEditorRevealType.InCenter)`
  - Set cursor position: `editor.selection = new vscode.Selection(position, position)`
  - Test: fullWorkflow.test.ts - "Should navigate to file location when clicking insight"

- [X] **T015** Wire navigation from tree view and webview
  - File: `src/LoggerUsage.VSCode/src/providers/LoggerTreeViewProvider.ts`
  - Set TreeItem.command to `loggerUsage.navigateToInsight` with insight as arg
  - File: `src/LoggerUsage.VSCode/views/insights.html`
  - Add click handler to table rows, send message to extension
  - Extension forwards to navigateToInsight command
  - Test: fullWorkflow.test.ts - "Should navigate to file location when clicking insight"

### T016-T018: Problems Panel (Diagnostics)

- [X] **T016** [P] Implement diagnostics provider
  - File: `src/LoggerUsage.VSCode/src/providers/DiagnosticsProvider.ts`
  - Class: `DiagnosticsProvider`
  - Constructor: creates diagnostic collection `vscode.languages.createDiagnosticCollection('loggerUsage')`
  - Method: `publishDiagnostics(insights)` - creates diagnostics for inconsistencies
  - Method: `clearDiagnostics()` - clears all diagnostics
  - Method: `clearFileDiagnostics(uri)` - clears diagnostics for specific file
  - Method: `dispose()` - disposes collection
  - Map inconsistency type to DiagnosticSeverity (Warning/Error)
  - Test: fullWorkflow.test.ts - "Should show diagnostics in Problems panel"

- [X] **T017** Integrate diagnostics with analysis results
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - After analysis completes, call `diagnosticsProvider.publishDiagnostics(insights)`
  - Filter insights with inconsistencies
  - Group by file URI
  - Create Diagnostic objects with proper range, message, severity
  - Test: fullWorkflow.test.ts - "Should show diagnostics in Problems panel"

- [X] **T018** Implement clearFilters command impact on diagnostics
  - File: `src/LoggerUsage.VSCode/src/commands/index.ts`
  - Command: `loggerUsage.clearFilters`
  - Handler: resets UI filter state BUT does NOT clear diagnostics
  - Note: Diagnostics in Problems panel are independent of UI filters
  - Test: fullWorkflow.test.ts - "Should clear diagnostics when clearing filters"
  - (Test may need adjustment - diagnostics shouldn't clear on filter reset)

### T019-T021: Export Insights

- [X] **T019** [P] Implement export service
  - File: `src/LoggerUsage.VSCode/src/services/ExportService.ts`
  - Method: `exportToJson(insights, summary, filePath)` - writes JSON
  - Method: `exportToCsv(insights, filePath)` - writes CSV
  - Method: `exportToMarkdown(insights, summary, filePath)` - writes Markdown
  - Use `fs.promises.writeFile` for async file writes
  - Handle errors gracefully
  - Test: fullWorkflow.test.ts - "Should export insights to JSON file"

- [X] **T020** Implement exportInsights command
  - File: `src/LoggerUsage.VSCode/src/commands/index.ts`
  - Command: `loggerUsage.exportInsights`
  - Handler: shows QuickPick for format (JSON/CSV/Markdown)
  - Shows SaveDialog for destination
  - Calls appropriate ExportService method
  - Shows success notification
  - Test: fullWorkflow.test.ts - "Should export insights to JSON file"

- [X] **T021** Add export button to webview
  - File: `src/LoggerUsage.VSCode/views/insights.html`
  - Add "Export" button to header
  - Click handler sends message to extension
  - Extension invokes `loggerUsage.exportInsights` command
  - Test: fullWorkflow.test.ts - "Should export insights to JSON file"

### T022-T024: Status Bar & Search

- [X] **T022** [P] Implement status bar item
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - Create status bar item: `vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left)`
  - Set text: `$(database) [Solution Name]` or `$(warning) No Solution`
  - Set tooltip: full solution path
  - Set command: `loggerUsage.selectSolution` (if multiple solutions)
  - Show item: `statusBarItem.show()`
  - Update on solution change
  - Test: fullWorkflow.test.ts - "Should update status bar with solution name"

- [X] **T023** Update status bar after analysis
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - After successful analysis, emit event with solution path
  - Extension listens and updates status bar text
  - Include insight count in tooltip: "123 logging calls found"
  - Test: fullWorkflow.test.ts - "Should update status bar with solution name"

- [X] **T024** Implement search in webview
  - File: `src/LoggerUsage.VSCode/views/insights.html` (inline JavaScript)
  - Add search input with debounce (300ms)
  - Function: `searchInsights(query)` - filters by message template, file path, parameter names
  - Update filter state on search input change
  - Re-render table with filtered results
  - Test: fullWorkflow.test.ts - "Should search insights by message template"

---

## Phase 3: Error Handling Tests (errorHandling.test.ts)

Implement robust error handling to make these tests pass. Tests are in `test/LoggerUsage.VSCode.Tests/integration/errorHandling.test.ts`.

### T025-T027: Bridge Process Management

- [X] **T025** [P] Implement bridge crash detection
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - Listen to bridge process `exit` event
  - If exit code != 0 and not intentional shutdown: emit error event
  - Store crash flag to prevent re-spawn loop
  - Test: errorHandling.test.ts - "Should handle bridge process crash gracefully"

- [X] **T026** Implement retry mechanism
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - On bridge crash: show error notification with "Retry" button
  - Retry button: resets crash flag, restarts bridge, retries analysis
  - Max retries: 3 (configurable)
  - Test: errorHandling.test.ts - "Should handle bridge process crash gracefully"
  - Test: errorHandling.test.ts - "Should provide retry option after analysis failure"

- [X] **T027** [P] Implement bridge communication error handling
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - Wrap JSON parsing in try-catch
  - On parse error: log to output channel, continue listening
  - On repeated errors (5+): restart bridge process
  - Test: errorHandling.test.ts - "Should recover from bridge communication errors"

### T028-T031: Solution & Compilation Errors

- [ ] **T028** [P] Implement invalid solution handling in bridge
  - File: `src/LoggerUsage.VSCode.Bridge/WorkspaceAnalyzer.cs`
  - Wrap MSBuildWorkspace.OpenSolutionAsync in try-catch
  - Catch InvalidOperationException, FileNotFoundException
  - Return AnalysisErrorResponse with ErrorCode: "INVALID_SOLUTION"
  - Include helpful message: "The solution file is invalid or corrupted"
  - Test: errorHandling.test.ts - "Should show user-friendly error for invalid solution file"

- [ ] **T029** Handle invalid solution errors in extension
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - Check response.Status == "error" and ErrorCode == "INVALID_SOLUTION"
  - Show user-friendly notification (no stack trace)
  - Suggest checking solution file integrity
  - Test: errorHandling.test.ts - "Should show user-friendly error for invalid solution file"

- [ ] **T030** [P] Implement partial results for compilation errors
  - File: `src/LoggerUsage.VSCode.Bridge/WorkspaceAnalyzer.cs`
  - Extract from projects even if some have compilation errors
  - Log compilation diagnostics to progress stream
  - Include successfully analyzed files in AnalysisSuccessResponse
  - Add warning count to summary
  - Test: errorHandling.test.ts - "Should handle compilation errors and show partial results"

- [ ] **T031** Show compilation error warnings
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - If summary.WarningsCount > 0: show warning notification
  - Message: "Analysis completed with compilation errors. Partial results shown."
  - Button: "View Errors" → opens output channel
  - Test: errorHandling.test.ts - "Should handle compilation errors and show partial results"

### T032-T034: Environment & Dependencies

- [ ] **T032** [P] Implement .NET SDK detection
  - File: `src/LoggerUsage.VSCode/src/utils/dotnetDetector.ts`
  - Function: `checkDotNetSdk()` - runs `dotnet --version`
  - Returns: { installed: boolean, version?: string, error?: string }
  - Timeout: 5 seconds
  - Test: errorHandling.test.ts - "Should handle missing .NET SDK gracefully"

- [ ] **T033** Check .NET SDK before analysis
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - Before starting analysis: call `checkDotNetSdk()`
  - If not installed: show error notification
  - Message: ".NET SDK not found. Please install .NET 10 SDK or later."
  - Button: "Download" → opens .NET download page
  - Test: errorHandling.test.ts - "Should handle missing .NET SDK gracefully"

- [ ] **T034** [P] Implement missing dependencies handling
  - File: `src/LoggerUsage.VSCode.Bridge/WorkspaceAnalyzer.cs`
  - Detect missing NuGet packages from compilation diagnostics
  - If CS0246 errors (missing type): check for NuGet references
  - Return AnalysisErrorResponse with ErrorCode: "MISSING_DEPENDENCIES"
  - Message: "Missing NuGet packages. Run 'dotnet restore'."
  - Test: errorHandling.test.ts - "Should handle missing project dependencies"

### T035-T037: File System & Timeout

- [ ] **T035** [P] Implement file system error handling
  - File: `src/LoggerUsage.VSCode.Bridge/WorkspaceAnalyzer.cs`
  - Wrap file access in try-catch
  - Catch UnauthorizedAccessException, IOException
  - Return AnalysisErrorResponse with ErrorCode: "FILE_SYSTEM_ERROR"
  - Include file path in error details
  - Test: errorHandling.test.ts - "Should handle network/file system errors"

- [ ] **T036** Handle file system errors in extension
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - Check ErrorCode == "FILE_SYSTEM_ERROR"
  - Show notification: "Unable to access file: [path]. Check permissions."
  - Test: errorHandling.test.ts - "Should handle network/file system errors"

- [ ] **T037** [P] Implement analysis timeout (optional)
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - Configuration: `loggerUsage.analysisTimeout` (default: 0 = no timeout)
  - If timeout set: use CancellationTokenSource with timeout
  - On timeout: cancel analysis, show warning notification
  - Message: "Analysis timed out. Showing partial results."
  - Test: errorHandling.test.ts - "Should handle analysis timeout gracefully"

### T038-T039: Concurrency & Logging

- [ ] **T038** [P] Implement concurrent analysis prevention
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - Add `isAnalyzing` flag
  - If `analyze()` called while `isAnalyzing == true`:
    - Option 1: Queue the request (recommended)
    - Option 2: Cancel current analysis and start new one
  - Show notification: "Analysis in progress. Request queued."
  - Test: errorHandling.test.ts - "Should handle concurrent analysis requests"

- [X] **T039** [P] Implement output channel logging
  - File: `src/LoggerUsage.VSCode/src/utils/logger.ts`
  - Create output channel: `vscode.window.createOutputChannel('Logger Usage')`
  - Function: `logInfo(message)`, `logWarning(message)`, `logError(message, error)`
  - Include timestamps
  - Export logger singleton
  - Use throughout extension for error logging
  - Test: errorHandling.test.ts - "Should log errors to output channel for debugging"

---

## Phase 4: Incremental Analysis Tests (incrementalAnalysis.test.ts)

Implement incremental analysis features. Tests are in `test/LoggerUsage.VSCode.Tests/integration/incrementalAnalysis.test.ts`.

### T040-T042: File Watcher & Re-Analysis

- [ ] **T040** [P] Implement file save watcher
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - Register: `vscode.workspace.onDidSaveTextDocument(doc => ...)`
  - Filter: only C# files (*.cs)
  - Check: `loggerUsage.autoAnalyzeOnSave` setting (default: true)
  - If enabled: trigger incremental analysis
  - Test: incrementalAnalysis.test.ts - "Should trigger re-analysis when C# file is saved"

- [ ] **T041** Implement incremental analysis in bridge
  - File: `src/LoggerUsage.VSCode.Bridge/WorkspaceAnalyzer.cs`
  - Method: `AnalyzeFile(string filePath, string solutionPath, CancellationToken)`
  - Load solution (cache if already loaded)
  - Find document in compilation
  - Extract logging from single document only
  - Return insights for that file only
  - Test: incrementalAnalysis.test.ts - "Should trigger re-analysis when C# file is saved"

- [ ] **T042** Add debouncing for rapid saves
  - File: `src/LoggerUsage.VSCode/src/utils/debounce.ts`
  - Function: `debounce<T>(func: Function, delay: number)` - generic debounce utility
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - Wrap incremental analysis call with debounce (500ms)
  - Test: incrementalAnalysis.test.ts - "Should handle rapid consecutive file saves"

### T043-T045: State Management & UI Updates

- [ ] **T043** [P] Implement insights state manager
  - File: `src/LoggerUsage.VSCode/src/state/InsightsState.ts`
  - Class: `InsightsState`
  - Property: `allInsights: Map<string, LoggingInsight[]>` (key = file path)
  - Method: `updateFile(filePath, insights)` - updates insights for one file
  - Method: `removeFile(filePath)` - removes insights for deleted file
  - Method: `getAllInsights()` - returns flattened array
  - Method: `getSummary()` - computes summary stats
  - Export singleton instance
  - Test: incrementalAnalysis.test.ts - "Should preserve insights from other files"

- [ ] **T044** Update UI after incremental analysis
  - File: `src/LoggerUsage.VSCode/src/services/AnalysisService.ts`
  - After file analysis: call `InsightsState.updateFile(filePath, insights)`
  - Emit `analysisComplete` event with updated all insights
  - Refresh tree view: `treeViewProvider.refresh(insightsState.getAllInsights())`
  - Update webview if open: `insightsPanelProvider.updateInsights(...)`
  - Test: incrementalAnalysis.test.ts - "Should update insights panel after file save"
  - Test: incrementalAnalysis.test.ts - "Should update tree view after incremental analysis"

- [ ] **T045** Update diagnostics for modified file
  - File: `src/LoggerUsage.VSCode/src/providers/DiagnosticsProvider.ts`
  - Method: `updateFileDiagnostics(fileUri, insights)` - updates diagnostics for single file
  - Clear previous diagnostics for that file
  - Create new diagnostics from updated insights
  - Test: incrementalAnalysis.test.ts - "Should update diagnostics for modified file"

### T046-T048: Configuration & File Deletion

- [ ] **T046** [P] Add autoAnalyzeOnSave configuration
  - File: `src/LoggerUsage.VSCode/package.json`
  - Configuration: `loggerUsage.autoAnalyzeOnSave` (type: boolean, default: true)
  - Description: "Automatically re-analyze file on save"
  - Test: incrementalAnalysis.test.ts - "Should respect autoAnalyzeOnSave configuration"

- [ ] **T047** Respect autoAnalyzeOnSave setting
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - In file save handler: check configuration value
  - If false: skip incremental analysis
  - Test: incrementalAnalysis.test.ts - "Should respect autoAnalyzeOnSave configuration"

- [ ] **T048** [P] Implement file deletion handling
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - Register: `vscode.workspace.onDidDeleteFiles(event => ...)`
  - For each deleted C# file: call `InsightsState.removeFile(filePath)`
  - Clear diagnostics for deleted file
  - Refresh tree view and webview
  - Test: incrementalAnalysis.test.ts - "Should handle file deletion gracefully"

### T049: Project File Changes

- [ ] **T049** Trigger full re-analysis on .csproj changes
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - In file save handler: check if file is .csproj or .sln
  - If yes: trigger full workspace analysis (not incremental)
  - Show notification: "Project structure changed. Re-analyzing..."
  - Test: incrementalAnalysis.test.ts - "Should re-analyze when .csproj file changes"

---

## Phase 5: Multi-Solution Tests (multiSolution.test.ts)

Implement multi-solution workspace support. Tests are in `test/LoggerUsage.VSCode.Tests/integration/multiSolution.test.ts`.

### T050-T052: Solution Detection & Selection

- [ ] **T050** [P] Implement solution detector
  - File: `src/LoggerUsage.VSCode/src/utils/solutionDetector.ts`
  - Function: `findAllSolutions(workspaceFolders)` - searches for *.sln files
  - Use `vscode.workspace.findFiles('**/*.sln', '**/node_modules/**')`
  - Return array of solution URIs with display names
  - Test: multiSolution.test.ts - "Should detect multiple .sln files in workspace"
  - Test: multiSolution.test.ts - "Should handle solution file in nested directories"

- [ ] **T051** Implement solution state manager
  - File: `src/LoggerUsage.VSCode/src/state/SolutionState.ts`
  - Class: `SolutionState`
  - Property: `allSolutions: SolutionInfo[]`
  - Property: `activeSolution: SolutionInfo | null`
  - Method: `setActiveSolution(solution)` - updates active solution
  - Method: `getActiveSolution()` - returns active solution
  - Event: `onActiveSolutionChanged` - emitted when active solution changes
  - Export singleton instance
  - Test: multiSolution.test.ts - "Should select first solution as active by default"

- [ ] **T052** Initialize solution state on activation
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - Call `findAllSolutions()` on activation
  - If multiple solutions found: set first as active
  - Update status bar with active solution name
  - Test: multiSolution.test.ts - "Should select first solution as active by default"

### T053-T055: Solution Picker & Switching

- [ ] **T053** [P] Implement selectSolution command
  - File: `src/LoggerUsage.VSCode/src/commands/index.ts`
  - Command: `loggerUsage.selectSolution`
  - Handler: shows QuickPick with all solution names
  - QuickPick items: display name, description = relative path
  - On selection: call `SolutionState.setActiveSolution(selected)`
  - Test: multiSolution.test.ts - "Should show solution picker when command executed"

- [ ] **T054** Trigger re-analysis on solution switch
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - Listen to `SolutionState.onActiveSolutionChanged` event
  - On change: clear current insights state
  - Trigger full workspace analysis for new solution
  - Show progress notification: "Switching to [Solution Name]..."
  - Test: multiSolution.test.ts - "Should switch active solution on selection"
  - Test: multiSolution.test.ts - "Should trigger re-analysis when switching solutions"

- [ ] **T055** Update status bar on solution switch
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - On `onActiveSolutionChanged`: update status bar text
  - Format: `$(database) [Solution Name]` (if 1 solution) or `$(database) [Name] (1 of 3)` (if multiple)
  - Tooltip: full path + insight count
  - Test: multiSolution.test.ts - "Should update status bar with solution name"
  - Test: multiSolution.test.ts - "Should show solution count in status bar"

### T056-T058: Solution-Specific Data Isolation

- [ ] **T056** [P] Scope insights to active solution
  - File: `src/LoggerUsage.VSCode/src/state/InsightsState.ts`
  - Add property: `currentSolution: string | null`
  - Method: `setSolution(solutionPath)` - clears insights, sets new solution
  - Ensure `getAllInsights()` returns only insights for current solution
  - Test: multiSolution.test.ts - "Should show insights only for active solution"

- [ ] **T057** Clear diagnostics on solution switch
  - File: `src/LoggerUsage.VSCode/src/providers/DiagnosticsProvider.ts`
  - On solution switch: call `clearDiagnostics()`
  - After new analysis completes: publish diagnostics for new solution
  - Test: multiSolution.test.ts - "Should clear diagnostics when switching solutions"

- [ ] **T058** Update tree view on solution switch
  - File: `src/LoggerUsage.VSCode/src/providers/LoggerTreeViewProvider.ts`
  - On solution switch: tree view automatically refreshes (via `refresh()` call)
  - Root node shows active solution name
  - Children show projects from active solution only
  - Test: multiSolution.test.ts - "Should update tree view when switching solutions"

### T059: Active Solution Detection from Editor

- [ ] **T059** [P] Determine active solution from active editor
  - File: `src/LoggerUsage.VSCode/src/utils/solutionDetector.ts`
  - Function: `findSolutionForFile(filePath, allSolutions)` - finds which solution contains file
  - Check if file path is within solution directory
  - File: `src/LoggerUsage.VSCode/extension.ts`
  - On `vscode.window.onDidChangeActiveTextEditor`: check active file
  - If file belongs to different solution: switch active solution
  - Test: multiSolution.test.ts - "Should determine active solution from active editor file"

---

## Phase 6: Test Completion & Validation

### T060-T062: Enable Skipped Tests

- [ ] **T060** [P] Enable errorHandling tests
  - File: `test/LoggerUsage.VSCode.Tests/integration/errorHandling.test.ts`
  - Remove `suite.skip` → change to `suite`
  - Remove `assert.fail` statements from tests
  - Implement actual test assertions
  - Run test suite and verify all pass

- [ ] **T061** [P] Enable incrementalAnalysis tests
  - File: `test/LoggerUsage.VSCode.Tests/integration/incrementalAnalysis.test.ts`
  - Remove `suite.skip` → change to `suite`
  - Remove `assert.fail` statements from tests
  - Implement actual test assertions
  - Run test suite and verify all pass

- [ ] **T062** [P] Enable multiSolution tests
  - File: `test/LoggerUsage.VSCode.Tests/integration/multiSolution.test.ts`
  - Remove `suite.skip` → change to `suite`
  - Remove `assert.fail` statements from tests
  - Implement actual test assertions
  - Run test suite and verify all pass

### T063-T064: Complete fullWorkflow Tests

- [ ] **T063** [P] Complete fullWorkflow test implementations
  - File: `test/LoggerUsage.VSCode.Tests/integration/fullWorkflow.test.ts`
  - Remove `test.skip` from all tests
  - Implement assertions for each TODO comment
  - Verify all 12 tests pass

- [ ] **T064** Run full integration test suite
  - Run all integration tests: `npm test -- integration/`
  - Verify all 43 tests pass
  - Fix any flaky tests
  - Update test timeout values if needed

---

## Phase 7: Documentation & Polish

- [ ] **T065** [P] Update README with new features
  - File: `src/LoggerUsage.VSCode/README.md`
  - Document error handling capabilities
  - Document incremental analysis feature
  - Document multi-solution support
  - Add screenshots of tree view, webview, problems panel
  - Add troubleshooting section for common errors

- [ ] **T066** [P] Update CHANGELOG
  - File: `src/LoggerUsage.VSCode/CHANGELOG.md`
  - Version 1.1.0 or next version
  - List all new features: error handling, incremental analysis, multi-solution
  - List all bug fixes
  - List all test improvements

- [ ] **T067** [P] Update plan.md with completed features
  - File: `specs/001-a-vscode-extension/plan.md`
  - Mark all applicable features as implemented
  - Update progress tracking section
  - Document any deviations from original plan

- [ ] **T068** Create integration test documentation
  - File: `test/LoggerUsage.VSCode.Tests/integration/README.md`
  - Explain integration test structure
  - Document test fixtures and helpers
  - Provide guidance for adding new integration tests
  - Document CI/CD integration

---

## Dependencies

**Sequential Dependencies**:

- Phase 1 (T001-T003) must complete before all other phases
- T004-T006 must complete before T007-T024 (need event infrastructure)
- T007-T009 must complete in order (tree view)
- T010-T013 must complete in order (webview)
- T016-T018 must complete in order (diagnostics)
- T025-T027 must complete before T028-T039 (bridge management before error handling)
- T040-T042 must complete before T043-T049 (file watching before state management)
- T050-T052 must complete before T053-T059 (detection before selection)
- T060-T064 must complete after all implementation tasks

**Parallel Groups**:

- Phase 2 UI Components [P]: T007, T010, T014, T016, T019, T022 (different providers)
- Phase 3 Error Handling [P]: T025, T028, T032, T035, T038, T039 (different error types)
- Phase 4 State Management [P]: T043, T048 (different concerns)
- Phase 5 Solution Management [P]: T050, T053, T056, T059 (different utilities)
- Phase 6 Test Enablement [P]: T060, T061, T062, T063 (different test files)
- Phase 7 Documentation [P]: T065, T066, T067, T068 (different documents)

---

## Validation Checklist

Before marking complete, verify:

- [ ] All 68 tasks completed
- [ ] All 43 integration tests passing (no skips)
- [ ] No test failures or flaky tests
- [ ] Error scenarios handled gracefully with user-friendly messages
- [ ] Incremental analysis works and preserves other file insights
- [ ] Multi-solution workspaces detected and switching works
- [ ] Status bar updates correctly
- [ ] Tree view, webview, and problems panel all functional
- [ ] Navigation to code locations works
- [ ] Export functionality works for all formats
- [ ] All documentation updated
- [ ] No regressions in existing functionality

---

## Notes

- **Test-Driven**: Implement features to make existing tests pass (tests already written)
- **[P] marker**: Tasks that can run in parallel (different files/components)
- **Commit strategy**: Commit after each completed task or logical group
- **Focus**: Make skipped tests pass by implementing their required features
- **Many features will require BOTH extension and bridge changes**

---

## Estimated Effort

- Phase 1 (Setup): 1 day
- Phase 2 (Full Workflow): 3-4 days
- Phase 3 (Error Handling): 3-4 days
- Phase 4 (Incremental Analysis): 2-3 days
- Phase 5 (Multi-Solution): 2-3 days
- Phase 6 (Test Completion): 1-2 days
- Phase 7 (Documentation): 1 day

**Total**: 13-18 days (2-3.5 weeks) for 1-2 developers working in parallel on [P] tasks

---

**Generated**: 2025-10-08
**Based on**: Integration test files with TODOs and skipped tests
**Alignment**: Constitutional principles maintained, TDD approach, existing extension architecture
