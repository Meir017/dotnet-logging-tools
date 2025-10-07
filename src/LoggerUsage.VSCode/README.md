# Logger Usage - VS Code Extension

> **Analyze logging patterns in C# projects using Microsoft.Extensions.Logging**

Get real-time insights into your application's logging statements, detect inconsistencies, and maintain consistent logging practices across your codebase.

## ✨ Features

- **🔍 Real-time Analysis**: Automatically analyze logging statements when opening C# solutions
- **📊 Insights Panel**: Interactive table view with filtering, search, and export capabilities
- **⚠️ Problems Integration**: See parameter inconsistencies directly in VS Code's Problems panel
- **🌳 Tree View**: Navigate logging statements hierarchically by solution → project → file
- **⚡ Incremental Updates**: Re-analyze only changed files on save for instant feedback
- **🎯 Smart Filtering**: Filter by log level, method type, message template, and inconsistencies
- **🔗 Quick Navigation**: Click any insight to jump directly to the code location
- **📤 Export**: Export insights to JSON, CSV, or Markdown formats

## 📦 Installation

### From VS Code Marketplace (Recommended)
1. Open VS Code
2. Press `Ctrl+Shift+X` (or `Cmd+Shift+X` on macOS) to open Extensions
3. Search for "Logger Usage"
4. Click **Install**

### From VSIX File
1. Download the `.vsix` file from [GitHub Releases](https://github.com/Meir017/dotnet-logging-usage/releases)
2. Open VS Code
3. Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on macOS)
4. Type "Install from VSIX" and select the downloaded file

### From Source
```bash
git clone https://github.com/Meir017/dotnet-logging-usage.git
cd dotnet-logging-usage/src/LoggerUsage.VSCode
npm install
npm run compile
npm run package
# Install the generated .vsix file
```

## 🚀 Quick Start

1. **Open a C# Workspace**: Open a folder containing a `.sln` or `.csproj` file
2. **Automatic Activation**: The extension activates and analyzes your logging statements
3. **View Insights**:
   - **Tree View**: Check the "Logger Usage" view in the Explorer sidebar
   - **Insights Panel**: Run "Logger Usage: Show Insights Panel" from the Command Palette
   - **Problems Panel**: View inconsistencies in the Problems panel (`Ctrl+Shift+M`)

## 📖 Usage

### Commands

Access commands via the Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`):

| Command | Description | Keyboard Shortcut |
|---------|-------------|-------------------|
| **Logger Usage: Analyze Workspace** | Manually trigger full workspace analysis | `Ctrl+Shift+L` / `Cmd+Shift+L` |
| **Logger Usage: Show Insights Panel** | Open the interactive insights webview | - |
| **Logger Usage: Select Active Solution** | Choose which solution to analyze (multi-solution workspaces) | - |
| **Logger Usage: Export Insights** | Export current insights to JSON/CSV/Markdown | - |
| **Logger Usage: Clear All Filters** | Reset all filters to defaults | - |
| **Logger Usage: Refresh** | Refresh the tree view | - |

### Tree View Navigation

1. Open the **Explorer** sidebar (`Ctrl+Shift+E`)
2. Locate the **"Logger Usage"** section
3. Expand nodes to navigate: **Solution → Projects → Files → Insights**
4. Click any insight to navigate to its location in code
5. Files show insight counts (e.g., `MyService.cs (5)`)
6. Insights with inconsistencies display a warning icon ⚠️

### Insights Panel

The insights panel provides an interactive table with powerful filtering:

1. **Search**: Type in the search box to filter by message template or file path
2. **Log Level Filter**: Check/uncheck log levels (Trace, Debug, Information, Warning, Error, Critical)
3. **Method Type Filter**: Select specific logging patterns (LoggerExtension, LoggerMessage, etc.)
4. **Inconsistencies Toggle**: Show only insights with detected issues
5. **Click to Navigate**: Click any table row to open the file at that logging statement
6. **Export**: Choose format (JSON, CSV, Markdown) and save location

### Automatic Analysis

By default, the extension automatically analyzes logging when:
- Opening a workspace with C# projects
- Saving a C# file (incremental analysis)
- Switching active solution (multi-solution workspaces)

Disable auto-analysis with: `"loggerUsage.autoAnalyzeOnSave": false`

## ⚙️ Configuration

Configure via VS Code settings: `File > Preferences > Settings > Extensions > Logger Usage`

### General Settings

## ⚙️ Configuration

Configure via VS Code settings: `File > Preferences > Settings > Extensions > Logger Usage`

### General Settings

#### `loggerUsage.autoAnalyzeOnSave`

- **Type**: `boolean`
- **Default**: `true`
- **Description**: Automatically analyze logging usage when C# files are saved

#### `loggerUsage.excludePatterns`

- **Type**: `string[]`
- **Default**: `["**/obj/**", "**/bin/**"]`
- **Description**: Glob patterns for files to exclude from analysis

#### `loggerUsage.enableProblemsIntegration`

- **Type**: `boolean`
- **Default**: `true`
- **Description**: Show logging inconsistencies in the Problems panel

### Performance Settings

#### `loggerUsage.performanceThresholds.maxFilesPerAnalysis`

- **Type**: `number`
- **Default**: `1000`
- **Description**: Maximum number of files to analyze in a single pass

#### `loggerUsage.performanceThresholds.analysisTimeoutMs`

- **Type**: `number`
- **Default**: `300000` (5 minutes)
- **Description**: Maximum time (ms) for analysis before timeout warning (best-effort)

### Filter Defaults

#### `loggerUsage.filterDefaults.logLevels`

- **Type**: `string[]`
- **Default**: `["Information", "Warning", "Error"]`
- **Description**: Default log levels to display in the insights panel

#### `loggerUsage.filterDefaults.showInconsistenciesOnly`

- **Type**: `boolean`
- **Default**: `false`
- **Description**: Show only logging statements with inconsistencies by default

## 🔍 Supported Logging Patterns

The extension analyzes the following `Microsoft.Extensions.Logging` patterns:

- **ILogger Extension Methods**: `logger.LogInformation("Message")`, `logger.LogError("Error: {Error}", ex)`
- **LoggerMessage Attribute**: `[LoggerMessage(...)]` with source generators (.NET 6+)
- **LoggerMessage.Define**: `LoggerMessage.Define<T1, T2>(...)` pattern
- **BeginScope**: `using (logger.BeginScope("Scope {Id}", id)) { ... }`

## ⚠️ Detected Inconsistencies

The extension detects and reports the following issues in the Problems panel:

### LU001: Parameter Name Mismatch

Template placeholders don't match parameter names:

```csharp
// ❌ Bad - placeholder {userId} doesn't match parameter name
logger.LogInformation("User {userId} logged in", username);

// ✅ Good - names match
logger.LogInformation("User {username} logged in", username);
```

### LU002: Missing Event ID

Log calls without explicit Event IDs:

```csharp
// ⚠️ Warning - no EventId specified
logger.LogInformation("Operation completed");

// ✅ Better - explicit EventId improves log searchability
logger.LogInformation(new EventId(1001, "OperationComplete"), "Operation completed");
```

### LU003: Sensitive Data Warning

Parameters marked with data classification attributes (e.g., `[PersonalData]`, `[SensitiveData]`):

```csharp
// ⚠️ Warning - logging potentially sensitive data
logger.LogInformation("User email: {Email}", user.Email); // where Email is marked [PersonalData]
```

## 📋 Requirements

- **VS Code**: Version 1.85 or higher
- **Workspace**: C# solution (`.sln`) or project (`.csproj`)
- **.NET Runtime**: .NET 10 (bundled with extension - no installation required)
- **Project Compatibility**: Any .NET project using `Microsoft.Extensions.Logging`

## 🐛 Troubleshooting

### Extension doesn't activate

- Ensure workspace contains a `.sln` or `.csproj` file in the root or subfolders
- Check **Output** panel: `View > Output > Logger Usage` for error messages
- Try reloading VS Code: `Developer: Reload Window` from Command Palette

### Analysis takes too long

- Increase timeout: `"loggerUsage.performanceThresholds.analysisTimeoutMs": 600000`
- Add exclusion patterns: `"loggerUsage.excludePatterns": ["**/obj/**", "**/bin/**", "**/packages/**"]`
- Disable auto-analysis: `"loggerUsage.autoAnalyzeOnSave": false` (run manually instead)
- For very large solutions (>500 files), consider analyzing specific projects

### No insights shown

- Verify your project compiles successfully (`dotnet build`)
- Ensure you're using `Microsoft.Extensions.Logging` (not `log4net`, `NLog`, etc.)
- Check that logging statements are in `.cs` files (not generated code)
- Run **"Logger Usage: Analyze Workspace"** manually from Command Palette
- Check **Problems** panel for compilation errors

### Insights Panel is blank

- Wait for analysis to complete (check status bar for progress)
- Try refreshing: Click the refresh button in the Insights Panel
- Check if filters are too restrictive (click **Clear All Filters**)
- Verify insights exist in the Tree View

### Performance issues

- The extension uses incremental analysis (only re-analyzes changed files)
- For large solutions, initial analysis may take 30-60 seconds
- Subsequent analyses after file saves are typically <2 seconds
- Consider excluding test projects or generated code via `excludePatterns`

## ⚡ Known Limitations

- **Multi-Solution Workspaces**: Analyzes one solution at a time (use "Select Active Solution" to switch)
- **Large Solutions**: Best-effort performance for solutions >50,000 lines of code
- **Compilation Required**: Requires successful compilation for accurate semantic analysis
- **Generated Code**: May include source-generated logging code in results
- **Cross-Project References**: Analyzes projects within the selected solution only

## 🤝 Contributing

Contributions are welcome! Please see the [main repository](https://github.com/Meir017/dotnet-logging-usage) for:

- Filing issues and feature requests
- Submitting pull requests
- Development setup instructions
- Architecture documentation

## 📄 License

MIT License - See [LICENSE](../../LICENSE) file for details.

## 🔗 Related

- **LoggerUsage CLI**: Command-line tool for CI/CD pipelines
- **LoggerUsage.Mcp**: Model Context Protocol server for AI integrations
- **Main Repository**: [Meir017/dotnet-logging-usage](https://github.com/Meir017/dotnet-logging-usage)

---

**Made with ❤️ by the .NET logging community**
