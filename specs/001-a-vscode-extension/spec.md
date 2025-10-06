# Feature Specification: VS Code Logging Insights Extension

**Feature Branch**: `001-a-vscode-extension`
**Created**: 2025-10-05
**Status**: Draft
**Input**: User description: "a vscode extension that provides logging insights when opening a C# project/solution"

## Execution Flow (main)
```
1. Parse user description from Input
   → If empty: ERROR "No feature description provided"
2. Extract key concepts from description
   → Identify: actors, actions, data, constraints
3. For each unclear aspect:
   → Mark with [NEEDS CLARIFICATION: specific question]
4. Fill User Scenarios & Testing section
   → If no clear user flow: ERROR "Cannot determine user scenarios"
5. Generate Functional Requirements
   → Each requirement must be testable
   → Mark ambiguous requirements
6. Identify Key Entities (if data involved)
7. Run Review Checklist
   → If any [NEEDS CLARIFICATION]: WARN "Spec has uncertainties"
   → If implementation details found: ERROR "Remove tech details"
8. Return: SUCCESS (spec ready for planning)
```

---

## Clarifications

### Session 2025-10-06
- Q: What is the acceptable maximum time for initial analysis of a typical C# project (10-50K lines of code)? → A: No hard limit - Best-effort acceptable (progress feedback required)
- Q: When multiple .sln files are present in a VS Code workspace, how should insights be displayed? → A: Active solution only - Show insights only for currently active/selected solution
- Q: Should users be able to filter/search within the insights panel? → A: Yes, full filtering - Filter by log level, file path, parameter name with text search
- Q: What configuration capabilities should the extension provide? → A: Comprehensive - Auto-analyze on save, file/folder exclusions, performance thresholds, manual commands
- Q: Should the extension integrate with VS Code's Problems panel and provide export functionality? → A: Problems only - Integrate with Problems panel, no export functionality

---

## ⚡ Quick Guidelines
- ✅ Focus on WHAT users need and WHY
- ❌ Avoid HOW to implement (no tech stack, APIs, code structure)
- 👥 Written for business stakeholders, not developers

### Section Requirements
- **Mandatory sections**: Must be completed for every feature
- **Optional sections**: Include only when relevant to the feature
- When a section doesn't apply, remove it entirely (don't leave as "N/A")

### For AI Generation
When creating this spec from a user prompt:
1. **Mark all ambiguities**: Use [NEEDS CLARIFICATION: specific question] for any assumption you'd need to make
2. **Don't guess**: If the prompt doesn't specify something (e.g., "login system" without auth method), mark it
3. **Think like a tester**: Every vague requirement should fail the "testable and unambiguous" checklist item
4. **Common underspecified areas**:
   - User types and permissions
   - Data retention/deletion policies
   - Performance targets and scale
   - Error handling behaviors
   - Integration requirements
   - Security/compliance needs

---

## User Scenarios & Testing *(mandatory)*

### Primary User Story
As a C# developer, I want to view logging insights directly in VS Code when I open a C# project or solution, so that I can understand logging patterns, identify inconsistencies, and improve logging quality without switching to external tools.

### Acceptance Scenarios
1. **Given** a C# project/solution is open in VS Code, **When** the workspace loads, **Then** the extension analyzes logging patterns and displays insights in a dedicated panel
2. **Given** logging insights are displayed, **When** I click on a log usage item, **Then** VS Code navigates to the corresponding source code location
3. **Given** the project contains logging inconsistencies, **When** insights are displayed, **Then** parameter name inconsistencies and pattern violations are highlighted
4. **Given** multiple C# projects in a workspace, **When** opening the workspace, **Then** insights aggregate data across all projects
5. **Given** source code changes are saved, **When** files containing logging calls are modified, **Then** insights update automatically to reflect the changes
6. **Given** a large codebase with many logging calls, **When** analysis runs, **Then** the extension provides progress feedback without a hard time limit (best-effort performance)

### Edge Cases
- What happens when the project has no logging calls? Display empty state with helpful message.
- What happens when Microsoft.Extensions.Logging is not referenced? Display informational message explaining extension requires this dependency.
- What happens when the project fails to compile? Display partial insights from successfully analyzed files with warning about compilation errors.
- What happens when opening a non-.NET workspace? Extension remains inactive with no errors displayed.
- What happens when multiple solutions are open simultaneously? Extension displays insights only for the currently active/selected solution.

## Requirements *(mandatory)*

### Functional Requirements

#### Core Analysis
- **FR-001**: Extension MUST detect when a C# project or solution is opened in VS Code
- **FR-002**: Extension MUST analyze all supported Microsoft.Extensions.Logging patterns (ILogger extensions, LoggerMessage attribute, LoggerMessage.Define, BeginScope)
- **FR-003**: Extension MUST extract logging metadata including: log level, event ID, message template, parameter names, file location, line number
- **FR-004**: Extension MUST analyze custom telemetry features (custom tag names, tag providers, data classification, transitive properties)
- **FR-005**: Extension MUST process projects incrementally - analyzing only changed files when source code is modified

#### User Interface
- **FR-006**: Extension MUST display insights in a dedicated VS Code panel/view
- **FR-007**: Extension MUST show summary statistics: total log usages, parameter counts, classification breakdown, inconsistency counts
- **FR-008**: Extension MUST list individual log usage details with file locations and message templates
- **FR-009**: Extension MUST highlight parameter name inconsistencies (same semantic parameter with different names)
- **FR-010**: Users MUST be able to click on log usage items to navigate to source code location
- **FR-011**: Extension MUST show most common parameter names across the codebase
- **FR-012**: Extension MUST display log level distribution (breakdown by Information, Warning, Error, etc.)
- **FR-013**: Extension MUST support filtering by log level, file path, and parameter name
- **FR-013a**: Extension MUST provide text search capability across all insight fields

#### Performance & Progress
- **FR-014**: Extension MUST provide visual progress feedback during analysis (progress bar or status indicator)
- **FR-015**: Extension MUST perform analysis in background without blocking VS Code UI
- **FR-016**: Extension MUST provide continuous progress feedback during analysis without imposing a hard time limit (best-effort performance with user visibility)
- **FR-017**: Extension MUST support cancellation of in-progress analysis

#### Error Handling
- **FR-018**: Extension MUST handle missing dependencies gracefully (display informative message when Microsoft.Extensions.Logging not found)
- **FR-019**: Extension MUST provide partial results when compilation errors exist
- **FR-020**: Extension MUST display actionable error messages when analysis fails
- **FR-021**: Extension MUST log diagnostic information for troubleshooting extension issues

#### Configuration
- **FR-022**: Users MUST be able to enable/disable auto-analyze on file save
- **FR-022a**: Users MUST be able to configure file/folder exclusion patterns for analysis
- **FR-022b**: Users MUST be able to configure performance thresholds and analysis limits
- **FR-023**: Extension MUST support both workspace-specific and user-level settings
- **FR-024**: Extension MUST provide manual commands for refresh and re-analysis

#### Integration
- **FR-025**: Extension MUST integrate with existing VS Code theme (light/dark mode)
- **FR-026**: Extension MUST work with multi-root workspaces containing multiple C# projects
- **FR-026a**: Extension MUST display insights only for the currently active/selected solution when multiple solutions are present
- **FR-027**: Extension MUST integrate with VS Code's Problems panel to display parameter inconsistencies and logging pattern violations as warnings

### Key Entities

- **Logging Insight**: Represents analyzed logging information including summary statistics, usage details, inconsistencies, and telemetry feature usage across the workspace
- **Log Usage**: Individual logging call with metadata (log level, event ID, message template, parameters, source location, method type)
- **Parameter Inconsistency**: Detected inconsistency where semantically equivalent parameters have different names across log calls
- **Summary Statistics**: Aggregated metrics including total usages, parameter counts, classification breakdown, log level distribution
- **Telemetry Feature**: Advanced logging features including custom tag names, tag providers, data classification annotations, and transitive properties
- **Source Location**: File path, line number, column number, and containing method/class for navigation

---

## Review & Acceptance Checklist
*GATE: Automated checks run during main() execution*

### Content Quality
- [ ] No implementation details (languages, frameworks, APIs)
- [ ] Focused on user value and business needs
- [ ] Written for non-technical stakeholders
- [ ] All mandatory sections completed

### Requirement Completeness
- [ ] No [NEEDS CLARIFICATION] markers remain
- [ ] Requirements are testable and unambiguous
- [ ] Success criteria are measurable
- [ ] Scope is clearly bounded
- [ ] Dependencies and assumptions identified

---

## Execution Status
*Updated by main() during processing*

- [x] User description parsed
- [x] Key concepts extracted (actors: C# developers; actions: analyze, display, navigate; data: logging patterns; constraints: performance, incremental updates)
- [x] Ambiguities marked (8 [NEEDS CLARIFICATION] markers for missing specifications)
- [x] User scenarios defined (primary story + 6 acceptance scenarios + 5 edge cases)
- [x] Requirements generated (28 functional requirements organized by category)
- [x] Entities identified (6 key entities with attributes and relationships)
- [x] Review checklist passed (spec ready with marked ambiguities for stakeholder clarification)

---

## Notes for Planning Phase

**Clarifications Completed (2025-10-06):**
1. ✅ Performance targets: Best-effort with progress feedback (no hard limits)
2. ✅ Multi-solution behavior: Active solution only
3. ✅ UI features: Full filtering and text search capabilities
4. ✅ Configuration options: Comprehensive settings (auto-analyze, exclusions, thresholds, commands)
5. ✅ Problems integration: Yes - display inconsistencies in Problems panel
6. ✅ Export functionality: Not required (out of scope)

**Dependencies on Existing Components:**
- Core LoggerUsage library for analysis engine
- LoggerUsage.MSBuild for workspace/solution loading
- LoggerUsageExtractor for extraction logic
- Report generation models for insight data structures

**Success Metrics:**
- Extension successfully analyzes projects with best-effort performance
- Users can navigate to source code from insights with single click
- Inconsistencies are clearly identified in both insights panel and Problems panel
- Extension remains responsive during analysis with visible progress feedback
- Zero crashes or unhandled errors during normal operation
- Comprehensive configuration options enable user customization

---
