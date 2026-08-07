# Quickstart: Hardened Analysis and SARIF

## Run extraction with cancellation

Library callers use the new cancellation-aware overload while existing overloads remain valid:

```csharp
using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var result = await extractor.ExtractLoggerUsagesAsync(
    workspace,
    progress: null,
    cancellationSource.Token);
```

Cancellation propagates as `OperationCanceledException`; it does not return an empty success result.

## Generate SARIF from the CLI

```powershell
dotnet run --project .\src\LoggerUsage.Cli -- `
  .\logging-usage.slnx `
  .\artifacts\logger-usage.sarif
```

The `.sarif` extension selects SARIF 2.1.0. The CLI uses the nearest Git repository root when available and otherwise derives a common source root from the findings.

## Upload to GitHub code scanning

```yaml
- name: Generate LoggerUsage SARIF
  run: dotnet run --project src/LoggerUsage.Cli -- logging-usage.slnx artifacts/logger-usage.sarif

- name: Upload LoggerUsage SARIF
  uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: artifacts/logger-usage.sarif
```

The initial report produces:

- `LUT001`: parameter type mismatch.
- `LUT002`: parameter casing inconsistency.

Normal logging calls are not code-scanning alerts.

## Validate the implementation

```powershell
dotnet build .\logging-usage.slnx --no-restore
dotnet run --project .\test\LoggerUsage.Tests\LoggerUsage.Tests.csproj --no-build -- --progress off
dotnet run --project .\test\LoggerUsage.MSBuild.Tests\LoggerUsage.MSBuild.Tests.csproj --no-build -- --progress off
```

Performance evidence must include the warmed 5,000-line fixture, the multi-project caller fixture, operation counters, and the 100-file memory fixture.
