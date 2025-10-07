# Tasks: VS Code Logging Insights Extension

**Branch**: `001-a-vscode-extension` | **Date**: 2025-10-06
**Input**: Design documents from `D:\Repos\Meir017\dotnet-logging-usage\specs\001-a-vscode-extension\`
**Prerequisites**: plan.md, research.md, data-model.md, contracts/, quickstart.md

---

## Task Format

`- [ ] T### [P?] Description in file/path`

- **[P]**: Can run in parallel (different files, no dependencies)
- **No [P]**: Must run sequentially (dependencies or same file)

---

## Phase 3.1: Setup & Scaffolding (Sequential)

- [ ] **T001** Initialize TypeScript extension project structure
  - Create `src/LoggerUsage.VSCode/` directory
  - Create `test/` directory for extension tests
  - Create `views/` subdirectory for webview HTML

- [ ] **T002** Configure package.json for VS Code extension
  - Set name: `logger-usage`
  - Set displayName: "Logger Usage"
  - Set description: "Logging insights for C# projects"
  - Configure engines: `"vscode": "^1.85.0"`
  - Set main entry: `"./out/extension.js"`
  - Add activation events: `workspaceContains:**/*.sln`, `workspaceContains:**/*.csproj`
  - Configure contributes sections (commands, configuration, views, keybindings)
  - Add dependencies: `@types/vscode`, `@types/node`
  - Add devDependencies: `@vscode/test-electron`, `esbuild`, `typescript`
  - Add scripts: `compile`, `watch`, `test`, `package`

- [ ] **T003** Setup TypeScript configuration (tsconfig.json)
  - Target: ES2020
  - Module: commonjs
  - Lib: ES2020
  - OutDir: `./out`
  - SourceMap: true
  - Strict mode enabled
  - Module resolution: node

- [ ] **T004** Initialize C# bridge project (LoggerUsage.VSCode.Bridge)
  - Create `src/LoggerUsage.VSCode.Bridge/` directory
  - Create LoggerUsage.VSCode.Bridge.csproj (.NET 10 console app)
  - Add PackageReferences: LoggerUsage, LoggerUsage.MSBuild, System.Text.Json
  - Configure OutputType: Exe
  - Configure PublishReadyToRun: true for performance
  - Create `Models/` subdirectory for DTOs

- [ ] **T005** Configure build pipeline
  - Setup esbuild configuration for TypeScript (bundle extension.js)
  - Configure dotnet publish for bridge (platform-specific)
  - Create build script to bundle .NET runtime (win-x64, linux-x64, osx-arm64)
  - Configure .vscodeignore to exclude source files from package

- [ ] **T006** Create extension icon and marketplace assets
  - Design 128x128 PNG icon for extension
  - Create README.md with features, usage, configuration
  - Create CHANGELOG.md
  - Add screenshots to assets/ directory

---

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### TypeScript Extension Tests [P] - Can run in parallel

- [ ] **T007** [P] Extension activation tests in `test/extension.test.ts`
  - Test: Extension activates when workspace contains .sln file
  - Test: Extension activates when workspace contains .csproj file
  - Test: Commands are registered on activation
  - Test: Status bar item created
  - Test: Tree view provider initialized
  - Test: Diagnostic collection created
  - Assertions must fail (no implementation yet)

- [ ] **T008** [P] Command execution tests in `test/commands.test.ts`
  - Test: loggerUsage.analyze triggers analysis
  - Test: loggerUsage.showInsightsPanel opens webview
  - Test: loggerUsage.selectSolution shows quick pick
  - Test: loggerUsage.exportInsights prompts for format and location
  - Test: loggerUsage.clearFilters resets filter state
  - Test: loggerUsage.navigateToInsight opens file at location
  - Assertions must fail (no implementation yet)

- [ ] **T009** [P] Analysis service tests in `test/analysisService.test.ts`
  - Test: Spawns bridge process with correct path
  - Test: Sends handshake and receives ready response
  - Test: Sends AnalysisRequest as JSON via stdin
  - Test: Parses AnalysisProgress messages from stdout
  - Test: Parses AnalysisSuccessResponse with insights
  - Test: Handles AnalysisErrorResponse gracefully
  - Test: Cancels analysis and terminates bridge process
  - Test: Closes bridge on extension deactivation
  - Assertions must fail (no implementation yet)

- [ ] **T010** [P] Insights panel tests in `test/insightsPanel.test.ts`
  - Test: Creates webview panel with correct configuration
  - Test: Sends updateInsights message to webview
  - Test: Receives applyFilters message from webview
  - Test: Receives navigateToInsight message and opens file
  - Test: Receives exportResults message and prompts for save
  - Test: Updates theme when VS Code theme changes
  - Test: Disposes webview properly
  - Assertions must fail (no implementation yet)

- [ ] **T011** [P] Problems provider tests in `test/problemsProvider.test.ts`
  - Test: Creates diagnostic collection with name 'loggerUsage'
  - Test: Publishes diagnostics for parameter inconsistencies
  - Test: Publishes diagnostics for missing EventIds
  - Test: Publishes diagnostics for sensitive data warnings
  - Test: Clears diagnostics for file on re-analysis
  - Test: Clears all diagnostics on configuration change
  - Test: Groups diagnostics by file URI
  - Assertions must fail (no implementation yet)

- [ ] **T012** [P] Tree view provider tests in `test/treeViewProvider.test.ts`
  - Test: Generates tree structure (solution → project → file → insight)
  - Test: Insight count displayed on file nodes
  - Test: Clicking insight node opens file at location
  - Test: Refresh updates tree data
  - Test: Empty state when no analysis results
  - Assertions must fail (no implementation yet)

### C# Bridge Tests [P] - Can run in parallel

- [ ] **T013** [P] Bridge program tests in `test/LoggerUsage.VSCode.Bridge.Tests/BridgeProgramTests.cs`
  - Test: Reads JSON commands from stdin
  - Test: Responds to ping command with ready status
  - Test: Routes analyze command to WorkspaceAnalyzer
  - Test: Routes analyzeFile command to WorkspaceAnalyzer
  - Test: Handles invalid JSON gracefully (error response)
  - Test: Handles unknown commands (error response)
  - Test: Terminates on shutdown command
  - Assertions must fail (no implementation yet)

- [ ] **T014** [P] Workspace analyzer tests in `test/LoggerUsage.VSCode.Bridge.Tests/WorkspaceAnalyzerTests.cs`
  - Test: Loads solution using MSBuildWorkspace
  - Test: Invokes LoggerUsageExtractor for all projects
  - Test: Sends progress updates during analysis
  - Test: Maps LoggerUsageInfo to LoggingInsightDto
  - Test: Handles solution load failures (error response)
  - Test: Handles compilation errors (partial results)
  - Test: Supports cancellation
  - Assertions must fail (no implementation yet)

- [ ] **T015** [P] Logger usage mapper tests in `test/LoggerUsage.VSCode.Bridge.Tests/LoggerUsageMapperTests.cs`
  - Test: Maps LoggerUsageInfo to LoggingInsightDto correctly
  - Test: Detects parameter name inconsistencies
  - Test: Detects missing EventIds
  - Test: Detects sensitive data classifications
  - Test: Generates unique insight IDs (filePath:line:column)
  - Test: Handles null/missing fields gracefully
  - Assertions must fail (no implementation yet)

### Integration Tests [P] - Can run in parallel (E2E scenarios)

- [ ] **T016** [P] Full workflow integration test in `test/integration/fullWorkflow.test.ts`
  - Test: Open workspace with .sln → extension activates
  - Test: Run analysis → insights displayed in panel
  - Test: Apply filters → table updates
  - Test: Click insight → editor navigates to location
  - Test: Problems panel shows diagnostics
  - Requires sample test project with logging code
  - Assertions must fail (no implementation yet)

- [ ] **T017** [P] Incremental analysis test in `test/integration/incrementalAnalysis.test.ts`
  - Test: Save C# file → automatic re-analysis triggers
  - Test: Insights panel updates with changes
  - Test: Diagnostics update for modified file
  - Test: Other files' insights unchanged
  - Assertions must fail (no implementation yet)

- [ ] **T018** [P] Error handling test in `test/integration/errorHandling.test.ts`
  - Test: Bridge crash → error notification, retry available
  - Test: Invalid solution file → error notification
  - Test: Compilation errors → partial results shown
  - Test: Network/file system errors handled gracefully
  - Assertions must fail (no implementation yet)

- [ ] **T019** [P] Multi-solution test in `test/integration/multiSolution.test.ts`
  - Test: Workspace with multiple .sln files → first selected
  - Test: Switch active solution → insights refresh
  - Test: Status bar shows active solution name
  - Assertions must fail (no implementation yet)

---

## Phase 3.3: Core Implementation (ONLY after tests are failing)

### TypeScript Data Models [P] - Can run in parallel (no dependencies)

- [ ] **T020** [P] Implement TypeScript models in `src/LoggerUsage.VSCode/models/insightViewModel.ts`
  - Interface: LoggingInsight with all properties per data-model.md
  - Interface: DataClassification
  - Interface: ParameterInconsistency
  - Interface: EventIdInfo
  - Interface: Location
  - Export all interfaces

- [ ] **T021** [P] Implement filter state model in `src/LoggerUsage.VSCode/models/filterState.ts`
  - Interface: FilterState with filter properties
  - Default filter values
  - Filter validation helpers
  - Export interface

- [ ] **T022** [P] Implement IPC message types in `src/LoggerUsage.VSCode/models/ipcMessages.ts`
  - Type: AnalysisRequest
  - Type: IncrementalAnalysisRequest
  - Type: AnalysisProgress
  - Type: AnalysisSuccessResponse
  - Type: AnalysisErrorResponse
  - Type: AnalysisSummary
  - Union type: AnalysisResponse
  - Export all types

- [ ] **T023** [P] Implement webview message types in `src/LoggerUsage.VSCode/models/webviewMessages.ts`
  - Type: ExtensionToWebviewMessage (union)
  - Type: WebviewToExtensionMessage (union)
  - Export all types

### C# Bridge DTOs [P] - Can run in parallel (no dependencies)

- [ ] **T024** [P] Implement C# request DTOs in `src/LoggerUsage.VSCode.Bridge/Models/Requests.cs`
  - Record: AnalysisRequest(string Command, string WorkspacePath, string? SolutionPath, string[]? ExcludePatterns)
  - Record: IncrementalAnalysisRequest(string Command, string FilePath, string SolutionPath)
  - Record: PingRequest(string Command)
  - Record: ShutdownRequest(string Command)

- [ ] **T025** [P] Implement C# response DTOs in `src/LoggerUsage.VSCode.Bridge/Models/Responses.cs`
  - Record: AnalysisSuccessResponse(string Status, AnalysisResult Result)
  - Record: AnalysisErrorResponse(string Status, string Message, string Details, string? ErrorCode)
  - Record: AnalysisProgress(string Status, int Percentage, string Message, string? CurrentFile)
  - Record: ReadyResponse(string Status, string Version)

- [ ] **T026** [P] Implement C# insight DTOs in `src/LoggerUsage.VSCode.Bridge/Models/Dtos.cs`
  - Record: LoggingInsightDto with all properties per data-model.md
  - Record: EventIdDto(int? Id, string? Name)
  - Record: LocationDto(string FilePath, int StartLine, int StartColumn, int EndLine, int EndColumn)
  - Record: DataClassificationDto(string ParameterName, string ClassificationType)
  - Record: ParameterInconsistencyDto(string Type, string Message, string Severity, LocationDto? Location)
  - Record: AnalysisSummary(int TotalInsights, Dictionary<string, int> ByMethodType, Dictionary<string, int> ByLogLevel, int InconsistenciesCount, int FilesAnalyzed, long AnalysisTimeMs)
  - Record: AnalysisResult(List<LoggingInsightDto> Insights, AnalysisSummary Summary)

### C# Bridge Foundation (Sequential - depends on DTOs)

- [ ] **T027** Implement bridge program entry point in `src/LoggerUsage.VSCode.Bridge/Program.cs`
  - Main method with stdio loop
  - Read JSON from Console.In
  - Deserialize to request objects
  - Route commands: ping, analyze, analyzeFile, shutdown
  - Serialize responses to Console.Out
  - Error handling (catch exceptions, send error response)
  - Graceful termination on shutdown
  - Test: T013 should now pass

- [ ] **T028** Implement workspace analyzer in `src/LoggerUsage.VSCode.Bridge/WorkspaceAnalyzer.cs`
  - Constructor: Inject IWorkspaceFactory, ILoggerUsageExtractor
  - Method: AnalyzeWorkspace(AnalysisRequest, CancellationToken)
  - Load solution via MSBuildWorkspace
  - Enumerate projects
  - Call LoggerUsageExtractor.ExtractFromWorkspace
  - Send progress updates to Console.Out
  - Map results via LoggerUsageMapper
  - Return AnalysisSuccessResponse or AnalysisErrorResponse
  - Test: T014 should now pass

- [ ] **T029** Implement logger usage mapper in `src/LoggerUsage.VSCode.Bridge/LoggerUsageMapper.cs`
  - Static method: ToDto(LoggerUsageInfo, string filePath) → LoggingInsightDto
  - Generate unique ID: $"{filePath}:{startLine}:{startColumn}"
  - Map all properties from LoggerUsageInfo to LoggingInsightDto
  - Detect parameter inconsistencies (compare template placeholders vs parameter names)
  - Detect missing EventIds (flag if null)
  - Detect sensitive data (check DataClassifications)
  - Populate Inconsistencies list if any found
  - Test: T015 should now pass

### TypeScript Extension Configuration

- [ ] **T030** Implement configuration service in `src/LoggerUsage.VSCode/configuration.ts`
  - Function: loadConfiguration() → ExtensionConfiguration
  - Read VS Code workspace configuration: `vscode.workspace.getConfiguration('loggerUsage')`
  - Map settings to ExtensionConfiguration interface
  - Function: watchConfiguration(callback) → register change listener
  - Export functions

### TypeScript Extension Analysis Service (Sequential - depends on models)

- [ ] **T031** Implement analysis service in `src/LoggerUsage.VSCode/analysisService.ts`
  - Class: AnalysisService
  - Constructor: Initialize bridge process path detection
  - Method: startBridge() - spawn child process, handshake
  - Method: analyze(request: AnalysisRequest, progress, token) → Promise<AnalysisResponse>
  - Method: analyzeFile(request: IncrementalAnalysisRequest, token) → Promise<AnalysisResponse>
  - Method: cancel() - send cancel command, terminate process
  - Method: dispose() - send shutdown, close stdio streams
  - Handle stdout JSON parsing (progress updates, responses)
  - Handle stderr logging (debug output)
  - Handle process exit (unexpected termination)
  - Test: T009 should now pass

### TypeScript Extension UI Components [P] - Can run in parallel after analysis service

- [ ] **T032** [P] Implement commands in `src/LoggerUsage.VSCode/commands.ts`
  - Function: registerCommands(context, services)
  - Command handler: analyzeWorkspace - show progress, call analysis service, update UI
  - Command handler: showInsightsPanel - create/reveal webview
  - Command handler: selectSolution - show quick pick, update active solution
  - Command handler: exportInsights - show format picker, save file dialog, generate report
  - Command handler: clearFilters - reset filter state, update webview
  - Command handler: navigateToInsight - parse ID, open document, reveal range
  - Command handler: refreshTreeView - trigger tree data provider refresh
  - Test: T008 should now pass

- [ ] **T033** [P] Implement insights panel in `src/LoggerUsage.VSCode/insightsPanel.ts`
  - Class: InsightsPanelProvider
  - Method: createOrShow(insights, summary) - create webview panel or reveal existing
  - Method: generateHtml() - load views/insightsView.html, inject nonce, CSP
  - Method: handleWebviewMessage(message) - route applyFilters, navigateToInsight, exportResults, refreshAnalysis
  - Method: sendMessage(message: ExtensionToWebviewMessage) - postMessage to webview
  - Method: updateInsights(insights, summary) - send updateInsights message
  - Method: updateFilters(filters) - send updateFilters message
  - Method: updateTheme(theme) - detect current theme, send updateTheme message
  - Listen to theme changes: vscode.window.onDidChangeActiveColorTheme
  - Test: T010 should now pass

- [ ] **T034** [P] Implement problems provider in `src/LoggerUsage.VSCode/problemsProvider.ts`
  - Class: ProblemsProvider
  - Constructor: Create diagnostic collection: vscode.languages.createDiagnosticCollection('loggerUsage')
  - Method: publishDiagnostics(insights) - filter inconsistencies, group by file, create diagnostics
  - Method: createDiagnostic(inconsistency) → vscode.Diagnostic - map severity, range, message, code
  - Method: clearDiagnostics() - diagnosticCollection.clear()
  - Method: clearFileD iagnostics(uri) - diagnosticCollection.delete(uri)
  - Method: dispose() - clear and dispose collection
  - Test: T011 should now pass

- [ ] **T035** [P] Implement tree view provider in `src/LoggerUsage.VSCode/treeViewProvider.ts`
  - Class: LoggerTreeViewProvider implements vscode.TreeDataProvider
  - Property: onDidChangeTreeData event emitter
  - Method: getTreeItem(element) → vscode.TreeItem
  - Method: getChildren(element?) → Thenable<TreeItem[]>
  - Build hierarchy: solution → projects → files → insights
  - File nodes show insight count in description
  - Insight nodes show line + message preview
  - Clicking insight triggers navigateToInsight command
  - Method: refresh() - fire onDidChangeTreeData
  - Test: T012 should now pass

### TypeScript Extension Activation (Sequential - orchestrates all components)

- [ ] **T036** Implement extension entry point in `src/LoggerUsage.VSCode/extension.ts`
  - Export function: activate(context: vscode.ExtensionContext)
  - Initialize configuration service
  - Initialize analysis service
  - Register commands via commands.ts
  - Initialize insights panel provider
  - Initialize problems provider
  - Initialize tree view provider: vscode.window.registerTreeDataProvider
  - Create status bar item with solution name
  - Scan workspace for .sln files
  - If solution found and autoAnalyzeOnSave: trigger initial analysis
  - Register file save watcher: workspace.onDidSaveTextDocument
  - Register workspace folder change watcher
  - Export function: deactivate() - dispose all services
  - Test: T007 should now pass

---

## Phase 3.4: Integration (Webview UI + E2E Tests)

- [ ] **T037** Implement webview HTML in `src/LoggerUsage.VSCode/views/insightsView.html`
  - HTML structure: header, filter section, insights table
  - Header: title, summary stats, refresh button, export dropdown
  - Filters: search input, log level checkboxes, method type dropdown, inconsistencies toggle
  - Table: columns (Method Type, Message Template, Log Level, Event ID, Location, Actions)
  - Empty state placeholder
  - Error banner (hidden by default)
  - Inline CSS with VS Code theme variables (--vscode-editor-background, etc.)
  - Inline JavaScript: message handler, filter logic, table rendering
  - Search debouncing (300ms)
  - Row click handler (navigate to insight)
  - Export button handler
  - Refresh button handler
  - CSP-compliant (no external resources)

- [ ] **T038** Implement inline JavaScript in webview HTML for client-side filtering
  - Variable: currentInsights array
  - Variable: currentFilters object
  - Function: applyFilters() - filter currentInsights by log level, method type, search query, inconsistencies
  - Function: renderTable(filteredInsights) - update table rows
  - Function: debounce(func, delay) - debounce search input
  - Function: sendMessage(message) - vscode.postMessage
  - Event listener: window.addEventListener('message', handleMessage)
  - Function: handleMessage(event) - route updateInsights, updateFilters, showError, updateTheme

- [ ] **T039** Run integration tests to verify E2E workflows
  - Execute test suite: T016 (full workflow) - should now pass
  - Execute test suite: T017 (incremental analysis) - should now pass
  - Execute test suite: T018 (error handling) - should now pass
  - Execute test suite: T019 (multi-solution) - should now pass
  - Fix any failing tests before proceeding

---

## Phase 3.5: Polish & Packaging

- [ ] **T040** [P] Unit tests for TypeScript utilities
  - Test configuration parsing edge cases
  - Test filter validation logic
  - Test ID parsing in navigateToInsight
  - Test debounce function behavior

- [ ] **T041** [P] Unit tests for C# mapper edge cases
  - Test null/missing fields in LoggerUsageInfo
  - Test inconsistency detection logic variations
  - Test ID generation uniqueness

- [ ] **T042** Performance validation
  - Test small solution (< 100 files): < 5 seconds analysis
  - Test medium solution (100-500 files): < 30 seconds analysis
  - Test large solution (500+ files): progress updates, best-effort
  - Test incremental file analysis: < 2 seconds
  - Verify memory usage < 1 GB for typical solutions

- [ ] **T043** [P] Update documentation in README.md
  - Features section with screenshots
  - Installation instructions
  - Configuration reference (all settings explained)
  - Usage guide (how to run analysis, navigate insights, filter)
  - Troubleshooting section
  - Known limitations

- [ ] **T044** [P] Update CHANGELOG.md
  - Version 1.0.0 initial release
  - List all features implemented
  - List all functional requirements satisfied

- [ ] **T045** Configure platform-specific packaging
  - Update package.json: add platform-specific vsix builds
  - Configure .NET runtime bundling for win-x64, linux-x64, osx-arm64
  - Test .vsix installation on each platform
  - Verify bundled runtime works without user-installed .NET

- [ ] **T046** Run manual testing scenarios from quickstart.md
  - Execute all 15 test scenarios
  - Verify all edge cases handled
  - Document any issues found
  - Fix critical bugs before release

- [ ] **T047** Final code review and cleanup
  - Remove console.log statements (use logger instead)
  - Remove commented-out code
  - Verify all TODOs resolved
  - Run linter and fix warnings
  - Verify no unused imports

- [ ] **T048** Prepare marketplace release
  - Verify package.json metadata complete
  - Verify icon and screenshots included
  - Test extension in clean VS Code instance
  - Create GitHub release tag
  - Publish to VS Code Marketplace

---

## Dependencies

**Sequential Dependencies (must run in order)**:

- T001-T006 (Setup) must complete before all other tasks
- T007-T019 (Tests) must complete before T020-T038 (Implementation)
- T020-T023 (TS Models) before T031 (Analysis Service)
- T024-T026 (C# DTOs) before T027 (Bridge Program)
- T027 (Bridge Program) before T028 (Workspace Analyzer)
- T028 (Workspace Analyzer) before T029 (Mapper)
- T031 (Analysis Service) before T032-T035 (UI Components)
- T032-T035 (UI Components) before T036 (Extension Activation)
- T036 (Extension Activation) before T037-T038 (Webview)
- T037-T038 (Webview) before T039 (Integration Tests)
- T039 (Integration Tests pass) before T040-T048 (Polish)

**Parallel Groups**:

- Tests [P]: T007, T008, T009, T010, T011, T012 (all TypeScript tests)
- Tests [P]: T013, T014, T015 (all C# bridge tests)
- Integration Tests [P]: T016, T017, T018, T019
- TS Models [P]: T020, T021, T022, T023
- C# DTOs [P]: T024, T025, T026
- UI Components [P]: T032, T033, T034, T035 (after T031 complete)
- Polish [P]: T040, T041, T043, T044

---

## Parallel Execution Example

**Phase 3.2 Tests** - Launch all TypeScript tests together:
```bash
# Terminal 1
npm test -- test/extension.test.ts

# Terminal 2
npm test -- test/commands.test.ts

# Terminal 3
npm test -- test/analysisService.test.ts

# Terminal 4
npm test -- test/insightsPanel.test.ts

# Terminal 5
npm test -- test/problemsProvider.test.ts

# Terminal 6
npm test -- test/treeViewProvider.test.ts
```

**Phase 3.2 C# Tests** - Launch C# tests in parallel:
```bash
# Terminal 1
dotnet test --filter "BridgeProgramTests"

# Terminal 2
dotnet test --filter "WorkspaceAnalyzerTests"

# Terminal 3
dotnet test --filter "LoggerUsageMapperTests"
```

**Phase 3.3 Models** - Implement all models simultaneously:
- Developer 1: T020 (insightViewModel.ts)
- Developer 2: T021 (filterState.ts)
- Developer 3: T022 (ipcMessages.ts)
- Developer 4: T023 (webviewMessages.ts)
- Developer 5: T024 (Requests.cs)
- Developer 6: T025 (Responses.cs)
- Developer 7: T026 (Dtos.cs)

---

## Validation Checklist

Before marking Phase 3 complete, verify:

- [ ] All 48 tasks completed
- [ ] All tests passing (extension tests, bridge tests, integration tests)
- [ ] No compiler errors or warnings
- [ ] No linter warnings
- [ ] Extension activates without errors
- [ ] Analysis completes successfully on sample project
- [ ] Insights panel displays and filters work
- [ ] Problems panel shows diagnostics
- [ ] Tree view navigates correctly
- [ ] Configuration changes take effect
- [ ] Error scenarios handled gracefully
- [ ] Performance targets met (per T042)
- [ ] Manual testing scenarios pass (per T046)
- [ ] All constitutional principles validated:
  - Principle 1: Symbol fidelity (bridge delegates to LoggerUsage)
  - Principle 2: Test-first (all tests written before implementation)
  - Principle 3: Thread-safe (async/await, cancellation tokens)
  - Principle 4: Graceful degradation (error handling throughout)
  - Principle 5: UX consistency (VS Code theme integration, standard patterns)
  - Principle 6: Performance (incremental analysis, progress reporting)

---

## Notes

- **[P] marker**: Tasks with this marker can be executed in parallel because they work on different files and have no dependencies on each other
- **Test-first**: All test tasks (T007-T019) MUST be completed and failing before any implementation tasks (T020-T038)
- **Commit strategy**: Commit after completing each task or logical group of [P] tasks
- **Avoid**: Vague task descriptions, tasks affecting the same files marked [P], skipping tests

---

## Task Generation Metadata

**Generated from**:
- plan.md: Architecture, tech stack, project structure
- data-model.md: TypeScript interfaces, C# records, data structures
- contracts/: extension-activation, analysis-service, insights-panel, problems-provider, commands
- research.md: Architectural decisions, IPC protocol, performance targets
- quickstart.md: Manual test scenarios for validation

**Constitutional Alignment**: All tasks satisfy Constitution v2.0.0 principles

**Total Tasks**: 48 (Setup: 6, Tests: 13, Implementation: 20, Integration: 3, Polish: 6)

**Estimated Effort**: 3-4 weeks for 2 developers working in parallel on [P] tasks

---

**Ready for execution via `/implement` command or manual development**
