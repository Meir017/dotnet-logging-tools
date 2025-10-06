<!--
Sync Impact Report
==================
Version Change: 1.0.0 → 2.0.0 (Major revision with restructured principles)
Modified Principles:
  - Principle I: "Library-First Architecture" → Removed (implementation detail, not constitutional)
  - Principle II: "Multi-Interface Exposure" → Removed (moved to technical docs)
  - Principle III: "Test-First Development" → Expanded to Principle 2 (Testing Standards)
  - Principle IV: "Analyzer Pattern Architecture" → Integrated into Principle 1 & 3 (Code Quality)
  - Principle V: "Roslyn Symbol Resolution Discipline" → Expanded to Principle 1 (Code Quality - Roslyn Symbol Fidelity)
  - Principle VI: "Observability & Structured Logging" → Integrated into Principle 4 (Graceful Degradation and Diagnostics)
  - Principle VII: "Versioning & Package Management" → Removed (operational detail, not constitutional)
Added Sections:
  - Principle 1: Roslyn Symbol Fidelity (code quality focus)
  - Principle 2: Test-First Development Discipline (testing standards)
  - Principle 3: Thread-Safe Parallel Execution (performance requirements)
  - Principle 4: Graceful Degradation and Diagnostics (code quality)
  - Principle 5: User Experience Consistency Across Outputs (UX consistency)
  - Principle 6: Performance and Scalability Contracts (performance requirements)
  - Constitutional Validation Checklist (enforcement mechanism)
  - Principle-Driven Examples (guidance appendix)
Removed Sections:
  - Technical Constraints (moved to .github/copilot-instructions.md)
  - Development Workflow (moved to .github/copilot-instructions.md)
  - Code Organization, Testing Organization, Naming Conventions (moved to .github/copilot-instructions.md)
Templates Status:
  ✅ plan-template.md - Constitution Check section updated with new principle gates
  ✅ spec-template.md - Aligned with requirement quality standards (testable, unambiguous)
  ✅ tasks-template.md - Aligned with TDD and parallel execution principles
  ⚠️  agent-file-template.md - Should be updated to reference constitutional principles
Follow-up TODOs:
  - Update .github/copilot-instructions.md to reference this constitution for governance
  - Consider adding automated checks for Principle 1 (symbol comparison patterns)
  - Consider adding automated checks for Principle 6 (performance regression tests)
-->

# Project Constitution: dotnet-logging-usage

**Repository**: <https://github.com/Meir017/dotnet-logging-usage>
**Version**: 2.0.0
**Ratification Date**: 2025-10-05
**Last Amended**: 2025-10-05

---

## Purpose and Scope

This constitution establishes the foundational principles governing the development, maintenance, and evolution of the dotnet-logging-usage library. This project is a sophisticated static code analysis tool that extracts and analyzes logging patterns from .NET projects using Roslyn, targeting .NET 10 and focusing on Microsoft.Extensions.Logging patterns.

The constitution applies to all components: LoggerUsage (core library), LoggerUsage.Cli (command-line tool), LoggerUsage.Mcp (Model Context Protocol server), and LoggerUsage.MSBuild (MSBuild integration).

---

## Principles

### Principle 1: Roslyn Symbol Fidelity

**Principle Statement**:
All symbol comparisons MUST use semantic symbol comparison via `ISymbol.Equals()` or `SymbolEqualityComparer`. String-based name comparisons for type/method identification are PROHIBITED except for diagnostic logging purposes.

**Rationale**:
Roslyn's symbol model provides accurate semantic information that accounts for namespace disambiguation, generic type instantiation, and overload resolution. String-based comparisons are brittle and fail in the presence of type aliases, using directives, and similar names from different namespaces. This principle ensures analyzer correctness and prevents false positives/negatives.

**Implementation Requirements**:

- All analyzers MUST use the `LoggingTypes` class to access pre-resolved logging symbols
- Symbol comparison MUST use `SymbolEqualityComparer.Default.Equals()` or `ISymbol.Equals()`
- When adding new pattern detection, symbol resolution MUST be added to `LoggingTypes` constructor
- Code reviews MUST reject string-based type/method identification patterns
- Exception: Diagnostic messages may include symbol names as strings for human readability

**Testing Requirements**:

- Test cases MUST include scenarios with namespace conflicts and type aliases
- Tests MUST validate correct symbol resolution in presence of `using` directives
- Edge case tests MUST cover missing symbols and incomplete compilations

---

### Principle 2: Test-First Development Discipline

**Principle Statement**:
All new functionality MUST have failing tests written BEFORE implementation. Integration and contract tests are mandatory; unit tests are encouraged for complex logic. Tests MUST use `TestUtils.CreateCompilationAsync()` and follow the established patterns.

**Rationale**:
Test-first development ensures that requirements are clearly understood before coding begins, produces testable designs, and provides immediate validation that implementation satisfies specifications. This is critical for a code analysis tool where correctness is paramount.

**Implementation Requirements**:

- New analyzers MUST have test files created before implementation
- Test files MUST follow naming convention: `{AnalyzerName}Tests.cs`
- Tests MUST use descriptive names: `TestMethod_WithCondition_ExpectedResult`
- Theory tests with `[MemberData]` MUST be used for parameter variations
- Tests MUST validate: method type, parameters, location, EventId/LogLevel
- All tests MUST initially FAIL demonstrating they test real behavior

**Testing Requirements**:

- Each analyzer MUST have tests covering: basic cases, edge cases, malformed input, missing symbols
- Test coverage MUST include parallel execution scenarios for thread safety
- Mock generated code patterns (e.g., LoggerMessage source generation) when needed
- Performance tests MUST exist for operations processing large syntax trees

---

### Principle 3: Thread-Safe Parallel Execution

**Principle Statement**:
All analyzers MUST be stateless and thread-safe. Extraction MUST run in parallel across syntax trees using thread-safe collections. Shared mutable state is PROHIBITED in analyzer implementations.

**Rationale**:
The library analyzes potentially large codebases with many files. Parallel execution is essential for acceptable performance. Thread safety violations lead to non-deterministic failures that are difficult to debug and undermine user trust.

**Implementation Requirements**:

- Analyzers MUST implement `ILoggerUsageAnalyzer` without instance state
- Result aggregation MUST use `ConcurrentBag<T>` or equivalent thread-safe collections
- The `LoggingTypes` class MAY cache expensive symbol lookups (immutable after construction)
- Code reviews MUST verify no shared mutable state in analyzer code paths
- Diagnostic logging MUST use thread-safe ILogger injection

**Testing Requirements**:

- Integration tests MUST verify correct results when processing multiple files simultaneously
- Stress tests SHOULD process 100+ files concurrently to detect race conditions
- Tests MUST validate result completeness (no lost results due to concurrency issues)

---

### Principle 4: Graceful Degradation and Diagnostics

**Principle Statement**:
Analyzers MUST never throw unhandled exceptions. When required symbols are missing or compilation is incomplete, analysis MUST skip gracefully and log warnings with structured context. Partial results are acceptable; crashes are not.

**Rationale**:
The library operates in diverse environments with varying project configurations, potentially incomplete compilations, and missing references. Users should receive best-effort results rather than tool failures. Comprehensive diagnostics enable troubleshooting without requiring debugger attachment.

**Implementation Requirements**:

- Analyzers MUST check for null symbols and return empty results when unavailable
- Missing symbols MUST be logged at Warning level with structured parameters
- Log messages MUST include: file path, method/type name, missing symbol name
- Exceptions in analyzer code MUST be caught by the orchestrator and logged
- User-facing error messages MUST be actionable (suggest adding references, checking compilation)

**Testing Requirements**:

- Tests MUST cover scenarios with missing Microsoft.Extensions.Logging references
- Tests MUST verify graceful handling of incomplete semantic models
- Tests MUST validate structured logging parameters for diagnostics
- Negative tests MUST confirm no exceptions escape analyzer boundaries

---

### Principle 5: User Experience Consistency Across Outputs

**Principle Statement**:
All output formats (HTML, JSON, Markdown) MUST present equivalent information with format-appropriate styling. Reports MUST be accessible (dark mode support, semantic HTML), machine-readable (versioned JSON schema), and human-friendly (collapsible sections, filtering).

**Rationale**:
Users interact with the library through multiple interfaces (CLI, MCP, direct API usage) and consume output in various contexts (CI/CD pipelines, IDE integration, documentation). Consistency ensures predictable workflows; accessibility ensures broad usability; versioned schemas enable reliable automation.

**Implementation Requirements**:

- HTML reports MUST support both light and dark modes via CSS media queries
- HTML MUST use semantic elements (`<nav>`, `<article>`, `<section>`) with ARIA labels
- JSON output MUST include schema version (`"schemaVersion": "2.0"`)
- JSON schema changes MUST increment version (major for breaking, minor for additions)
- Markdown reports MUST use consistent heading hierarchy and formatting
- All outputs MUST include: summary statistics, parameter analysis, file locations
- Report generators MUST be independently testable with mock data

**Testing Requirements**:

- Golden file tests MUST validate report structure for each format
- Tests MUST verify schema version is incremented on model changes
- Accessibility tests SHOULD validate HTML against WCAG 2.1 AA guidelines
- Tests MUST verify equivalent data across all output formats

---

### Principle 6: Performance and Scalability Contracts

**Principle Statement**:
Analysis of a single syntax tree MUST complete in under 500ms for files up to 5,000 lines. Memory usage MUST remain under 500MB for analyzing 100 files. Performance degradation MUST be investigated and addressed before release.

**Rationale**:
As a development tool potentially running in watch mode or IDE contexts, performance directly impacts developer productivity. Predictable resource usage enables integration into CI/CD pipelines with resource constraints. Performance regressions erode user trust and adoption.

**Implementation Requirements**:

- Analyzers MUST NOT enumerate entire syntax trees unnecessarily (use targeted descendant queries)
- Symbol lookups MUST be cached in `LoggingTypes` to avoid repeated compilation queries
- Large result collections MUST stream rather than materialize entire result sets
- Performance-critical paths MAY use `Stopwatch` for debug-level timing diagnostics
- Memory allocations in hot paths MUST be minimized (avoid LINQ chains with many allocations)

**Testing Requirements**:

- Benchmark tests MUST verify single-file analysis completes under 500ms (5K lines)
- Memory profiling tests MUST verify peak memory under 500MB (100 files)
- Performance regression tests MUST fail CI if analysis time increases >10% without justification
- Profiling results SHOULD be captured for release notes demonstrating performance characteristics

---

## Constitutional Validation Checklist

The following checklist MUST be used during plan.md Constitution Check sections:

### Code Quality Gates

- [ ] **Symbol Fidelity**: No string-based type/method comparisons (Principle 1)
- [ ] **Thread Safety**: Analyzers are stateless, use thread-safe collections (Principle 3)
- [ ] **Error Handling**: No unhandled exceptions, graceful degradation implemented (Principle 4)
- [ ] **Performance**: Analysis meets latency/memory contracts (Principle 6)

### Testing Gates

- [ ] **Test-First**: Tests exist before implementation and initially failed (Principle 2)
- [ ] **Test Coverage**: Basic, edge, error, and thread safety cases covered (Principles 2, 3)
- [ ] **Performance Tests**: Benchmark tests verify contracts (Principle 6)

### User Experience Gates

- [ ] **Output Consistency**: All formats (HTML/JSON/Markdown) present equivalent data (Principle 5)
- [ ] **Accessibility**: HTML reports support dark mode and semantic markup (Principle 5)
- [ ] **Schema Versioning**: JSON schema version updated if models changed (Principle 5)

### Documentation Gates

- [ ] **XML Documentation**: Public APIs have complete XML docs
- [ ] **Change Documentation**: Breaking changes documented in release notes
- [ ] **Example Updates**: README and quickstart guides reflect new functionality

---

## Appendix: Principle-Driven Examples

### Example 1: Adding a New Analyzer (Principles 1, 2, 3, 4)

**Compliant Approach**:

```csharp
// 1. Add symbol resolution to LoggingTypes (Principle 1)
public INamedTypeSymbol MyNewPattern { get; }

// 2. Write failing tests first (Principle 2)
[Fact]
public async Task AnalyzeMyPattern_WithValidUsage_ExtractsCorrectly()
{
    var code = """...""";
    var compilation = await TestUtils.CreateCompilationAsync(code);
    var result = await extractor.ExtractAsync(compilation);
    Assert.Empty(result.Usages); // FAILS until implemented
}

// 3. Implement stateless analyzer (Principle 3)
internal class MyPatternAnalyzer : ILoggerUsageAnalyzer
{
    public IEnumerable<LoggerUsageInfo> Analyze(
        LoggingTypes loggingTypes,
        SyntaxNode root,
        SemanticModel semanticModel)
    {
        var nodes = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var node in nodes)
        {
            // 4. Graceful null handling (Principle 4)
            if (semanticModel.GetOperation(node) is not IInvocationOperation op)
                continue;

            // Principle 1: Symbol comparison, not string comparison
            if (SymbolEqualityComparer.Default.Equals(
                op.TargetMethod.ContainingType,
                loggingTypes.MyNewPattern))
            {
                yield return ExtractUsage(op, node);
            }
        }
    }
}
```

### Example 2: Report Generation (Principle 5)

**Compliant Approach**:

```csharp
// All generators implement common interface
public interface IReportGenerator
{
    string Generate(LoggerUsageExtractionResult result);
}

// JSON includes schema version
public class JsonReportGenerator : IReportGenerator
{
    public string Generate(LoggerUsageExtractionResult result)
    {
        var report = new
        {
            schemaVersion = "2.0", // Principle 5: versioned schema
            summary = result.Summary,
            usages = result.Usages
        };
        return JsonSerializer.Serialize(report, _options);
    }
}

// HTML includes accessibility features
public class HtmlReportGenerator : IReportGenerator
{
    private const string Template = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta name="color-scheme" content="light dark"> <!-- dark mode -->
            <style>
                @media (prefers-color-scheme: dark) { /* Principle 5 */ }
            </style>
        </head>
        <body>
            <nav aria-label="Report navigation"> <!-- semantic HTML --> </nav>
            <main>
                <section aria-labelledby="summary-heading">
                    <h2 id="summary-heading">Summary</h2>
                    {summary}
                </section>
            </main>
        </body>
        </html>
        """;
}
```

---

## Governance

### Amendment Procedure

Constitution amendments require:

1. A pull request proposing changes with rationale
2. Review by at least one project maintainer
3. Update to `Version` following semantic versioning
4. Execution of Sync Impact Report generation (via constitution.prompt.md)
5. Updates to dependent templates and documentation

### Versioning Policy

Constitution version follows semantic versioning (MAJOR.MINOR.PATCH):

- **MAJOR**: Backward-incompatible principle removals or redefinitions that invalidate existing practices
- **MINOR**: New principles, expanded sections, or material guidance additions
- **PATCH**: Clarifications, typo fixes, non-semantic wording improvements

### Compliance Review

All pull requests MUST be reviewed for constitutional compliance:

- Code reviews MUST reference specific principles when requesting changes
- CI checks SHOULD automate verifiable requirements (e.g., test naming, warnings as errors)
- Major feature additions MUST include a constitution compliance checklist
- Annual review SHOULD assess whether principles remain appropriate for project evolution

### Enforcement

Violations discovered post-merge:

1. Create issue documenting violation and impacted principle
2. Prioritize remediation based on severity (user-facing impact, correctness risk)
3. Add tests preventing recurrence
4. Update review guidelines if pattern missed in review

---

*This constitution establishes the foundational governance for the dotnet-logging-usage project. All development activities must align with these principles to ensure consistency, quality, and maintainability.*
