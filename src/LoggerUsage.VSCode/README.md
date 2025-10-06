# Logger Usage - VS Code Extension

Analyze logging patterns in C# projects using Microsoft.Extensions.Logging.

## Features

- **Real-time Analysis**: Automatically analyze logging statements when opening C# solutions
- **Insights Panel**: View all logging calls with filtering and search capabilities
- **Problems Integration**: See parameter inconsistencies directly in the Problems panel
- **Tree View**: Navigate logging statements by solution, project, and file
- **Incremental Updates**: Re-analyze only changed files on save for fast feedback
- **Comprehensive Filtering**: Filter by log level, method type, file path, and more
- **Navigation**: Click to navigate directly to logging statement locations

## Installation

Install from the VS Code Marketplace or build from source.

## Usage

1. Open a workspace containing a C# solution (.sln) or project (.csproj)
2. The extension activates automatically and analyzes logging usage
3. View insights in the "Logger Usage" panel or tree view
4. Use the command palette (`Ctrl+Shift+P`) for additional commands:
   - **Logger Usage: Analyze Workspace** - Re-run analysis
   - **Logger Usage: Show Insights Panel** - Open the insights webview
   - **Logger Usage: Select Active Solution** - Choose which solution to analyze

## Configuration

Configure the extension via VS Code settings (`File > Preferences > Settings`):

### `loggerUsage.autoAnalyzeOnSave`
- Type: `boolean`
- Default: `true`
- Automatically analyze logging usage when C# files are saved

### `loggerUsage.excludePatterns`
- Type: `string[]`
- Default: `["**/obj/**", "**/bin/**"]`
- Glob patterns for files to exclude from analysis

### `loggerUsage.performanceThresholds.maxFilesPerAnalysis`
- Type: `number`
- Default: `1000`
- Maximum number of files to analyze in a single pass

### `loggerUsage.performanceThresholds.analysisTimeoutMs`
- Type: `number`
- Default: `300000`
- Maximum time (ms) for analysis before timeout warning (best-effort)

### `loggerUsage.enableProblemsIntegration`
- Type: `boolean`
- Default: `true`
- Show logging inconsistencies in the Problems panel

### `loggerUsage.filterDefaults.logLevels`
- Type: `string[]`
- Default: `["Information", "Warning", "Error"]`
- Default log levels to display

### `loggerUsage.filterDefaults.showInconsistenciesOnly`
- Type: `boolean`
- Default: `false`
- Show only logging statements with inconsistencies by default

## Supported Logging Patterns

- `ILogger` extension methods (`logger.LogInformation(...)`)
- `LoggerMessage` attribute (source generators)
- `LoggerMessage.Define` pattern
- `ILogger.BeginScope` calls

## Detected Inconsistencies

- **Parameter Name Mismatches**: Template placeholders don't match parameter names
- **Missing Event IDs**: Log calls without explicit Event IDs
- **Sensitive Data**: Parameters marked with data classification attributes

## Requirements

- VS Code 1.85 or higher
- C# workspace with .NET projects
- .NET 10 runtime (bundled with extension)

## Troubleshooting

### Extension doesn't activate
- Ensure workspace contains a `.sln` or `.csproj` file
- Check Output panel (View > Output > Logger Usage) for errors

### Analysis takes too long
- Increase `loggerUsage.performanceThresholds.analysisTimeoutMs`
- Add patterns to `loggerUsage.excludePatterns` to skip unnecessary files
- Disable `loggerUsage.autoAnalyzeOnSave` for large solutions

### No insights shown
- Verify project compiles successfully
- Check that logging statements use `Microsoft.Extensions.Logging`
- Run "Logger Usage: Analyze Workspace" command manually

## Known Limitations

- Analyzes only one solution at a time in multi-solution workspaces
- Best-effort performance for very large solutions (>50K LOC)
- Requires successful compilation for accurate analysis

## Contributing

Contributions welcome! See the [repository](https://github.com/Meir017/dotnet-logging-usage) for details.

## License

MIT License - see LICENSE file for details
