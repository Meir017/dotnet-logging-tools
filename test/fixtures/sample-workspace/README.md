# Sample Workspace for Integration Tests

This workspace contains a simple C# console application with various logging patterns for testing the Logger Usage extension.

## Structure

- **SampleApp.sln** - Solution file
- **SampleApp/** - Console application project
  - **UserService.cs** - Contains multiple logging scenarios including inconsistencies
  - **OrderService.cs** - Uses LoggerMessage attribute (source generators)
  - **Program.cs** - Entry point that exercises the services

## Logging Patterns Included

### UserService.cs
- ✅ Correct logging with matching parameter names
- ⚠️ Missing EventId
- ❌ Parameter name mismatch (LU001)
- ❌ Multiple inconsistencies in error logging
- Uses `BeginScope`

### OrderService.cs
- Uses `[LoggerMessage]` attribute with EventIds
- Mix of source-generated and regular logging
- Demonstrates good practices

## Expected Analysis Results

The extension should detect:
- **Total Insights**: ~10-12 logging statements
- **Inconsistencies**: 
  - 2-3 parameter name mismatches (LU001)
  - 4-5 missing EventIds (LU002)
- **Method Types**:
  - LoggerExtension (ILogger methods)
  - LoggerMessage (attribute-based)
  - BeginScope

## Building

```bash
cd SampleApp
dotnet build
```

## Usage in Tests

This workspace is used by:
- `fullWorkflow.test.ts` - Complete E2E testing
- `incrementalAnalysis.test.ts` - File modification scenarios
- `errorHandling.test.ts` - Error recovery testing
