
# Implementation Plan: MCP Progress Tracking Support

**Branch**: `005-mcp-progress-tracking` | **Date**: 2025-10-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `D:\Repos\Meir017\dotnet-logging-usage\specs\005-mcp-progress-tracking\spec.md`

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

Add MCP progress tracking support to the `analyze_logger_usages_in_csproj` tool in LoggerUsage.Mcp server. The tool will accept an optional `progressToken` parameter and send progress notifications during analysis using the existing `IProgress<ProgressReport>` infrastructure in `LoggerUsageExtractor` and the MCP C# SDK's `SendNotificationAsync` method. This enables clients to display real-time progress updates (e.g., "Analyzing file 25 of 100") during long-running analysis operations.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: ModelContextProtocol.Server, Microsoft.Extensions.DependencyInjection, LoggerUsageExtractor (existing)
**Storage**: N/A (stateless tool, progress in memory)
**Testing**: xUnit, existing test infrastructure (LoggerUsage.Mcp.Tests)
**Target Platform**: ASP.NET Core web server (cross-platform: Windows, Linux, macOS)
**Project Type**: Single project (MCP server enhancement)
**Performance Goals**: Progress notification overhead <5% of analysis time, no blocking of main analysis
**Constraints**: Must maintain backward compatibility with clients not using progress tokens
**Scale/Scope**: Single tool enhancement, ~100-200 LOC, 3-5 new test cases
**User Input Context**: Support MCP progress tracking via recent C# SDK capabilities (https://github.com/modelcontextprotocol/csharp-sdk/blob/f28639119b3596b0357ea4b979d3289cad54054a/docs/concepts/progress/progress.md)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Code Quality Gates

- [x] **Symbol Fidelity**: No string-based type/method comparisons (Constitution Principle 1)
  - Rationale: This feature does not involve Roslyn symbol analysis. It only adds MCP protocol handling to an existing tool. No symbol comparisons needed. COMPLIANT.
- [x] **Thread Safety**: Analyzers are stateless, use thread-safe collections (Constitution Principle 3)
  - Rationale: Not adding new analyzers. The progress adapter will be stateless. Progress notifications are fire-and-forget (no shared mutable state). COMPLIANT.
- [x] **Error Handling**: No unhandled exceptions, graceful degradation implemented (Constitution Principle 4)
  - Rationale: Progress notification errors will be caught and logged without failing analysis. Progress token is optional, tool works without it. COMPLIANT.
- [x] **Performance**: Analysis meets latency/memory contracts (Constitution Principle 6)
  - Rationale: Progress notifications are async and non-blocking. Overhead target <5% verified by benchmark tests. No impact to existing contracts. COMPLIANT.

### Testing Gates

- [x] **Test-First**: Tests exist before implementation and initially failed (Constitution Principle 2)
  - Rationale: Will write integration tests in LoggerUsage.Mcp.Tests before implementation. Tests verify progress notifications sent when token provided. COMPLIANT.
- [x] **Test Coverage**: Basic, edge, error, and thread safety cases covered (Constitution Principles 2, 3)
  - Rationale: Tests cover: with token, without token, error handling, single file, multiple files. No thread safety concerns (stateless adapter). COMPLIANT.
- [x] **Performance Tests**: Benchmark tests verify contracts (Constitution Principle 6)
  - Rationale: Add benchmark test comparing analysis time with/without progress tracking. Verify <5% overhead requirement. COMPLIANT.

### User Experience Gates

- [x] **Output Consistency**: All formats (HTML/JSON/Markdown) present equivalent data (Constitution Principle 5)
  - Rationale: This feature only affects MCP protocol (notifications). No changes to output formats. Analysis result unchanged. NOT APPLICABLE.
- [x] **Accessibility**: HTML reports support dark mode and semantic markup (Constitution Principle 5)
  - Rationale: No UI changes. MCP progress is a protocol feature, not user-facing output. NOT APPLICABLE.
- [x] **Schema Versioning**: JSON schema version updated if models changed (Constitution Principle 5)
  - Rationale: No changes to `LoggerUsageExtractionResult` model. Progress via notifications, not response body. Schema version unchanged. COMPLIANT.

### Documentation Gates

- [x] **XML Documentation**: Public APIs have complete XML docs
  - Rationale: Will add XML docs to new `progressToken` parameter and any public progress adapter class. COMPLIANT.
- [x] **Change Documentation**: Breaking changes documented in release notes
  - Rationale: No breaking changes. Adding optional parameter maintains backward compatibility. Will document new feature in release notes. COMPLIANT.
- [x] **Example Updates**: README and quickstart guides reflect new functionality
  - Rationale: Will update quickstart.md with example of using progress token. LoggerUsage.Mcp README (if exists) will document progress support. COMPLIANT.

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
src/
├── LoggerUsage.Mcp/
│   ├── Program.cs                          # Tool implementation (add progressToken parameter)
│   └── McpProgressAdapter.cs               # NEW: Bridge IProgress<ProgressReport> to MCP notifications
└── LoggerUsage/
    └── Models/
        └── ProgressReport.cs               # Existing model (no changes)

test/
└── LoggerUsage.Mcp.Tests/
    └── ProgressTrackingTests.cs            # NEW: Integration tests for progress notifications
```

**Structure Decision**: Single project enhancement (Option 1). Adding progress tracking to existing LoggerUsage.Mcp project. New McpProgressAdapter class in src/LoggerUsage.Mcp. New test file in test/LoggerUsage.Mcp.Tests.

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

This feature enhances an existing MCP tool with progress tracking. Tasks will be generated in TDD order:

1. **Test Tasks First** (following Constitution Principle 2):
   - Create failing integration test for tool with progress token
   - Create failing integration test for tool without progress token
   - Create benchmark test for performance overhead

2. **Implementation Tasks**:
   - Create `McpProgressAdapter` class (bridge between IProgress and MCP)
   - Update `LoggerUsageExtractorTool.AnalyzeLoggerUsagesInCsproj` method signature
   - Wire adapter in tool method when progress token provided
   - Add XML documentation

3. **Validation Tasks**:
   - Run all tests and verify they pass
   - Run benchmark and verify <5% overhead
   - Execute quickstart.md validation steps

**Task Categories**:
- **[P]** = Parallel-safe (independent file creation)
- **[S]** = Sequential (depends on previous tasks)
- **[T]** = Test task (must come before implementation)

**Ordering Strategy**:

1. TDD order: Tests before implementation (Constitution Principle 2)
2. Dependency order: 
   - Contract tests can be written in parallel [P]
   - Adapter implementation depends on test existence [S]
   - Tool enhancement depends on adapter [S]
   - Integration tests run after implementation [S]
   - Benchmark runs after all tests pass [S]

**Estimated Output**: 12-15 tasks in tasks.md

**Task Types**:
- 3 contract test tasks [P]
- 1 adapter implementation task
- 1 tool enhancement task
- 3 integration test tasks
- 1 benchmark test task
- 1 documentation task
- 1 quickstart validation task

**Dependencies**:
```
Contract Tests [P]
    ↓
McpProgressAdapter Implementation [S]
    ↓
Tool Enhancement (add progressToken parameter) [S]
    ↓
Integration Tests [S]
    ↓
Benchmark Test [S]
    ↓
Documentation + Quickstart Validation [S]
```

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

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
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:

- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved (none in spec)
- [x] Complexity deviations documented (none - all principles compliant)

---
*Based on Constitution v2.0.0 - See `.specify/memory/constitution.md`*
