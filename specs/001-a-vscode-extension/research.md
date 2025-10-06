# Research: VS Code Logging Insights Extension

**Date**: 2025-10-06
**Feature**: VS Code extension for logging insights
**Purpose**: Document technical decisions, alternatives evaluated, and research findings

---

## 1. VS Code Extension Architecture

### Decision: Hybrid TypeScript + C# Architecture

**Chosen Approach**: TypeScript extension host with C# bridge library

**Rationale**:

- VS Code extensions must be written in TypeScript/JavaScript for activation, commands, and UI
- Existing LoggerUsage library is .NET/C# with Roslyn dependencies
- Bridge pattern allows reuse of mature analysis engine without rewrite
- Separation of concerns: TypeScript handles UI/workflow, C# handles Roslyn analysis

**Alternatives Considered**:

1. **Pure TypeScript with Roslyn WASM** - Rejected: Roslyn WASM support immature, large bundle size, performance concerns
2. **Pure C# extension via Ionide pattern** - Rejected: Limited VS Code API access, complex debugging, poor IDE integration
3. **Separate language server protocol (LSP)** - Rejected: Over-engineered for this use case, LSP better suited for language features (completion, diagnostics), not domain-specific insights

**Implementation Notes**:

- Use `child_process` in TypeScript to spawn .NET process for analysis
- Communicate via stdio with JSON serialization
- Bridge executable can be packaged with extension or reference global .NET SDK

---

## 2. Inter-Process Communication (IPC)

### Decision: JSON over stdio

**Chosen Approach**: Spawn .NET bridge process, communicate via stdin/stdout with JSON payloads

**Rationale**:

- Simple, platform-agnostic, no external dependencies
- VS Code `child_process` module provides robust process management
- JSON serialization well-supported in both TypeScript and C#
- Existing LoggerUsage models already serializable

**Alternatives Considered**:

1. **gRPC** - Rejected: Overhead for single-machine IPC, compilation complexity, binary dependencies
2. **Named pipes** - Rejected: Platform-specific implementation differences (Windows vs Unix)
3. **HTTP server in .NET process** - Rejected: Port management complexity, unnecessary network stack overhead
4. **MessagePack** - Rejected: JSON sufficient for expected payload sizes (<1MB typical), simpler debugging

**Protocol Design**:

```text
Request: { "command": "analyze", "workspacePath": "...", "solutionPath": "..." }
Response: { "status": "success", "result": { ...LoggerUsageExtractionResult... } }
Error: { "status": "error", "message": "...", "details": "..." }
Progress: { "status": "progress", "percentage": 45, "message": "Analyzing..." }
```

---

## 3. UI Framework for Webview

### Decision: VS Code Webview API with Vanilla TypeScript + CSS

**Chosen Approach**: Use VS Code's webview API with simple HTML/CSS/TS (no framework)

**Rationale**:

- VS Code webviews are isolated iframes with message passing
- Simple UI requirements (list, filters, search) don't justify framework overhead
- Smaller bundle size, faster load times
- VS Code CSS variables for automatic theme integration
- Lower maintenance burden

**Alternatives Considered**:

1. **React** - Rejected: Overkill for relatively static content display, bundle size overhead
2. **Svelte** - Rejected: While lightweight, still adds build complexity for marginal benefit
3. **Lit** - Rejected: Web Components good fit but unnecessary for this scope
4. **VS Code TreeView API** - Partially used: Good for hierarchical navigation, but combined with webview for rich content (filtering, search, formatting)

**UI Component Strategy**:

- Use VS Code TreeView for outline/navigation in sidebar
- Use Webview for detailed insights panel with filtering/search
- Implement filtering/search in TypeScript extension layer (not webview) for better performance

---

## 4. Incremental Analysis Strategy

### Decision: File-level incremental updates with workspace-level caching

**Chosen Approach**: Cache analysis results per file, re-analyze only changed files on save

**Rationale**:

- Roslyn compilations expensive to recreate from scratch
- LoggerUsage analyzers operate at syntax tree level (per-file)
- Workspace state in VS Code provides change notifications
- Aligns with FR-005 (process projects incrementally)

**Implementation**:

- Maintain `Map<filePath, LoggerUsageInfo[]>` in extension memory
- On `workspace.onDidSaveTextDocument`:
  1. Check if file is C# and part of analyzed solution
  2. Re-analyze only that file via bridge
  3. Update cached results
  4. Refresh UI with new aggregated results
- On solution/workspace change: Full re-analysis

**Caching Strategy**:

- In-memory only (no persistence between sessions)
- Cache invalidation on: file save, file delete, project/solution file change, workspace folder change
- Memory limit: ~100MB for cached results (typical project <1000 files * ~100KB per file cache)

---

## 5. Problems Panel Integration

### Decision: VS Code DiagnosticCollection API

**Chosen Approach**: Use `vscode.languages.createDiagnosticCollection` to publish inconsistencies

**Rationale**:

- Native VS Code API for Problems panel
- Automatic integration with editor (underlines, hovers, quick fixes)
- Standard UX familiar to users
- Per FR-027: display parameter inconsistencies as warnings

**Diagnostic Mapping**:

```typescript
// LoggerUsage inconsistency → VS Code Diagnostic
{
  severity: vscode.DiagnosticSeverity.Warning,
  range: new vscode.Range(startLine, startCol, endLine, endCol),
  message: `Parameter name inconsistency: '${param1}' vs '${param2}'`,
  source: 'LoggerUsage',
  code: 'PARAM_INCONSISTENCY'
}
```

**Diagnostic Categories** (per spec requirements):

- Parameter name inconsistencies
- Missing EventId on log calls
- Potentially sensitive data in log parameters (via DataClassification analysis)

---

## 6. Configuration Management

### Decision: VS Code settings with workspace/user scope support

**Chosen Approach**: Standard VS Code configuration with `contributes.configuration` in package.json

**Per clarifications** (comprehensive configuration):

```json
{
  "loggerUsage.autoAnalyzeOnSave": true,
  "loggerUsage.excludePatterns": ["**/obj/**", "**/bin/**"],
  "loggerUsage.performanceThresholds": {
    "maxFilesPerAnalysis": 1000,
    "analysisTimeoutMs": 300000
  },
  "loggerUsage.enableProblemsIntegration": true,
  "loggerUsage.filterDefaults": {
    "logLevels": ["Information", "Warning", "Error"],
    "showInconsistenciesOnly": false
  }
}
```

**Settings Precedence**: Workspace > Folder > User (standard VS Code behavior)

---

## 7. Multi-Solution Workspace Handling

### Decision: Active solution detection with explicit selection

**Per clarifications**: Analyze only currently active/selected solution

**Implementation Strategy**:

1. **Solution Detection**:
   - Scan workspace for `.sln` files
   - If multiple found, track "active" solution (default: first opened file's solution)
   - Provide command: "LoggerUsage: Select Active Solution"

2. **Active Solution Tracking**:
   - Monitor `window.activeTextEditor` changes
   - Determine which solution contains active file
   - Re-analyze if active solution changes

3. **UI Indication**:
   - Status bar item showing active solution name
   - Insights panel title: "Logging Insights - [SolutionName]"

---

## 8. Performance Optimization

### Best-Effort with Progress Feedback

**Per clarifications**: No hard time limits, continuous progress feedback

**Progress Reporting Strategy**:

```typescript
// Report progress during analysis
const progressToken = vscode.window.withProgress({
  location: vscode.ProgressLocation.Notification,
  title: "Analyzing logging usage",
  cancellable: true
}, async (progress, cancellationToken) => {
  // Bridge reports progress via IPC
  // Update progress.report({ increment: 10, message: "Analyzing file 10/100" })
});
```

**Cancellation Support**:

- VS Code provides `CancellationToken`
- Pass cancellation signal to bridge process
- Bridge monitors cancellation, terminates Roslyn analysis gracefully
- Return partial results if cancelled

**Memory Management**:

- Dispose Roslyn compilations after analysis
- Clear cached results on workspace close
- Monitor process memory, warn if exceeds 1GB

---

## 9. Testing Strategy

### Multi-Layer Test Approach

**1. Extension Tests** (VS Code Extension Test Runner):
- Activation scenarios
- Command execution
- Settings integration
- Webview rendering
- File watcher behavior

**2. Bridge Integration Tests** (xUnit):
- Bridge process spawn/communication
- JSON serialization round-trip
- Error handling and graceful degradation
- Progress reporting

**3. E2E Tests** (VS Code Extension Test Runner):
- Full workflow: open workspace → analyze → display → filter → navigate
- Multi-solution scenarios
- Cancellation scenarios
- Performance with sample projects (10K, 50K LOC)

**Test Fixtures**:
- Sample C# projects with known logging patterns
- Edge cases: no logging, missing dependencies, compilation errors
- Multi-solution workspaces

---

## 10. Packaging and Distribution

### Decision: VS Code Marketplace with bundled .NET runtime

**Distribution Options**:
1. **Bundled .NET Runtime** (Recommended):
   - Include .NET 10 runtime with extension
   - Platform-specific builds (win-x64, linux-x64, osx-arm64)
   - Larger download size (~50MB per platform) but zero setup
   - Use `vsce` with platform-specific packaging

2. **Require User-Installed .NET**:
   - Smaller extension package (<5MB)
   - Detect .NET SDK/runtime, prompt install if missing
   - Better for users with existing .NET development setup

**Chosen**: Bundled runtime for initial release (better first-run experience), evaluate user-installed .NET based on feedback

**Package.json essentials**:
```json
{
  "engines": { "vscode": "^1.85.0" },
  "activationEvents": ["workspaceContains:**/*.sln", "workspaceContains:**/*.csproj"],
  "main": "./out/extension.js",
  "contributes": {
    "commands": [...],
    "configuration": {...},
    "views": {...}
  }
}
```

---

## Research Summary

| Decision Area | Chosen Approach | Key Rationale |
|---------------|----------------|---------------|
| Extension Architecture | Hybrid TypeScript + C# bridge | Reuse existing .NET analysis, VS Code API requirements |
| IPC Mechanism | JSON over stdio | Simple, cross-platform, debuggable |
| UI Framework | Vanilla TS + Webview API | Minimal overhead, sufficient for requirements |
| Incremental Analysis | File-level caching | Performance, aligns with FR-005 |
| Problems Integration | DiagnosticCollection API | Native VS Code, automatic editor integration |
| Configuration | VS Code settings (workspace+user) | Standard pattern, per clarifications (comprehensive) |
| Multi-Solution | Active solution only | Per clarifications, simpler UX |
| Performance | Best-effort + progress | Per clarifications, no hard limits |
| Testing | Multi-layer (unit + integration + E2E) | Comprehensive coverage per Principle 2 |
| Distribution | Bundled .NET runtime | Best first-run experience |

---

**Dependencies Confirmed**:
- VS Code Extension API (TypeScript)
- Existing LoggerUsage library
- LoggerUsage.MSBuild for solution loading
- Microsoft.CodeAnalysis (Roslyn) - transitively via LoggerUsage
- .NET 10 runtime

**No NEEDS CLARIFICATION remain** - all ambiguities resolved in clarification session.

**Ready for Phase 1**: Design (data models, contracts, quickstart)
