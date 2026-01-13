
# Implementation Plan: MCP STDIO and HTTP Transport Support

**Branch**: `005-mcp-stdio-and` | **Date**: 2025-10-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/005-mcp-stdio-and/spec.md`

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

The LoggerUsage.Mcp server currently only supports HTTP transport. This feature adds support for both STDIO and HTTP transports, selectable via a `--transport` command line argument. This enables the MCP server to be used in different environments - HTTP for web-based integrations and STDIO for CLI tools, desktop applications, and VS Code extensions. The implementation leverages ASP.NET Core's configuration system to accept command-line arguments and conditionally register the appropriate MCP transport.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: ModelContextProtocol.Server, Microsoft.Extensions.Configuration.CommandLine
**Storage**: N/A
**Testing**: xUnit, integration tests through `LoggerUsage.Mcp.Tests`
**Target Platform**: Windows, Linux, macOS (cross-platform console application)
**Project Type**: Single (console application with ASP.NET Core hosting)
**Performance Goals**: Transport initialization <100ms, no impact on analysis performance
**Constraints**: Must maintain backward compatibility (default to HTTP), zero breaking changes to existing MCP server functionality
**Scale/Scope**: Single project modification (LoggerUsage.Mcp), ~5 files changed, add configuration handling

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Code Quality Gates

- [x] **Symbol Fidelity**: No string-based type/method comparisons (Constitution Principle 1)
  - Rationale: This feature does not involve Roslyn symbol resolution or code analysis. It only modifies server startup configuration. Not applicable.
- [x] **Thread Safety**: Analyzers are stateless, use thread-safe collections (Constitution Principle 3)
  - Rationale: This feature does not modify analyzers or parallel extraction logic. Configuration is set once at startup. Not applicable.
- [x] **Error Handling**: No unhandled exceptions, graceful degradation implemented (Constitution Principle 4)
  - Rationale: Configuration validation will throw clear exceptions with actionable messages if invalid transport specified. Server will fail fast with helpful error messages.
- [x] **Performance**: Analysis meets latency/memory contracts (Constitution Principle 6)
  - Rationale: This feature only affects server startup time (<100ms additional overhead). Analysis performance is unaffected.

### Testing Gates

- [x] **Test-First**: Tests exist before implementation and initially failed (Constitution Principle 2)
  - Rationale: Integration tests will be written first to verify STDIO and HTTP transport modes work correctly.
- [x] **Test Coverage**: Basic, edge, error, and thread safety cases covered (Constitution Principles 2, 3)
  - Rationale: Tests will cover: default HTTP, explicit HTTP, STDIO, invalid transport, missing transport argument.
- [x] **Performance Tests**: Benchmark tests verify contracts (Constitution Principle 6)
  - Rationale: Not applicable - transport initialization is trivial overhead, no performance-critical paths added.

### User Experience Gates

- [x] **Output Consistency**: All formats (HTML/JSON/Markdown) present equivalent data (Constitution Principle 5)
  - Rationale: This feature does not modify output formats. MCP protocol responses are identical regardless of transport.
- [x] **Accessibility**: HTML reports support dark mode and semantic markup (Constitution Principle 5)
  - Rationale: This feature does not modify HTML report generation. Not applicable.
- [x] **Schema Versioning**: JSON schema version updated if models changed (Constitution Principle 5)
  - Rationale: No model changes - only server configuration. Not applicable.

### Documentation Gates

- [x] **XML Documentation**: Public APIs have complete XML docs
  - Rationale: No new public APIs exposed. Internal configuration classes will have XML docs.
- [x] **Change Documentation**: Breaking changes documented in release notes
  - Rationale: No breaking changes - defaults to HTTP (existing behavior). New `--transport` argument is purely additive.
- [x] **Example Updates**: README and quickstart guides reflect new functionality
  - Rationale: README will be updated with command-line examples for both transport modes.

## Project Structure

### Documentation (this feature)

```
specs/005-mcp-stdio-and/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)

```
src/LoggerUsage.Mcp/
├── Program.cs                    # MODIFIED: Add transport configuration
├── appsettings.json             # MODIFIED: Add transport configuration section
├── appsettings.Development.json # MODIFIED: Optional STDIO default for dev
├── TransportOptions.cs          # NEW: Configuration model for transport
└── obj/

test/LoggerUsage.Mcp.Tests/
├── TransportConfigurationTests.cs  # NEW: Test transport selection
└── obj/
```

**Structure Decision**: Single project modification focused on LoggerUsage.Mcp. This is a configuration-level change that does not require new projects or major architectural shifts. The existing test project structure is used for validation.

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

- Load `.specify/templates/tasks-template.md` as base
- Generate tasks from Phase 1 design docs (data-model.md, contracts/, quickstart.md)
- Test-first approach: configuration tests before implementation
- Each configuration scenario → integration test task [P]
- Each transport mode → transport registration task
- Implementation tasks to make tests pass
- Documentation tasks for README updates

**Ordering Strategy**:

1. **Setup Phase**: Create TransportOptions model and enum [P]
2. **Test Phase**: Write configuration binding tests (all parallel) [P]
   - Test default HTTP transport [P]
   - Test explicit HTTP transport [P]
   - Test STDIO transport [P]
   - Test invalid transport [P]
   - Test command-line override [P]
3. **Implementation Phase**: Implement conditional transport registration
4. **Integration Phase**: Update Program.cs to use TransportOptions
5. **Documentation Phase**: Update README with command-line examples [P]

**Estimated Output**: 12-15 numbered, ordered tasks in tasks.md

**Parallel Execution Markers**:
- Model creation: [P] - independent of tests
- Test creation: [P] - all test files independent
- Documentation: [P] - independent of implementation

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
- [x] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:

- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented (None)

**Artifacts Generated**:

- [x] research.md - All technology decisions documented
- [x] data-model.md - TransportOptions and TransportMode defined
- [x] contracts/configuration-contract.md - CLI/config API contract
- [x] quickstart.md - 5 test scenarios documented
- [x] .github/copilot-instructions.md - Updated with new framework dependencies
- [x] tasks.md - 17 numbered tasks with TDD ordering and parallel execution markers

---
*Based on Constitution v2.0.0 - See `.specify/memory/constitution.md`*
*Phase 3 complete. Ready for implementation (Phase 4).*
