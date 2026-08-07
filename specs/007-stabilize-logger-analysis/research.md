# Research Decisions

## Direct `ILogger.Log<TState>`

**Decision**: Treat the interface method as a distinct logger usage and extract a template only when the `state` argument is statically a string.  
**Rationale**: `TState` is intentionally arbitrary; interpreting structured state or invoking the formatter would require unsafe execution or speculative dataflow.  
**Alternative rejected**: Evaluate the formatter delegate. Static analysis must not execute user code.

## Summary consistency

**Decision**: Populate the summary inside each public extraction result before returning it.  
**Rationale**: Direct consumers of the compilation entrypoint currently receive an empty summary, while workspace consumers receive a populated aggregate.  
**Alternative rejected**: Require callers to invoke `LoggerUsageSummarizer`; this contradicts the result model and existing workspace behavior.

## Modern syntax compatibility

**Decision**: Add integration tests first and change production code only for demonstrated failures.  
**Rationale**: Roslyn semantic operations should already normalize many C# 14 syntax differences. Syntax-specific branches would add unnecessary maintenance.

## Performance testing

**Decision**: Start with an in-memory scale test in the existing integration test project.  
**Rationale**: It exercises the public contract without adding a benchmark dependency or project. A dedicated BenchmarkDotNet suite remains a follow-up if profiling becomes routine.

## Release hygiene

**Decision**: Correct active package/extension metadata and CI policy in this phase.  
**Rationale**: Broken links and non-blocking lint reduce release trust and are low-risk changes.

## Authoritative references

- https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger.log
- https://learn.microsoft.com/dotnet/core/extensions/logging/source-generation
- https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14
- https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.operations.iinvocationoperation

