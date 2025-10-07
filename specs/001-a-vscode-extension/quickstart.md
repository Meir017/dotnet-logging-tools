# Quickstart: Manual Testing Guide

**Feature**: VS Code Logging Insights Extension
**Purpose**: Step-by-step manual testing scenarios for functional requirements validation

---

## Prerequisites

Before testing, ensure:

1. **Development Environment**:
   - VS Code installed (version 1.85+)
   - .NET 10 SDK installed
   - Extension built and installed (or running in Extension Development Host)

2. **Test Workspaces**:
   - Sample C# solution with logging code
   - Multiple solutions in same workspace (for multi-solution testing)
   - Solution with compilation errors (for error handling testing)

3. **Test Data**:
   - C# files with various logging patterns:
     - `ILogger<T>` extension methods (`LogInformation`, `LogWarning`, etc.)
     - `LoggerMessage` attribute usage
     - `LoggerMessage.Define` usage
     - `BeginScope` calls
   - Files with parameter inconsistencies
   - Files with missing EventIds

---

## Test Scenario 1: Extension Activation

**Requirements**: FR-001 (Extension activates when workspace contains solution)

### Steps

1. Open VS Code
2. Open folder with `.sln` file: `File > Open Folder`
3. Wait for extension to activate

### Expected Results

✓ Extension icon appears in Activity Bar (if applicable)
✓ Status bar item shows solution name
✓ "Logger Usage" tree view appears in Explorer sidebar
✓ Automatic analysis starts (if `autoAnalyzeOnSave: true`)
✓ Progress notification shows analysis status

### Variations

- **No solution**: Open folder without `.sln` → Status bar shows "No solution detected"
- **Multiple solutions**: Workspace with 2+ `.sln` files → Extension selects first, allows switching

---

## Test Scenario 2: Full Workspace Analysis

**Requirements**: FR-002 (Analyze logging usage in solution)

### Steps

1. Activate extension (see Scenario 1)
2. Execute command: `Ctrl+Shift+L` or Command Palette → "Logger Usage: Analyze Workspace"
3. Observe progress notification
4. Wait for completion

### Expected Results

✓ Progress notification appears with cancel button
✓ Progress updates every few seconds
✓ Completion notification shows summary: "Analysis complete: X insights found"
✓ Tree view populates with results
✓ Insights panel opens automatically (if configured)

### Validation

- Open insights panel: Check insights table has data
- Open tree view: Expand solution → projects → files → see logging statements
- Open Problems panel: See any inconsistencies flagged

---

## Test Scenario 3: Insights Panel Display

**Requirements**: FR-003 (Display logging insights), FR-008 (Tree view), FR-009 (Details panel)

### Steps

1. Complete analysis (Scenario 2)
2. Execute command: "Logger Usage: Show Insights Panel"
3. Examine panel contents

### Expected Results

✓ Panel opens in editor area (Column 2)
✓ Header shows: "Logging Insights - {SolutionName}"
✓ Summary stats displayed: "X insights | Y inconsistencies | Z files"
✓ Insights table shows:
  - Method Type column
  - Message Template column
  - Log Level column
  - Event ID column
  - Location column (file:line)
  - Navigate button

### Validation

- Click insight row → Editor opens to logging statement location
- Click navigate button → Same behavior as row click
- Hover over row → Tooltip shows full message template
- Verify all analyzed logging statements are present

---

## Test Scenario 4: Filtering Insights

**Requirements**: FR-006 (Filter by log level), FR-007 (Search), FR-012 (Filter by method type)

### Steps

1. Open insights panel (Scenario 3)
2. **Filter by Log Level**:
   - Uncheck "Information" checkbox
   - Observe table updates
3. **Search**:
   - Type "user" in search box
   - Observe table filters to matching templates
4. **Filter by Method Type**:
   - Select "LoggerMessageAttribute" from dropdown
   - Observe only attribute-based logs shown
5. **Show Inconsistencies Only**:
   - Toggle "Show inconsistencies only" switch
   - Observe only flagged insights shown

### Expected Results

✓ Each filter action immediately updates table (no delay)
✓ Multiple filters combine (AND logic)
✓ Search is case-insensitive
✓ Search debounces (300ms after typing stops)
✓ Filter state persists when panel closed/reopened

### Validation

- Verify row count matches expected filtered results
- Clear filters → All insights reappear

---

## Test Scenario 5: Incremental Analysis (File Save)

**Requirements**: FR-005 (Process projects incrementally)

### Steps

1. Complete initial analysis (Scenario 2)
2. Open a C# file with logging code
3. Modify a logging statement (change message template)
4. Save file: `Ctrl+S`
5. Observe behavior

### Expected Results

✓ Brief progress notification: "Analyzing file..."
✓ Analysis completes within 2 seconds
✓ Insights panel updates with changes
✓ Tree view refreshes for that file
✓ Problems panel updates if inconsistencies changed

### Validation

- Check insights panel for updated message template
- Verify other files' insights unchanged

---

## Test Scenario 6: Tree View Navigation

**Requirements**: FR-008 (Tree view with hierarchical grouping)

### Steps

1. Complete analysis (Scenario 2)
2. Open "Logger Usage" tree view in Explorer
3. Expand nodes: Solution → Project → File → Insights
4. Click an insight node

### Expected Results

✓ Tree structure:
  ```
  MyApp.sln
    ├─ MyApp.Core
    │  ├─ Services/LoggingService.cs (5)
    │  │  ├─ Line 42: User {userId} logged in
    │  │  ├─ Line 58: Error processing {orderId}
    │  │  └─ ...
    │  └─ ...
    └─ ...
  ```
✓ File nodes show insight count: "LoggingService.cs (5)"
✓ Insight nodes show preview: "Line 42: User {userId}..."
✓ Clicking insight opens file at exact location

### Validation

- Right-click file node → Context menu shows "Export to CSV"
- Refresh button in tree view toolbar → Tree reloads

---

## Test Scenario 7: Problems Panel Integration

**Requirements**: FR-027 (Display inconsistencies in Problems panel)

### Steps

1. Prepare test file with inconsistencies:
   ```csharp
   logger.LogInformation("User {userId} logged in", user); // Parameter name mismatch
   ```
2. Run analysis
3. Open Problems panel: `View > Problems`

### Expected Results

✓ Warning appears in Problems panel:
  - Source: "LoggerUsage"
  - Message: "Parameter name 'user' does not match template placeholder '{userId}'"
  - File: Correct file path
  - Line: Correct line number
✓ Editor shows squiggly underline at parameter location
✓ Hover tooltip shows diagnostic message

### Validation

- Click diagnostic in Problems panel → Editor navigates to location
- Fix inconsistency, save file → Diagnostic disappears

---

## Test Scenario 8: Export Insights

**Requirements**: FR-010 (Generate reports in multiple formats)

### Steps

1. Complete analysis (Scenario 2)
2. Execute command: "Logger Usage: Export Insights"
3. Select format: "Markdown"
4. Choose save location: `C:\Temp\logging-insights.md`
5. Confirm save

### Expected Results

✓ Save dialog opens with default filename: `logging-insights-{date}.md`
✓ File created at selected location
✓ Success notification: "Insights exported to {file}" with "Open File" button
✓ Click "Open File" → File opens in VS Code

### Validation

- Open exported file → Verify markdown table structure
- Verify all insights present in export
- Repeat with JSON and CSV formats → Verify correct serialization

---

## Test Scenario 9: Solution Selection

**Requirements**: FR-004 (Support multi-solution workspaces)

### Steps

1. Open workspace with multiple `.sln` files
2. Execute command: "Logger Usage: Select Solution"
3. Choose solution from quick pick
4. Observe behavior

### Expected Results

✓ Quick pick shows all detected solutions:
  - "MyApp.sln (D:\\Projects\\MyApp)"
  - "OtherApp.sln (D:\\Projects\\OtherApp)"
  - "Browse for solution file..."
✓ Select solution → Status bar updates with new solution name
✓ Analysis automatically triggers (if enabled)
✓ Insights panel and tree view refresh

### Validation

- Select different solution → Insights change accordingly
- Select "Browse" → File picker opens, can select `.sln` file outside workspace

---

## Test Scenario 10: Configuration Changes

**Requirements**: FR-011 (Configuration options)

### Steps

1. Open Settings: `File > Preferences > Settings`
2. Search: "Logger Usage"
3. Modify settings:
   - Uncheck `Auto Analyze On Save`
   - Add pattern to `Exclude Patterns`: `**/Tests/**`
   - Uncheck `Enable Problems Integration`
4. Save settings
5. Run analysis

### Expected Results

✓ Auto-analyze disabled: Save C# file → No automatic analysis
✓ Exclude patterns applied: Test files not analyzed
✓ Problems integration disabled: No diagnostics in Problems panel

### Validation

- Re-enable settings → Behavior reverts
- Verify exclude patterns apply glob matching correctly

---

## Test Scenario 11: Theme Integration

**Requirements**: FR-014 (Theme integration)

### Steps

1. Open insights panel (Scenario 3)
2. Change VS Code theme: `File > Preferences > Color Theme`
3. Select "Dark+" theme
4. Observe panel
5. Change to "Light+" theme
6. Observe panel

### Expected Results

✓ Panel colors update automatically
✓ Text remains readable (contrast preserved)
✓ Table borders, buttons, inputs match theme
✓ No manual refresh needed

### Validation

- Try high-contrast theme → Panel uses high-contrast styles
- Verify tree view icons also update with theme

---

## Test Scenario 12: Error Handling

**Requirements**: FR-026 (Error handling and recovery)

### Steps

1. **Invalid Solution**:
   - Corrupt `.sln` file
   - Run analysis
   - Expected: Error notification with "Select different solution" action

2. **Compilation Errors**:
   - C# code with syntax errors
   - Run analysis
   - Expected: Warning notification, partial results shown

3. **Bridge Crash**:
   - Manually kill bridge process during analysis
   - Expected: Error notification, "Retry" button available

4. **Timeout**:
   - Very large solution (simulate long analysis)
   - Expected: Progress continues, no hard timeout, cancel available

### Expected Results

✓ All errors handled gracefully (no extension crashes)
✓ User-friendly error messages
✓ Recovery actions available
✓ Partial results displayed when possible

---

## Test Scenario 13: Performance

**Requirements**: FR-013 (Performance optimization)

### Steps

1. **Small Solution** (< 100 files):
   - Run analysis
   - Measure time: < 5 seconds

2. **Medium Solution** (100-500 files):
   - Run analysis
   - Measure time: < 30 seconds

3. **Large Solution** (500+ files):
   - Run analysis
   - Verify progress updates every few seconds
   - Measure time: < 2 minutes (best-effort)

### Expected Results

✓ Analysis completes within target times
✓ UI remains responsive during analysis
✓ Memory usage < 1 GB for typical solutions
✓ Incremental file analysis < 2 seconds

### Validation

- Open Task Manager/Activity Monitor → Verify memory usage
- Try interacting with VS Code during analysis → No freezing

---

## Test Scenario 14: Accessibility

**Requirements**: General accessibility compliance

### Steps

1. Enable screen reader (e.g., NVDA, VoiceOver)
2. Open insights panel
3. Navigate using keyboard only:
   - Tab through filters
   - Arrow keys through table
   - Enter to navigate to insight

### Expected Results

✓ All controls accessible via keyboard
✓ Screen reader announces table contents
✓ Focus indicators visible
✓ ARIA labels present on controls

---

## Test Scenario 15: Cancellation

**Requirements**: User can cancel long-running operations

### Steps

1. Start analysis on large solution
2. Click "Cancel" in progress notification
3. Observe behavior

### Expected Results

✓ Analysis stops gracefully
✓ Partial results displayed (insights analyzed before cancellation)
✓ Status bar shows "Analysis cancelled"
✓ Can re-run analysis immediately

---

## Edge Cases

### Edge Case 1: Empty Workspace

**Setup**: Open folder with no C# code

**Expected**: Extension activates but shows "No solution detected", analysis not available

---

### Edge Case 2: No Logging Code

**Setup**: C# solution with no logging statements

**Expected**: Analysis completes, shows "0 insights found"

---

### Edge Case 3: Workspace Reload

**Setup**: Reload VS Code window during active analysis

**Expected**: Extension re-activates cleanly, previous results lost, can re-run analysis

---

### Edge Case 4: Concurrent File Saves

**Setup**: Save multiple C# files rapidly (e.g., via refactoring tool)

**Expected**: Extension queues incremental analyses, processes sequentially, no crashes

---

### Edge Case 5: Very Long Message Template

**Setup**: Logging statement with 500+ character message template

**Expected**: Insights panel shows truncated template with "..." and tooltip shows full text

---

## Regression Testing Checklist

After any code changes, verify:

- [ ] Extension activates without errors
- [ ] Analysis completes successfully
- [ ] Insights panel displays results
- [ ] Tree view populates correctly
- [ ] Problems panel shows diagnostics
- [ ] Filtering works as expected
- [ ] Export generates valid files
- [ ] Incremental analysis on file save works
- [ ] Theme changes apply to panel
- [ ] No memory leaks (long-running session)
- [ ] Configuration changes take effect
- [ ] Error scenarios handled gracefully

---

## Automated Testing Notes

**Manual testing above supplements automated tests**. Automated tests cover:

- Unit tests for models, utilities, mappers
- Integration tests for bridge communication
- E2E tests for command execution and webview rendering

**Manual testing focuses on**:

- Visual appearance and UX
- Theme integration
- Performance perception
- Accessibility
- Error recovery workflows

---

## Test Data Generator

**Script**: `test-sample/generate-test-solution.ps1`

Generates C# solution with various logging patterns for testing:

```powershell
.\generate-test-solution.ps1 -ProjectCount 3 -FilesPerProject 50
```

Creates solution with:

- Mix of logging methods (extensions, attributes, define)
- Intentional inconsistencies (parameter mismatches, missing EventIds)
- Edge cases (long templates, sensitive data annotations)

---

## Reporting Issues

When manual testing finds issues:

1. **Reproduce**: Confirm issue is reproducible
2. **Document**: Note steps, expected vs actual behavior
3. **Collect logs**: Open "Logger Usage" output channel, copy logs
4. **Screenshot**: Capture relevant UI state
5. **Create GitHub issue**: Include all above information

---

**Testing Responsibility**: QA team + developers (before PR merge)
**Frequency**: Before each release, after major changes
**Duration**: Full manual test pass ~2 hours
