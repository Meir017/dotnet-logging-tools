# Data Models: VS Code Logging Insights Extension

**Date**: 2025-10-06
**Feature**: VS Code extension for logging insights
**Purpose**: Define data structures for UI state, configuration, and IPC communication

---

## 1. TypeScript Models (Extension)

### 1.1 Logging Insight View Model

**Purpose**: Represents a single logging statement in the UI

```typescript
interface LoggingInsight {
  /** Unique identifier for this insight (file path + line + column) */
  id: string;

  /** The logging method type */
  methodType: 'LoggerExtension' | 'LoggerMessageAttribute' | 'LoggerMessageDefine' | 'BeginScope';

  /** Message template string */
  messageTemplate: string;

  /** Log level (e.g., Information, Warning, Error) */
  logLevel: string | null;

  /** Event ID information */
  eventId: {
    id: number | null;
    name: string | null;
  } | null;

  /** Parameter names extracted from message template or method signature */
  parameters: string[];

  /** File location */
  location: {
    filePath: string;
    startLine: number;
    startColumn: number;
    endLine: number;
    endColumn: number;
  };

  /** Tags/categories for filtering */
  tags: string[];

  /** Data classification information (for sensitive data detection) */
  dataClassifications: DataClassification[];

  /** Whether this insight has any inconsistencies */
  hasInconsistencies: boolean;

  /** Inconsistency details if applicable */
  inconsistencies?: ParameterInconsistency[];
}

interface DataClassification {
  parameterName: string;
  classificationType: string; // e.g., 'PersonalData', 'SensitiveData'
}

interface ParameterInconsistency {
  type: 'NameMismatch' | 'MissingEventId' | 'SensitiveDataInLog';
  message: string;
  severity: 'Warning' | 'Error';
  location?: {
    filePath: string;
    startLine: number;
    startColumn: number;
    endLine: number;
    endColumn: number;
  };
}
```

---

### 1.2 Filter State

**Purpose**: Tracks user-applied filters in the UI

```typescript
interface FilterState {
  /** Selected log levels (empty = all) */
  logLevels: string[];

  /** Selected method types (empty = all) */
  methodTypes: ('LoggerExtension' | 'LoggerMessageAttribute' | 'LoggerMessageDefine' | 'BeginScope')[];

  /** Search query for message templates */
  searchQuery: string;

  /** Show only entries with inconsistencies */
  showInconsistenciesOnly: boolean;

  /** Selected tags (empty = all) */
  tags: string[];

  /** Selected files/projects (empty = all) */
  filePaths: string[];
}
```

---

### 1.3 Extension Configuration

**Purpose**: User settings for the extension (maps to VS Code settings)

```typescript
interface ExtensionConfiguration {
  /** Automatically analyze on file save */
  autoAnalyzeOnSave: boolean;

  /** File patterns to exclude from analysis */
  excludePatterns: string[];

  /** Performance thresholds */
  performanceThresholds: {
    maxFilesPerAnalysis: number;
    analysisTimeoutMs: number;
  };

  /** Enable Problems panel integration */
  enableProblemsIntegration: boolean;

  /** Default filter settings */
  filterDefaults: {
    logLevels: string[];
    showInconsistenciesOnly: boolean;
  };
}
```

---

### 1.4 Analysis Request/Response (IPC)

**Purpose**: Communication between TypeScript extension and C# bridge

```typescript
/** Request to analyze a workspace/solution */
interface AnalysisRequest {
  command: 'analyze';
  workspacePath: string;
  solutionPath: string | null;
  excludePatterns?: string[];
}

/** Request to re-analyze a single file (incremental) */
interface IncrementalAnalysisRequest {
  command: 'analyzeFile';
  filePath: string;
  solutionPath: string;
}

/** Progress update from bridge */
interface AnalysisProgress {
  status: 'progress';
  percentage: number;
  message: string;
  currentFile?: string;
}

/** Successful analysis response */
interface AnalysisSuccessResponse {
  status: 'success';
  result: {
    insights: LoggingInsight[];
    summary: AnalysisSummary;
  };
}

/** Error response */
interface AnalysisErrorResponse {
  status: 'error';
  message: string;
  details: string;
  errorCode?: string;
}

/** Summary statistics */
interface AnalysisSummary {
  totalInsights: number;
  byMethodType: Record<string, number>;
  byLogLevel: Record<string, number>;
  inconsistenciesCount: number;
  filesAnalyzed: number;
  analysisTimeMs: number;
}

type AnalysisResponse = AnalysisSuccessResponse | AnalysisErrorResponse;
```

---

### 1.5 Webview Messages

**Purpose**: Communication between extension host and webview

```typescript
/** Messages sent from extension → webview */
type ExtensionToWebviewMessage =
  | { command: 'updateInsights'; insights: LoggingInsight[]; summary: AnalysisSummary }
  | { command: 'updateFilters'; filters: FilterState }
  | { command: 'showError'; message: string; details?: string }
  | { command: 'updateTheme'; theme: 'light' | 'dark' | 'high-contrast' };

/** Messages sent from webview → extension */
type WebviewToExtensionMessage =
  | { command: 'applyFilters'; filters: FilterState }
  | { command: 'navigateToInsight'; insightId: string }
  | { command: 'exportResults'; format: 'json' | 'csv' | 'markdown' }
  | { command: 'refreshAnalysis' };
```

---

### 1.6 Tree View Items

**Purpose**: Sidebar tree view data

```typescript
/** Tree item for solution/project navigation */
interface LoggerTreeItem {
  type: 'solution' | 'project' | 'file' | 'insight';
  label: string;
  description?: string;
  tooltip?: string;

  /** Associated insight if type === 'insight' */
  insight?: LoggingInsight;

  /** File path if type === 'file' */
  filePath?: string;

  /** Child items */
  children?: LoggerTreeItem[];

  /** Collapsible state */
  collapsibleState: 'none' | 'collapsed' | 'expanded';

  /** Icon name or path */
  iconPath?: string;

  /** Command to execute on click */
  command?: {
    command: string;
    title: string;
    arguments: any[];
  };
}
```

---

## 2. C# Models (Bridge Library)

### 2.1 Bridge Request/Response DTOs

**Purpose**: Strongly-typed C# equivalents of IPC messages

```csharp
// Request DTOs
public record AnalysisRequest(
    string Command,
    string WorkspacePath,
    string? SolutionPath,
    string[]? ExcludePatterns);

public record IncrementalAnalysisRequest(
    string Command,
    string FilePath,
    string SolutionPath);

// Response DTOs
public record AnalysisSuccessResponse(
    string Status,
    AnalysisResult Result);

public record AnalysisErrorResponse(
    string Status,
    string Message,
    string Details,
    string? ErrorCode = null);

public record AnalysisProgress(
    string Status,
    int Percentage,
    string Message,
    string? CurrentFile = null);
```

---

### 2.2 Analysis Result Models

**Purpose**: Structured results from LoggerUsage library

```csharp
public record AnalysisResult(
    List<LoggingInsightDto> Insights,
    AnalysisSummary Summary);

public record LoggingInsightDto(
    string Id,
    string MethodType,
    string MessageTemplate,
    string? LogLevel,
    EventIdDto? EventId,
    List<string> Parameters,
    LocationDto Location,
    List<string> Tags,
    List<DataClassificationDto> DataClassifications,
    bool HasInconsistencies,
    List<ParameterInconsistencyDto>? Inconsistencies);

public record EventIdDto(
    int? Id,
    string? Name);

public record LocationDto(
    string FilePath,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);

public record DataClassificationDto(
    string ParameterName,
    string ClassificationType);

public record ParameterInconsistencyDto(
    string Type,
    string Message,
    string Severity,
    LocationDto? Location);

public record AnalysisSummary(
    int TotalInsights,
    Dictionary<string, int> ByMethodType,
    Dictionary<string, int> ByLogLevel,
    int InconsistenciesCount,
    int FilesAnalyzed,
    long AnalysisTimeMs);
```

---

### 2.3 Mapping from LoggerUsage Library

**Purpose**: Map between LoggerUsage models and Bridge DTOs

```csharp
public static class LoggerUsageMapper
{
    public static LoggingInsightDto ToDto(this LoggerUsageInfo info, string filePath)
    {
        return new LoggingInsightDto(
            Id: $"{filePath}:{info.Location.StartLine}:{info.Location.StartColumn}",
            MethodType: info.MethodType.ToString(),
            MessageTemplate: info.MessageTemplate ?? string.Empty,
            LogLevel: info.LogLevel?.Name,
            EventId: info.EventId != null
                ? new EventIdDto(info.EventId.Id, info.EventId.Name)
                : null,
            Parameters: info.MessageParameters.Select(p => p.Name).ToList(),
            Location: new LocationDto(
                FilePath: filePath,
                StartLine: info.Location.StartLine,
                StartColumn: info.Location.StartColumn,
                EndLine: info.Location.EndLine,
                EndColumn: info.Location.EndColumn),
            Tags: info.Tags.ToList(),
            DataClassifications: info.DataClassifications
                .Select(dc => new DataClassificationDto(dc.ParameterName, dc.ClassificationType))
                .ToList(),
            HasInconsistencies: DetectInconsistencies(info, out var inconsistencies),
            Inconsistencies: inconsistencies);
    }

    private static bool DetectInconsistencies(
        LoggerUsageInfo info,
        out List<ParameterInconsistencyDto> inconsistencies)
    {
        inconsistencies = new List<ParameterInconsistencyDto>();

        // Check for parameter name inconsistencies
        // Check for missing EventId
        // Check for sensitive data classifications

        // Implementation details omitted for brevity

        return inconsistencies.Count > 0;
    }
}
```

---

## 3. VS Code Diagnostic Model

**Purpose**: Representation of problems for Problems panel

```typescript
interface LoggingDiagnostic {
  /** Source file URI */
  uri: vscode.Uri;

  /** Range in document */
  range: vscode.Range;

  /** Diagnostic severity */
  severity: vscode.DiagnosticSeverity; // Warning or Error

  /** Problem message */
  message: string;

  /** Diagnostic source */
  source: 'LoggerUsage';

  /** Optional code identifier */
  code?: string; // e.g., 'PARAM_INCONSISTENCY', 'MISSING_EVENT_ID'

  /** Related information (e.g., other occurrences) */
  relatedInformation?: vscode.DiagnosticRelatedInformation[];
}
```

---

## 4. Configuration Schema (package.json)

**Purpose**: VS Code extension settings definition

```json
{
  "configuration": {
    "title": "Logger Usage",
    "properties": {
      "loggerUsage.autoAnalyzeOnSave": {
        "type": "boolean",
        "default": true,
        "description": "Automatically analyze logging usage when C# files are saved"
      },
      "loggerUsage.excludePatterns": {
        "type": "array",
        "items": { "type": "string" },
        "default": ["**/obj/**", "**/bin/**"],
        "description": "Glob patterns for files to exclude from analysis"
      },
      "loggerUsage.performanceThresholds.maxFilesPerAnalysis": {
        "type": "number",
        "default": 1000,
        "description": "Maximum number of files to analyze in a single pass"
      },
      "loggerUsage.performanceThresholds.analysisTimeoutMs": {
        "type": "number",
        "default": 300000,
        "description": "Maximum time (ms) for analysis before timeout warning"
      },
      "loggerUsage.enableProblemsIntegration": {
        "type": "boolean",
        "default": true,
        "description": "Show logging inconsistencies in the Problems panel"
      },
      "loggerUsage.filterDefaults.logLevels": {
        "type": "array",
        "items": { "type": "string" },
        "default": ["Information", "Warning", "Error"],
        "description": "Default log levels to display"
      },
      "loggerUsage.filterDefaults.showInconsistenciesOnly": {
        "type": "boolean",
        "default": false,
        "description": "Show only logging statements with inconsistencies by default"
      }
    }
  }
}
```

---

## 5. Model Relationships

```text
┌─────────────────────────┐
│   VS Code Extension     │
│   (TypeScript)          │
│                         │
│  ┌──────────────────┐   │
│  │ FilterState      │   │
│  │ (User Input)     │   │
│  └────────┬─────────┘   │
│           │             │
│           ▼             │
│  ┌──────────────────┐   │
│  │ LoggingInsight[] │◄──┼─── JSON over stdio ───┐
│  │ (Displayed Data) │   │                        │
│  └────────┬─────────┘   │                        │
│           │             │                        │
│           ▼             │                        │
│  ┌──────────────────┐   │                        │
│  │ LoggingDiagnostic│   │                        │
│  │ (Problems Panel) │   │                        │
│  └──────────────────┘   │                        │
└─────────────────────────┘                        │
                                                   │
┌──────────────────────────────────────────────────┼────┐
│          C# Bridge Process                       │    │
│          (LoggerUsage.VSCode.Bridge)             │    │
│                                                  │    │
│  ┌──────────────────┐      ┌──────────────────┐ │    │
│  │ AnalysisRequest  │      │AnalysisResult    │─┼────┘
│  │ (stdin)          │      │ (stdout)         │ │
│  └────────┬─────────┘      └─────────▲────────┘ │
│           │                          │          │
│           ▼                          │          │
│  ┌────────────────────────────────────────────┐ │
│  │     LoggerUsageExtractor                   │ │
│  │     (from LoggerUsage library)             │ │
│  │                                            │ │
│  │  ┌────────────────┐   ┌─────────────────┐ │ │
│  │  │LoggerUsageInfo │   │ EventIdInfo     │ │ │
│  │  │(from library)  │   │ (from library)  │ │ │
│  │  └────────────────┘   └─────────────────┘ │ │
│  │                                            │ │
│  │           Mapped to DTOs via               │ │
│  │        LoggerUsageMapper                   │ │
│  └────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────┘
```

---

## 6. Data Flow Example

### Scenario: User opens solution with logging code

1. **Extension Activation**
   - Extension reads `ExtensionConfiguration` from VS Code settings
   - Creates `AnalysisRequest` with workspace/solution paths

2. **Bridge Invocation**
   - TypeScript spawns C# bridge process
   - Sends `AnalysisRequest` as JSON via stdin

3. **Bridge Processing**
   - Bridge deserializes `AnalysisRequest`
   - Invokes `LoggerUsageExtractor` from library
   - Gets back `LoggerUsageExtractionResult`
   - Maps to `AnalysisResult` DTOs
   - Sends `AnalysisProgress` updates periodically
   - Returns `AnalysisSuccessResponse` via stdout

4. **Extension Processing**
   - Extension receives `AnalysisResponse`
   - Converts to `LoggingInsight[]` models
   - Applies default `FilterState` from configuration
   - Updates TreeView with `LoggerTreeItem[]`
   - Sends `ExtensionToWebviewMessage` to insights panel
   - Creates `LoggingDiagnostic[]` and publishes to Problems panel

5. **User Interaction**
   - User modifies `FilterState` in webview
   - Webview sends `WebviewToExtensionMessage`
   - Extension re-filters `LoggingInsight[]` and updates views
   - No bridge re-invocation needed (client-side filtering)

---

## 7. Validation Rules

### LoggingInsight Validation

- `id` must be unique within analysis result
- `location.filePath` must be absolute path
- `location` line/column numbers must be ≥ 1
- `methodType` must be one of defined enum values
- `parameters` should match template placeholders (inconsistency flagged if not)

### AnalysisRequest Validation

- `workspacePath` must exist and be readable
- `solutionPath` (if provided) must be `.sln` file
- `excludePatterns` must be valid glob patterns

### FilterState Validation

- `logLevels` values should match known levels (Information, Warning, Error, etc.)
- `searchQuery` is free-text (no validation)
- `methodTypes` must be subset of defined enum

---

**Model Summary**:

| Model | Layer | Purpose | Serialization |
|-------|-------|---------|---------------|
| `LoggingInsight` | TypeScript | UI display | JSON |
| `FilterState` | TypeScript | User filters | In-memory |
| `ExtensionConfiguration` | TypeScript | Settings | VS Code settings API |
| `AnalysisRequest/Response` | Both | IPC | JSON over stdio |
| `LoggingInsightDto` | C# | Bridge output | JSON |
| `LoggerUsageInfo` | C# | Library output | N/A (internal) |
| `LoggingDiagnostic` | TypeScript | Problems panel | VS Code Diagnostic API |
| `LoggerTreeItem` | TypeScript | Tree view | VS Code TreeItem API |

---

**Ready for Phase 1**: Contracts definition (next step)
