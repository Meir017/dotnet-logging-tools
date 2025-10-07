
# Implementation Plan: VS Code Logging Insights Extension

**Branch**: `001-a-vscode-extension` | **Date**: 2025-10-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `D:\Repos\Meir017\dotnet-logging-usage\specs\001-a-vscode-extension\spec.md`

## Execution Flow (/plan command scope)

```
1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from file system structure or context (web=frontend+backend, mobile=app+api)
   → Set Structure Decision based on project type
3. Fill the Constitution Check section based on the content of the constitution document.
4. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking
   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check
5. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file (e.g., `CLAUDE.md` for Claude Code, `.github/copilot-instructions.md` for GitHub Copilot, `GEMINI.md` for Gemini CLI, `QWEN.md` for Qwen Code, or `AGENTS.md` for all other agents).
7. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check
8. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
9. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:

- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary

Create a Visual Studio Code extension that provides real-time logging insights for C# projects using the existing LoggerUsage analysis engine. The extension will automatically analyze Microsoft.Extensions.Logging patterns when a C# project/solution is opened, displaying insights in a dedicated panel with navigation, filtering, and Problems panel integration. Key features include: incremental analysis on file changes, comprehensive filtering (log level, file path, parameter name) with text search, configuration options for auto-analyze behavior and exclusions, and integration with VS Code's Problems panel to surface parameter inconsistencies. The extension leverages the existing LoggerUsage library and MSBuild integration to provide best-effort analysis performance with continuous progress feedback, focusing on the currently active solution when multiple solutions are present in a workspace.

## Technical Context

**Language/Version**: TypeScript 5.x (VS Code Extension API), C# / .NET 10 (existing LoggerUsage library)
**Primary Dependencies**: VS Code Extension API (@types/vscode), existing LoggerUsage library, LoggerUsage.MSBuild for workspace loading, Microsoft.CodeAnalysis (Roslyn) via LoggerUsage
**Storage**: In-memory caching of analysis results, VS Code workspace state API for user settings
**Testing**: VS Code Extension Test Runner (Mocha), xUnit for .NET library components
**Target Platform**: VS Code 1.85+ on Windows/macOS/Linux
**Project Type**: VS Code Extension (TypeScript) consuming .NET library (single project structure with extension host)
**Performance Goals**: Best-effort analysis with continuous progress feedback (no hard time limits per clarifications), incremental updates on file changes
**Constraints**: Must not block VS Code UI, support cancellation, handle incomplete compilations gracefully, analyze only active solution in multi-solution workspaces
**Scale/Scope**: Support typical C# projects (10-50K LOC), dozens of logging calls, hundreds of files per project
**User Input Context**: vscode extension

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Code Quality Gates

- [x] **Symbol Fidelity**: No string-based type/method comparisons (Constitution Principle 1)
  - Rationale: Extension delegates all Roslyn analysis to existing LoggerUsage library which already implements symbol-based comparison via `LoggingTypes` class. Extension code in TypeScript handles only UI/workflow orchestration, not Roslyn symbol resolution.
- [x] **Thread Safety**: Analyzers are stateless, use thread-safe collections (Constitution Principle 3)
  - Rationale: Extension reuses existing thread-safe LoggerUsage analyzers. Extension's TypeScript layer uses async/await patterns and VS Code's cancellation tokens for thread-safe operations. No new analyzers being created.
- [x] **Error Handling**: No unhandled exceptions, graceful degradation implemented (Constitution Principle 4)
  - Rationale: Extension will catch analysis errors, display user-friendly messages, and provide partial results. Matches existing LoggerUsage error handling patterns. Missing dependencies and compilation errors handled per FR-018, FR-019, FR-020.
- [x] **Performance**: Analysis meets latency/memory contracts (Constitution Principle 6)
  - Rationale: Uses best-effort performance model (per clarifications) with continuous progress feedback (FR-014, FR-016). Incremental analysis (FR-005) minimizes re-analysis overhead. Background execution (FR-015) prevents UI blocking.

### Testing Gates

- [x] **Test-First**: Tests exist before implementation and initially failed (Constitution Principle 2)
  - Rationale: Will follow TDD for extension activation, command handling, UI rendering, and integration with LoggerUsage library. Extension tests will use VS Code Extension Test Runner. Existing LoggerUsage library tests already validate analysis correctness.
- [x] **Test Coverage**: Basic, edge, error, and thread safety cases covered (Constitution Principles 2, 3)
  - Rationale: Test plan includes: activation scenarios, workspace detection, progress feedback, cancellation, error states (no logging calls, missing dependencies, compilation errors), filtering/search functionality, Problems panel integration, and multi-solution workspace behavior.
- [x] **Performance Tests**: Benchmark tests verify contracts (Constitution Principle 6)
  - Rationale: Will measure analysis time for typical projects (10-50K LOC) and memory usage. Performance acceptance criteria: best-effort with visible progress, no hard limits (per clarifications). Existing LoggerUsage performance characteristics apply.

### User Experience Gates

- [x] **Output Consistency**: All formats (HTML/JSON/Markdown) present equivalent data (Constitution Principle 5)
  - Rationale: Extension displays insights in VS Code panel only (no export per clarifications). UI presents same data as LoggerUsage library extracts: summary statistics, log usages, inconsistencies, telemetry features. Problems panel integration provides additional view of same inconsistency data.
- [x] **Accessibility**: HTML reports support dark mode and semantic markup (Constitution Principle 5)
  - Rationale: Extension UI integrates with VS Code theme system (FR-025) supporting light/dark modes automatically via VS Code's webview API. No custom HTML reports generated (export out of scope per clarifications).
- [x] **Schema Versioning**: JSON schema version updated if models changed (Constitution Principle 5)
  - Rationale: Extension consumes existing LoggerUsage models (LoggerUsageExtractionResult, LoggerUsageInfo, etc.) without modification. If future models change, existing LoggerUsage schema versioning applies. Extension persists no data requiring independent versioning.

### Documentation Gates

- [x] **XML Documentation**: Public APIs have complete XML docs
  - Rationale: TypeScript extension will use JSDoc for public APIs. Existing LoggerUsage library already has XML documentation. Extension README will document activation, commands, configuration options, and troubleshooting.
- [x] **Change Documentation**: Breaking changes documented in release notes
  - Rationale: Extension is new (no breaking changes initially). Future changes will follow repository's release notes pattern. Configuration schema changes will be documented.
- [x] **Example Updates**: README and quickstart guides reflect new functionality
  - Rationale: Will create extension-specific README with screenshots, configuration examples, and usage scenarios. Quickstart.md (Phase 1) will provide manual testing guide. Main repository README will link to extension as new package.

**Constitution Check Result**: ✅ PASS - All gates satisfied. Extension architecture aligns with constitutional principles by reusing existing compliant LoggerUsage components and following VS Code extension best practices.

## Project Structure

### Documentation (this feature)

```
specs/[###-feature]/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)

```
src/LoggerUsage.VSCode/
├── extension.ts          # Extension entry point, activation
├── analysisService.ts    # Bridge to .NET LoggerUsage library
├── insightsPanel.ts      # Webview panel for displaying insights
├── problemsProvider.ts   # Diagnostic provider for Problems panel
├── commands.ts           # Command registration and handlers
├── configuration.ts      # Settings management
├── models/
│   ├── insightViewModel.ts   # UI-specific view models
│   └── filterState.ts        # Filter and search state
└── views/
    └── insightsView.html     # HTML template for webview

src/LoggerUsage.VSCode.Bridge/    # C# .NET library bridge
├── AnalysisBridge.cs             # Entry point for extension to call
├── WorkspaceAnalyzer.cs          # Workspace detection and analysis orchestration
└── LoggerUsage.VSCode.Bridge.csproj

test/
├── extension.test.ts      # Extension activation, commands
├── analysisService.test.ts  # Analysis integration tests
├── insightsPanel.test.ts    # UI rendering tests
└── integration/
    └── fullWorkflow.test.ts  # End-to-end scenarios
```

**Structure Decision**: Single project with TypeScript extension + C# bridge library. The extension consumes existing LoggerUsage/LoggerUsage.MSBuild packages via the bridge. This hybrid structure leverages VS Code Extension API (TypeScript) while reusing .NET analysis components (C#).

## Phase 0: Outline & Research

1. **Extract unknowns from Technical Context** above:
   - For each NEEDS CLARIFICATION → research task
   - For each dependency → best practices task
   - For each integration → patterns task

2. **Generate and dispatch research agents**:

   ```
   For each unknown in Technical Context:
     Task: "Research {unknown} for {feature context}"
   For each technology choice:
     Task: "Find best practices for {tech} in {domain}"
   ```

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all NEEDS CLARIFICATION resolved

## Phase 1: Design & Contracts

*Prerequisites: research.md complete*

1. **Extract entities from feature spec** → `data-model.md`:
   - Entity name, fields, relationships
   - Validation rules from requirements
   - State transitions if applicable

2. **Generate API contracts** from functional requirements:
   - For each user action → endpoint
   - Use standard REST/GraphQL patterns
   - Output OpenAPI/GraphQL schema to `/contracts/`

3. **Generate contract tests** from contracts:
   - One test file per endpoint
   - Assert request/response schemas
   - Tests must fail (no implementation yet)

4. **Extract test scenarios** from user stories:
   - Each story → integration test scenario
   - Quickstart test = story validation steps

5. **Update agent file incrementally** (O(1) operation):
   - Run `.specify/scripts/powershell/update-agent-context.ps1 -AgentType copilot`
     **IMPORTANT**: Execute it exactly as specified above. Do not add or remove any arguments.
   - If exists: Add only NEW tech from current plan
   - Preserve manual additions between markers
   - Update recent changes (keep last 3)
   - Keep under 150 lines for token efficiency
   - Output to repository root

**Output**: data-model.md, /contracts/*, failing tests, quickstart.md, agent-specific file

## Phase 2: Task Planning Approach

*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:

The /tasks command will generate implementation tasks based on the design artifacts created in Phase 1. Task breakdown follows Test-Driven Development (TDD) principles per Principle 2 (Test-First Development).

### Task Categories

1. **Setup & Scaffolding** (Sequential):
   - Initialize TypeScript extension project structure
   - Configure package.json with VS Code extension metadata
   - Setup tsconfig.json with proper compilation settings
   - Initialize C# bridge project (LoggerUsage.VSCode.Bridge)
   - Configure build pipeline (esbuild for TypeScript, dotnet build for C#)
   - Package bundled .NET runtime for distribution

2. **TypeScript Extension Tests** (Before Implementation) [P]:
   - `extension.test.ts`: Extension activation, command registration
   - `commands.test.ts`: Command execution for analyze, showInsightsPanel, selectSolution, exportInsights
   - `analysisService.test.ts`: Bridge process spawn, IPC communication, JSON deserialization
   - `insightsPanel.test.ts`: Webview creation, message passing, filter application
   - `problemsProvider.test.ts`: Diagnostic collection creation, diagnostic publishing
   - `treeViewProvider.test.ts`: Tree data generation, hierarchical grouping

3. **TypeScript Extension Implementation** (After Tests):
   - `extension.ts`: Implement activate(), deactivate(), command registration
   - `commands.ts`: Implement all command handlers (analyze, showInsightsPanel, etc.)
   - `analysisService.ts`: Implement bridge process management, IPC protocol
   - `insightsPanel.ts`: Implement webview panel, HTML generation, message handlers
   - `problemsProvider.ts`: Implement diagnostic creation, publishing, clearing
   - `treeViewProvider.ts`: Implement tree data provider, refresh logic
   - `configuration.ts`: Implement settings loading, change watching
   - `models/*.ts`: Implement TypeScript data models (InsightViewModel, FilterState, etc.)

4. **C# Bridge Tests** (Before Implementation) [P]:
   - `BridgeProgramTests.cs`: Stdio communication, JSON deserialization, command routing
   - `WorkspaceAnalyzerTests.cs`: Solution detection, MSBuildWorkspace integration
   - `LoggerUsageMapperTests.cs`: Mapping from LoggerUsageInfo to LoggingInsightDto

5. **C# Bridge Implementation** (After Tests):
   - `Program.cs`: Main entry point, stdio loop, command dispatcher
   - `WorkspaceAnalyzer.cs`: Solution loading, project enumeration, LoggerUsageExtractor invocation
   - `LoggerUsageMapper.cs`: DTO mapping, inconsistency detection
   - `Models/*.cs`: C# DTOs (AnalysisRequest, AnalysisSuccessResponse, LoggingInsightDto, etc.)

6. **Webview UI** (Implementation + Manual Testing):
   - `views/insightsView.html`: HTML structure for insights table
   - Inline CSS with VS Code theme variables
   - Inline JavaScript for filter logic, message handling
   - Implement sorting, search debouncing, virtual scrolling (if needed)

7. **Integration Tests** (E2E Scenarios):
   - `fullWorkflow.test.ts`: Open workspace → analyze → view insights → filter → navigate
   - `incrementalAnalysis.test.ts`: Save file → re-analyze → verify updates
   - `errorHandling.test.ts`: Bridge crash, invalid solution, compilation errors
   - `multiSolution.test.ts`: Multiple solutions, switching active solution

8. **Configuration & Packaging**:
   - Complete `package.json` contributions (commands, configuration, views, keybindings)
   - Configure activation events (workspaceContains)
   - Setup extension icon and marketplace assets
   - Configure platform-specific builds (win-x64, linux-x64, osx-arm64)
   - Test installation from .vsix package

### Task Ordering Strategy

**Test-First (Principle 2)**:
- Tests authored before implementation for all components
- Tests initially fail (red), then implementation makes them pass (green)

**Dependency Order**:
1. Models first (TypeScript & C# DTOs) - no dependencies
2. Bridge foundation (stdio, command routing) - depends on models
3. Analysis service (IPC client) - depends on bridge
4. UI components (webview, tree view, problems) - depends on analysis service
5. Commands - depends on all UI components
6. Extension activation - orchestrates all components

**Parallel Execution [P]**:
- Tests in each category can run in parallel (independent files)
- Model creation tasks can run in parallel
- UI component implementation can run in parallel (once analysis service ready)

### Estimated Task Breakdown

- **Setup & Scaffolding**: 5-7 tasks (sequential)
- **TypeScript Tests**: 6 tasks [P]
- **TypeScript Implementation**: 10-12 tasks (mostly [P] after tests pass)
- **C# Bridge Tests**: 3 tasks [P]
- **C# Bridge Implementation**: 5 tasks (after tests pass)
- **Webview UI**: 2-3 tasks
- **Integration Tests**: 4 tasks [P]
- **Configuration & Packaging**: 3-4 tasks

**Total Estimated**: 38-42 tasks

### Constitutional Alignment

All tasks follow constitutional principles:

- **Principle 1 (Symbol Fidelity)**: Bridge reuses existing LoggerUsageExtractor (no Roslyn symbol reimplementation)
- **Principle 2 (Test-First)**: All tasks ordered tests before implementation
- **Principle 3 (Thread-Safe Execution)**: Analysis runs in separate bridge process (inherits LoggerUsage thread safety)
- **Principle 4 (Graceful Degradation)**: Error handling tasks for bridge failures, compilation errors
- **Principle 5 (UX Consistency)**: Theme integration task, VS Code API conventions followed
- **Principle 6 (Performance Contracts)**: Incremental analysis task, progress reporting, cancellation support

### Example Task Format (from /tasks command output)

```markdown
## Task 15: Implement Analysis Service IPC Client [P]

**File**: `src/LoggerUsage.VSCode/analysisService.ts`

**Prerequisites**: Tasks 12-14 (C# bridge process complete)

**Description**: Implement TypeScript service that spawns bridge process and communicates via stdio.

**Acceptance Criteria**:
- Spawn bridge process with correct executable path
- Send AnalysisRequest as JSON via stdin
- Parse AnalysisProgress and AnalysisSuccessResponse from stdout
- Handle AnalysisErrorResponse with user-friendly notifications
- Implement cancellation signal sending
- Close bridge gracefully on extension deactivation

**Test**: `test/analysisService.test.ts` (must pass)

**Constitutional Check**: Principle 4 (error handling for bridge failures)
```

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan. The /plan command only describes the approach above.

## Phase 3+: Future Implementation

*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)
**Phase 4**: Implementation (execute tasks.md following constitutional principles)
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking

*Fill ONLY if Constitution Check has violations that must be justified*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |

## Progress Tracking

*This checklist is updated during execution flow*

**Phase Status**:

- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [x] Phase 2: Task planning complete (/plan command - describe approach only)
- [x] Phase 3: Tasks generated (/tasks command)
- [x] Phase 4: Implementation complete (PARTIAL - core features done, integration tests pending)
- [ ] Phase 5: Validation passed

**Gate Status**:

- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented

**Current Status**: Core extension implemented. Remaining integration tests moved to follow-up feature:
- See `specs/002-vscode-extension-remaining-tests/` for completing integration tests
- 43 integration tests with TODOs require feature implementation
- Includes: error handling, incremental analysis, multi-solution support, UI completion

---
*Based on Constitution v2.0.0 - See `.specify/memory/constitution.md`*
