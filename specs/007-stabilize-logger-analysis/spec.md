# Feature Specification: Stabilize Logger Analysis

**Feature Branch**: `007-stabilize-logger-analysis`  
**Created**: 2026-08-07  
**Status**: Approved for implementation  
**Input**: Deliver the roadmap's 0-3 month phase: correctness, modern .NET compatibility, performance baseline, and release hygiene.

## User Scenarios & Testing

### Primary User Story

As a developer analyzing a modern .NET solution, I receive complete and consistent logging usage results regardless of which public extraction entrypoint I use or which supported C# logging syntax the solution contains.

### Acceptance Scenarios

1. **Given** a direct `ILogger.Log<TState>` call, **when** the compilation is analyzed, **then** the result contains its log level, event ID, state-derived message, location, and direct logger method classification.
2. **Given** equivalent input analyzed through workspace and compilation entrypoints, **when** results are returned, **then** both include equivalent populated summaries.
3. **Given** supported .NET 9 and C# 14 logging patterns, **when** they are analyzed, **then** extraction completes without diagnostics or crashes and returns the expected usages.
4. **Given** a representative large source set, **when** extraction is measured, **then** a repeatable test verifies the project's constitutional latency contract.
5. **Given** repository packages and CI, **when** metadata and lint checks are evaluated, **then** links point to the current repository and lint failures block validation.

### Edge Cases

- `ILogger.Log<TState>` state is a string, a structured state object, or not statically convertible to a useful template.
- `[LoggerMessage]` omits the message or uses an empty message.
- Logging code appears inside a C# 14 extension block.
- A custom logging helper uses a C# 13 `params ReadOnlySpan<T>` parameter.
- An attribute argument uses `nameof` with an unbound generic type.
- A compilation contains no logging usage or lacks required logging symbols.

## Requirements

### Functional Requirements

- **FR-001**: The extractor MUST recognize direct `ILogger.Log<TState>` calls by symbol identity.
- **FR-002**: Direct logger calls MUST be classified as `LoggerMethod`, not `LoggerExtensions`.
- **FR-003**: The extractor MUST capture the log level and event ID from direct logger calls when statically available.
- **FR-004**: The extractor MUST use a string state as the message template when statically available and MUST degrade gracefully for other state types.
- **FR-005**: Both public extraction entrypoints MUST return populated summaries.
- **FR-006**: Public-entrypoint integration tests MUST cover .NET 9 primary-constructor logging, empty messages, C# 14 extension blocks, span-based custom overloads, and unbound-generic `nameof`.
- **FR-007**: Unsupported custom logging methods MUST not be misclassified as framework logging methods.
- **FR-008**: A repeatable scale test MUST exercise extraction against a source file near the 5,000-line constitutional boundary.
- **FR-009**: Package and extension metadata MUST reference `Meir017/dotnet-logging-tools`.
- **FR-010**: VS Code lint failures MUST fail CI.
- **FR-011**: The README roadmap MUST describe implemented behavior and remaining work accurately.

## Success Criteria

- All previously skipped direct `ILogger.Log<TState>` cases pass.
- Summary assertions pass through both public entrypoints.
- All modern compatibility scenarios pass through `LoggerUsageExtractor`.
- The scale test completes under the constitution's 500 ms single-tree threshold after warm-up on supported CI hardware, or records a justified and stable threshold if environment variance proves the constitutional value unsuitable.
- Existing .NET and VS Code test suites remain green.
- Repository metadata contains no stale `dotnet-logging-usage` GitHub URLs in active package manifests.

## Scope Boundaries

This phase does not add SARIF, AI suggestions, incremental workspace caching, wrapper-method dataflow, a new analyzer package, or new report surfaces.

## Review & Acceptance Checklist

- [x] Focused on user-visible correctness, compatibility, performance, and release trust
- [x] No unresolved clarification markers
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Scope and exclusions are explicit

