# Change Log

All notable changes to the "logger-usage" extension will be documented in this file.

Check [Keep a Changelog](http://keepachangelog.com/) for recommendations on how to structure this file.

## [1.0.0] - 2025-10-07

### Added

- **Initial Release** of Logger Usage VS Code Extension 🎉
- **Real-time Analysis** of Microsoft.Extensions.Logging patterns in C# projects using Roslyn
- **Insights Panel** with interactive table view, search, and comprehensive filtering capabilities
- **Tree View** for navigating logging statements by solution → project → file hierarchy
- **Problems Panel Integration** showing logging inconsistencies as diagnostics (LU001-LU003)
- **Multi-Pattern Support**:
  - `ILogger` extension methods (e.g., `logger.LogInformation(...)`)
  - `LoggerMessage` attribute with source generators (.NET 6+)
  - `LoggerMessage.Define<T>` pattern
  - `ILogger.BeginScope(...)` calls
- **Automatic Analysis** on file save with smart incremental updates (only changed files)
- **Manual Analysis** command with keyboard shortcut (`Ctrl+Shift+L` / `Cmd+Shift+L`)
- **Export Functionality** to JSON, CSV, and Markdown formats for reporting
- **Advanced Filtering**:
  - By log level (Trace, Debug, Information, Warning, Error, Critical)
  - By method type (LoggerExtension, LoggerMessage, LoggerMessageDefine, BeginScope)
  - By file path and message template (search)
  - Show only inconsistencies toggle
- **Click-to-Navigate** directly to source code locations from insights or tree view
- **Multi-Solution Support** with active solution selector for complex workspaces
- **Extension Icon** with automated SVG→PNG conversion using Sharp
- **Comprehensive Configuration**:
  - `autoAnalyzeOnSave` - Toggle automatic analysis (default: true)
  - `excludePatterns` - Glob patterns to exclude files (default: `**/obj/**`, `**/bin/**`)
  - `performanceThresholds.maxFilesPerAnalysis` - Max files per run (default: 1000)
  - `performanceThresholds.analysisTimeoutMs` - Analysis timeout (default: 300000ms)
  - `enableProblemsIntegration` - Show in Problems panel (default: true)
  - `filterDefaults.logLevels` - Default visible log levels
  - `filterDefaults.showInconsistenciesOnly` - Start with inconsistencies filtered
- **Inconsistency Detection**:
  - **LU001**: Parameter name mismatches (template vs. actual parameters)
  - **LU002**: Missing Event IDs for better log searchability
  - **LU003**: Sensitive data warnings (PersonalData, SensitiveData attributes)
- **Status Bar Integration** showing active solution and analysis progress

### Technical Details

- **Language**: TypeScript 5.3+ with strict mode enabled
- **Architecture**: VS Code extension with .NET bridge process for semantic analysis
- **Bridge**: .NET 10 console app using LoggerUsage library + MSBuildWorkspace
- **Communication**: JSON Lines protocol over stdio (IPC)
- **Features**: Progress reporting, cancellation support, incremental analysis
- **VS Code API**: 1.85+ compatibility
- **Testing**: 78 passing unit tests with Mocha + @vscode/test-electron
- **CI/CD**: GitHub Actions workflow for multi-platform testing (Ubuntu, Windows, macOS)
- **Packaging**: VSIX with bundled .NET 10 runtime (no user installation required)

### Documentation

- **README**: Comprehensive documentation with installation, usage, configuration, troubleshooting
- **Code Examples**: Inconsistency detection examples with ✅ good / ❌ bad patterns
- **Keyboard Shortcuts**: Documented all commands with shortcuts
- **Troubleshooting Guide**: Common issues and solutions

### Known Limitations

- **Multi-Solution**: Analyzes one solution at a time (switch with "Select Active Solution")
- **Performance**: Best-effort for solutions >50,000 lines of code
- **Compilation**: Requires successful compilation for accurate semantic analysis
- **Integration Tests**: 44 tests marked as placeholders (suite.skip) - require real workspace fixtures
- **Generated Code**: May include source-generated logging in results

### Development Status

- ✅ Core functionality complete and production-ready
- ✅ Unit tests passing (78 tests, 52 pending workspace-dependent)
- ✅ Cross-platform CI/CD (Ubuntu, Windows, macOS)
- ✅ Icon generation automated (SVG→PNG with Sharp)
- ✅ Comprehensive documentation
- ⏭️ Integration tests deferred (require workspace fixtures)
- 📦 Ready for production use and marketplace publishing

### Breaking Changes

None (initial release)

---

## [Unreleased] - Future Enhancements

### Planned Features

- **Integration Test Suite**: Complete E2E tests with real C# project fixtures
- **Performance**: Optimizations for codebases >100K LOC
- **Advanced Filtering**: Regular expression support for message templates
- **Code Lens**: Inline insights at logging call sites
- **Refactoring**: Automated fixes for inconsistencies
- **Configuration Presets**: Templates for common logging standards (Serilog, OpenTelemetry, etc.)
- **Multi-Solution**: Concurrent analysis of multiple solutions
- **Dashboard**: Telemetry and diagnostics overview panel
- **Custom Rules**: User-defined inconsistency detection rules
- **Batch Refactoring**: Fix multiple inconsistencies at once
- **AI-Powered**: Suggest better log messages and levels

### Under Consideration

- Support for additional logging frameworks (Serilog, NLog, log4net)
- Log message internationalization (i18n) analysis
- Performance impact analysis (high-frequency logging detection)
- Log aggregation integration (export to Elasticsearch, Azure App Insights, etc.)
- Team collaboration features (shared logging standards, review workflows)

---

**Feedback**: Please file issues and feature requests at [github.com/Meir017/dotnet-logging-usage/issues](https://github.com/Meir017/dotnet-logging-usage/issues)
