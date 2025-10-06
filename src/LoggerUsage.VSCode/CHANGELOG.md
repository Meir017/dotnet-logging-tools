# Change Log

All notable changes to the "logger-usage" extension will be documented in this file.

Check [Keep a Changelog](http://keepachangelog.com/) for recommendations on how to structure this file.

## [1.0.0] - 2025-10-06

### Added

- **Initial Release** of Logger Usage VS Code Extension
- **Real-time Analysis** of Microsoft.Extensions.Logging patterns in C# projects
- **Insights Panel** with interactive table view and comprehensive filtering
- **Tree View** for navigating logging statements by solution/project/file hierarchy
- **Problems Panel Integration** showing logging inconsistencies as diagnostics
- **Multi-Pattern Support**:
  - `ILogger` extension methods (`logger.LogInformation(...)`)
  - `LoggerMessage` attribute with source generators
  - `LoggerMessage.Define` pattern
  - `ILogger.BeginScope` calls
- **Automatic Analysis** on file save with incremental updates
- **Manual Analysis** command with keyboard shortcut (`Ctrl+Shift+L`)
- **Export Functionality** to JSON, CSV, and Markdown formats
- **Advanced Filtering** by log level, method type, file path, and inconsistencies
- **Click-to-Navigate** directly to source code locations
- **Multi-Solution Support** with active solution selector
- **Configuration Options**:
  - Toggle auto-analyze on save
  - Exclude patterns for performance
  - Performance thresholds (max files, timeout)
  - Problems panel integration toggle
  - Default filter settings

### Technical Details

- Built with TypeScript 5.3+ in strict mode
- Uses .NET bridge process for Roslyn-based semantic analysis
- JSON-based IPC between TypeScript extension and .NET backend
- Progress reporting with cancellation support during analysis
- VS Code API 1.85+ compatibility
- .NET 10 runtime requirement
- 78 passing unit tests with Mocha framework

### Known Issues

- Integration tests are placeholders (44 tests currently skipped)
- Best-effort performance on very large solutions (>50,000 LOC)
- Single active solution at a time in multi-solution workspaces
- Bridge process requires .NET 10 runtime on system

### Development Status

- ✅ Core functionality complete and tested
- ✅ Unit tests passing (78 tests)
- ⏭️ Integration tests require real workspace fixtures (44 tests skipped)
- 📦 Ready for alpha/beta testing

---

## [Unreleased] - Future Plans

### Planned Features

- Complete integration test suite with real workspace fixtures
- Performance optimizations for large codebases (>100K LOC)
- Enhanced filtering with regular expression support
- Code lens for quick insights at logging call sites
- Refactoring suggestions for inconsistent patterns
- Configuration presets for common logging standards
- Concurrent multi-solution analysis
- Telemetry and diagnostics dashboard
- Custom rule definitions via configuration
- Batch refactoring tools for fixing inconsistencies
