# Code Coverage Improvement Plan

## Coverage Analysis Summary

Based on the analysis of three coverage reports (LoggerUsage.Cli.Tests, LoggerUsage.Mcp.Tests, and LoggerUsage.Tests), the following areas need significant test coverage improvements:

### Overall Coverage Status

- **Total Line Coverage**: ~62% (1198/1917 lines covered in CLI tests)
- **Total Branch Coverage**: ~46% (316/682 branches covered in CLI tests)
- **Main Test Project**: ~64% line coverage, ~70% branch coverage

## Critical Low-Coverage Areas (Priority 1)

### 1. KeyValuePairExtractionService (2.4% line coverage)

**Current Coverage**: 2.4% line coverage, 0% branch coverage  
**Location**: `src/LoggerUsage/Services/KeyValuePairExtractionService.cs`

**Missing Test Coverage**:

- `TryExtractParameters()` method - Core functionality
- `TryExtractFromObjectCreation()` - Object creation handling
- `TryExtractFromArrayCreation()` - Array creation handling
- `TryExtractFromLocalReference()` - Local variable references
- `TryExtractFromFieldReference()` - Field references
- `ExtractFromCollectionInitializer()` - Collection initialization
- `ExtractFromArrayInitializer()` - Array initialization
- `ExtractKeyValuePairFromObjectCreation()` - KeyValuePair construction
- `ExtractFromInvocation()` - Method invocation handling
- `ExtractFromAssignment()` - Assignment operations
- `ExtractFromKeyValueArguments()` - Key-value argument extraction

**Test Cases Needed**:

- KeyValuePair enumerable detection
- Object creation with KeyValuePair parameters
- Array creation with KeyValuePair elements
- Local variable references to KeyValuePair collections
- Field references to KeyValuePair collections
- Various collection initializer patterns
- Method invocations creating KeyValuePairs
- Assignment operations within initializers
- Error handling and edge cases

**Code Examples Not Currently Tested**:

```csharp
// 1. Object creation with collection initializer
var kvpCollection = new List<KeyValuePair<string, object?>>
{
    new("userId", userId),
    new("correlationId", correlationId)
};
using (logger.BeginScope(kvpCollection))
{
    // scope content
}

// 2. Array creation with KeyValuePair elements
var kvpArray = new KeyValuePair<string, object?>[]
{
    new("requestId", requestId),
    new("timestamp", DateTime.UtcNow)
};
using (logger.BeginScope(kvpArray))
{
    // scope content
}

// 3. Local variable reference
var scopeData = GetScopeKeyValuePairs();
using (logger.BeginScope(scopeData))
{
    // scope content
}

// 4. Field reference
private readonly List<KeyValuePair<string, object?>> _defaultScope = new();
using (logger.BeginScope(_defaultScope))
{
    // scope content
}

// 5. Method invocation creating KeyValuePairs
using (logger.BeginScope(CreateKeyValuePairs("operation", "save")))
{
    // scope content
}

// 6. Assignment operations within initializers
var scope = new Dictionary<string, object?>
{
    ["operation"] = "delete",
    ["entityId"] = entityId
};
```

### 2. ScopeAnalysisService (7.4% line coverage)

**Current Coverage**: 7.4% line coverage, 0% branch coverage  
**Location**: `src/LoggerUsage/Services/ScopeAnalysisService.cs`

**Missing Test Coverage**:

- `AnalyzeScopeState()` method - Main analysis method
- `ExtractParameters()` - Parameter extraction strategy
- `ExtractExtensionMethodParameters()` - Extension method parameter handling
- `ExtractCoreMethodParameters()` - Core method parameter handling
- `ExtractFromParamsArgument()` - Params array handling
- `ExtractFromArrayElements()` - Array element extraction
- `ExtractMessageTemplate()` - Message template extraction
- `GetArgumentIndex()` - Argument index calculation

**Test Cases Needed**:

- Extension method scope analysis
- Core BeginScope method analysis
- Message template extraction from literal values
- Parameter extraction from different argument types
- Params array parameter handling
- Array element parameter extraction
- Error handling scenarios
- KeyValuePair integration testing

**Code Examples Not Currently Tested**:

```csharp
// 1. Extension method with message template and params array
logger.BeginScope("Processing {Operation} for user {UserId} at {Timestamp}", 
    "login", userId, DateTime.UtcNow);

// 2. Core BeginScope with KeyValuePair enumerable
var scopeState = new List<KeyValuePair<string, object?>>
{
    new("transactionId", transactionId),
    new("amount", amount)
};
using (logger.BeginScope(scopeState))
{
    // scope content
}

// 3. Extension method with array creation
using (logger.BeginScope("Request {RequestId} from {Source}", 
    new object[] { requestId, sourceSystem }))
{
    // scope content  
}

// 4. Core BeginScope with anonymous object
using (logger.BeginScope(new { RequestId = requestId, UserId = userId }))
{
    // scope content
}

// 5. Complex params argument handling
logger.BeginScope("Complex operation {Op} with data {Data} and flags {Flags}",
    operation,
    new { Id = dataId, Name = dataName },
    new[] { "flag1", "flag2" });

// 6. Message template extraction from non-literal values
var template = GetMessageTemplate();
logger.BeginScope(template, param1, param2);
```

### 3. AnonymousObjectParameterExtractor (0% line coverage)

**Current Coverage**: 0% line coverage, 0% branch coverage  
**Location**: `src/LoggerUsage/ParameterExtraction/AnonymousObjectParameterExtractor.cs`

**Missing Test Coverage**:

- `TryExtractParameters()` method - Core extraction logic
- Anonymous object property extraction
- Property reference handling
- Assignment operation processing

**Test Cases Needed**:

- Anonymous object with simple properties
- Anonymous object with complex property types
- Nested anonymous objects
- Error handling for malformed objects
- Integration with scope analysis

**Code Examples Not Currently Tested**:

```csharp
// 1. Simple anonymous object
using (logger.BeginScope(new { UserId = userId, Action = "login" }))
{
    // scope content
}

// 2. Complex property types
using (logger.BeginScope(new 
{ 
    RequestId = Guid.NewGuid(),
    Timestamp = DateTimeOffset.UtcNow,
    Data = new { Count = 42, Items = new[] { "a", "b" } },
    Flags = new List<string> { "important", "urgent" }
}))
{
    // scope content
}

// 3. Nested anonymous objects
using (logger.BeginScope(new 
{
    User = new { Id = userId, Name = userName },
    Request = new { Id = requestId, Type = requestType },
    Context = new { Environment = "prod", Version = "1.0" }
}))
{
    // scope content
}

// 4. Anonymous object with null values
using (logger.BeginScope(new 
{ 
    OptionalData = (string?)null,
    RequiredData = "value",
    NullableInt = (int?)null
}))
{
    // scope content
}

// 5. Anonymous object with different value types
using (logger.BeginScope(new 
{
    StringValue = "text",
    IntValue = 123,
    BoolValue = true,
    DoubleValue = 3.14,
    DateValue = DateTime.Now,
    EnumValue = LogLevel.Information
}))
{
    // scope content
}
```

### 4. GenericTypeParameterExtractor (0% line coverage)

**Current Coverage**: 0% line coverage, 0% branch coverage  
**Location**: `src/LoggerUsage/ParameterExtraction/GenericTypeParameterExtractor.cs`

**Missing Test Coverage**:

- `TryExtractParameters()` method
- Generic type argument extraction
- Message template integration
- Type argument to parameter mapping

**Test Cases Needed**:

- LoggerMessage.Define generic method calls
- Type argument extraction from generic methods
- Message template parameter matching
- Non-generic method handling
- Error scenarios

**Code Examples Not Currently Tested**:

```csharp
// 1. LoggerMessage.Define with single generic type
private static readonly Action<ILogger, string, Exception?> LogUserAction =
    LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(1, "UserAction"),
        "User {UserId} performed action");

// 2. LoggerMessage.Define with multiple generic types
private static readonly Action<ILogger, string, int, DateTime, Exception?> LogComplexAction =
    LoggerMessage.Define<string, int, DateTime>(
        LogLevel.Warning,
        new EventId(2, "ComplexAction"),
        "User {UserId} performed action {ActionId} at {Timestamp}");

// 3. LoggerMessage.Define with value types
private static readonly Action<ILogger, int, double, bool, Exception?> LogNumericAction =
    LoggerMessage.Define<int, double, bool>(
        LogLevel.Debug,
        new EventId(3, "NumericAction"),
        "Processing item {ItemId} with score {Score} and flag {IsActive}");

// 4. LoggerMessage.Define with complex types
private static readonly Action<ILogger, Guid, TimeSpan, object, Exception?> LogObjectAction =
    LoggerMessage.Define<Guid, TimeSpan, object>(
        LogLevel.Error,
        new EventId(4, "ObjectAction"),
        "Operation {OperationId} took {Duration} with result {Result}");

// 5. Generic method without message template (error case)
var invalidDefine = LoggerMessage.Define<string>(
    LogLevel.Information,
    new EventId(5, "Invalid"),
    null); // null template

// 6. Non-generic method (should not be processed)
SomeNonGenericMethod();

// 7. Generic method with mismatched parameter count
var mismatchedDefine = LoggerMessage.Define<string, int>(
    LogLevel.Information, 
    new EventId(6, "Mismatched"),
    "Only one parameter {Param1}"); // 2 type args, 1 template param
```

### 4a. MethodSignatureParameterExtractor (0% line coverage)

**Current Coverage**: 0% line coverage, 0% branch coverage  
**Location**: `src/LoggerUsage/ParameterExtraction/MethodSignatureParameterExtractor.cs`

**Note**: This class has a placeholder `TryExtractParameters` method that returns false by design. The actual implementation is in the static `TryExtractFromMethodSignature` method used by LoggerMessageAttributeAnalyzer.

**Missing Test Coverage**:

- `TryExtractFromMethodSignature()` static method - Core functionality  
- Method parameter filtering (excluding ILogger, LogLevel, Exception types)
- Message template to method parameter mapping
- Parameter type display string generation

**Code Examples Not Currently Tested**:

```csharp
// Test the static method directly since it contains the real logic
public static void TestMethodSignatureExtraction()
{
    // 1. Method with standard logger parameters
    public void LogUserAction(ILogger logger, LogLevel level, string userId, int actionId, Exception? ex)
    {
        // Template: "User {UserId} performed action {ActionId}"
        // Should extract: userId (string), actionId (int)
        // Should exclude: logger, level, ex
    }

    // 2. Method with complex parameter types
    public void LogComplexData(ILogger<TestClass> logger, 
        Guid operationId, 
        CustomType data, 
        DateTime timestamp)
    {
        // Should extract all non-logger parameters with proper type names
    }

    // 3. Method with LogPropertiesAttribute parameters
    public void LogWithProperties(ILogger logger, 
        string message,
        [LogProperties] ComplexObject properties)
    {
        // Should exclude the LogProperties parameter
    }

    // 4. Template with more placeholders than method parameters
    // Template: "User {UserId} did {Action} at {Time} with {Data}"
    // Method: LogAction(ILogger logger, string userId, string action)
    // Should handle gracefully
}
```

## Moderate Priority Areas (Priority 2)

### 5. EventIdExtractor (10.4% line coverage)

**Current Coverage**: 10.4% line coverage, 9.5% branch coverage  
**Location**: `src/LoggerUsage/Analyzers/EventIdExtractor.cs`

**Additional Test Coverage Needed**:

- Complex EventId extraction scenarios
- Error handling paths
- Edge cases in EventId detection

**Code Examples Not Currently Tested**:

```csharp
// 1. EventId constructor with both ID and name
logger.LogInformation(new EventId(100, "UserLogin"), "User {UserId} logged in", userId);

// 2. EventId constructor with only ID
logger.LogError(new EventId(500), "An error occurred: {Error}", errorMessage);

// 3. EventId from field reference
private static readonly EventId LoginEventId = new(101, "Login");
logger.LogInformation(LoginEventId, "Login attempt for {Username}", username);

// 4. EventId from property reference
public EventId CurrentEventId => new(GetEventIdValue(), GetEventName());
logger.LogWarning(CurrentEventId, "Warning: {Message}", warningMessage);

// 5. EventId from method call
logger.LogDebug(GetEventId("debug"), "Debug info: {Data}", debugData);

// 6. EventId from static field
logger.LogCritical(Events.SystemFailure, "System failure: {Details}", details);

// 7. Default EventId parameter (should be skipped)
logger.LogInformation(default(EventId), "Message without specific event ID");

// 8. Complex EventId construction
var dynamicId = isError ? ErrorEvents.ValidationFailed : InfoEvents.ValidationPassed;
logger.Log(LogLevel.Information, dynamicId, "Validation result: {IsValid}", isValid);

// 9. EventId from constant expression
logger.LogTrace(new EventId(DEBUG_BASE_ID + 1, "TraceEvent"), "Trace: {Value}", value);

// 10. EventId with null name (edge case)
logger.LogInformation(new EventId(200, null), "Event with null name");
```

### 6. LoggerMessageDefineAnalyzer (5.6% line coverage)

**Current Coverage**: 5.6% line coverage, 0% branch coverage  
**Location**: `src/LoggerUsage/Analyzers/LoggerMessageDefineAnalyzer.cs`

**Missing Test Coverage**:

- LoggerMessage.Define analysis
- Generic type parameter integration
- Message template extraction
- Parameter mapping

**Code Examples Not Currently Tested**:

```csharp
// 1. Basic LoggerMessage.Define usage
public static partial class LoggerExtensions
{
    private static readonly Action<ILogger, string, Exception?> _userLoggedIn =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1001, "UserLoggedIn"),
            "User {UserId} has logged in successfully");

    public static void UserLoggedIn(this ILogger logger, string userId)
        => _userLoggedIn(logger, userId, null);
}

// 2. LoggerMessage.Define with multiple parameters
private static readonly Action<ILogger, string, int, DateTime, Exception?> _orderProcessed =
    LoggerMessage.Define<string, int, DateTime>(
        LogLevel.Information,
        new EventId(2001, "OrderProcessed"),
        "Order {OrderId} with {ItemCount} items processed at {ProcessedAt}");

// 3. LoggerMessage.Define with complex EventId
private static readonly Action<ILogger, Guid, Exception?> _operationFailed =
    LoggerMessage.Define<Guid>(
        LogLevel.Error,
        Events.OperationFailure, // Static EventId reference
        "Operation {OperationId} failed with error");

// 4. LoggerMessage.Define with no parameters
private static readonly Action<ILogger, Exception?> _systemStarted =
    LoggerMessage.Define(
        LogLevel.Information,
        new EventId(3001, "SystemStarted"),
        "System has started successfully");

// 5. LoggerMessage.Define with custom types
private static readonly Action<ILogger, CustomType, TimeSpan, Exception?> _customOperation =
    LoggerMessage.Define<CustomType, TimeSpan>(
        LogLevel.Debug,
        new EventId(4001, "CustomOperation"),
        "Custom operation {Operation} completed in {Duration}");

// 6. Nested LoggerMessage.Define calls
public class NestedLoggers
{
    private static readonly Action<ILogger, string, Exception?> _innerLogger =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(5001, "InnerEvent"),
            "Inner event: {Message}");
}
```

### 7. BeginScopeAnalyzer (12.5% line coverage)

**Current Coverage**: 12.5% line coverage, 0% branch coverage  
**Location**: `src/LoggerUsage/Analyzers/BeginScopeAnalyzer.cs`

**Additional Test Coverage Needed**:

- More complex scope analysis scenarios
- Error handling paths
- Integration with scope analysis service

## Model Classes (Priority 3)

### 8. Model Classes with 0% Coverage

- `EventIdBase` (0% coverage)
- `EventIdDetails` (0% coverage)
- `EventIdRef` (0% coverage)
- `ConstantOrReference` (0% coverage)
- `ScopeAnalysisResult` (0% coverage in some test runs)

**Test Cases Needed**:

- Property getters/setters
- Object construction
- Equality comparisons
- ToString() methods
- Validation logic

**Code Examples Not Currently Tested**:

```csharp
// 1. EventIdBase and derived classes
var eventIdDetails = new EventIdDetails(
    ConstantOrReference.Constant(100),
    ConstantOrReference.Constant("TestEvent"));

var eventIdRef = new EventIdRef("Events.TestEvent");

// 2. ConstantOrReference creation and usage
var constantValue = ConstantOrReference.Constant("testValue");
var referenceValue = ConstantOrReference.Reference("variableName");
var missingValue = ConstantOrReference.Missing;

// Test ToString() methods
string constantStr = constantValue.ToString();
string referenceStr = referenceValue.ToString();
string missingStr = missingValue.ToString();

// Test equality
bool areEqual = constantValue.Equals(ConstantOrReference.Constant("testValue"));
bool hashCodesEqual = constantValue.GetHashCode() == referenceValue.GetHashCode();

// 3. ScopeAnalysisResult success and failure scenarios
var successResult = ScopeAnalysisResult.Success(
    "Message template {Param}",
    new List<MessageParameter> { new("Param", "string", "Literal") },
    isExtensionMethod: true);

var failureResult = ScopeAnalysisResult.Failure("Analysis failed due to invalid syntax");

// Test properties
bool isSuccess = successResult.IsSuccess;
string? errorMessage = failureResult.ErrorMessage;
var parameters = successResult.Parameters;

// 4. MessageParameter creation and properties
var messageParam = new MessageParameter("UserId", "string", "LocalReference");
string paramName = messageParam.Name;
string paramType = messageParam.Type;
string? paramKind = messageParam.Kind;

// Test record equality
var duplicateParam = new MessageParameter("UserId", "string", "LocalReference");
bool paramsEqual = messageParam.Equals(duplicateParam);

// 5. MethodCallLocation properties
var location = new MethodCallLocation("TestFile.cs", 42, 15);
string fileName = location.FileName;
int lineNumber = location.LineNumber;
int columnNumber = location.ColumnNumber;
```

## Report Generators (Priority 4)

### 9. HtmlLoggerReportGenerator (0-99% coverage varies by test run)

**Location**: `src/LoggerUsage/ReportGenerator/HtmlLoggerReportGenerator.cs`

**Additional Test Coverage Needed**:

- HTML generation for complex scenarios
- Template rendering
- Error handling in report generation

**Code Examples Not Currently Tested**:

```csharp
// 1. Complex extraction result with multiple logger usages
var complexResult = new LoggerUsageExtractionResult
{
    Results = new List<LoggerUsageInfo>
    {
        new() { 
            MethodType = LoggerUsageMethodType.LogInformation,
            MessageTemplate = "User {UserId} performed {Action} at {Timestamp}",
            Parameters = new List<MessageParameter>
            {
                new("UserId", "string", "LocalReference"),
                new("Action", "string", "Literal"),
                new("Timestamp", "DateTime", "PropertyReference")
            },
            EventId = new EventIdDetails(
                ConstantOrReference.Constant(1001),
                ConstantOrReference.Constant("UserAction")),
            Location = new MethodCallLocation("UserService.cs", 45, 12)
        },
        new() {
            MethodType = LoggerUsageMethodType.BeginScope,
            MessageTemplate = "Processing request {RequestId}",
            Parameters = new List<MessageParameter>
            {
                new("RequestId", "Guid", "FieldReference")
            },
            ScopeAnalysis = ScopeAnalysisResult.Success(
                "Processing request {RequestId}",
                new List<MessageParameter> { new("RequestId", "Guid", "FieldReference") },
                true)
        }
    },
    Summary = new LoggerUsageExtractionSummary
    {
        TotalLoggerUsages = 2,
        UniqueMessageTemplates = 2,
        ParameterTypeDistribution = new Dictionary<string, int>
        {
            ["string"] = 2,
            ["DateTime"] = 1,
            ["Guid"] = 1
        }
    }
};

// 2. HTML generation with different themes (dark/light mode)
var htmlGenerator = new HtmlLoggerReportGenerator();
string lightThemeHtml = htmlGenerator.GenerateReport(complexResult, isDarkMode: false);
string darkThemeHtml = htmlGenerator.GenerateReport(complexResult, isDarkMode: true);

// 3. Error handling scenarios
var invalidResult = new LoggerUsageExtractionResult { Results = null! };
// Should handle null results gracefully

var emptyResult = new LoggerUsageExtractionResult 
{ 
    Results = new List<LoggerUsageInfo>(),
    Summary = new LoggerUsageExtractionSummary { TotalLoggerUsages = 0 }
};
// Should generate empty report HTML

// 4. Edge cases in template rendering
var edgeCaseResult = new LoggerUsageExtractionResult
{
    Results = new List<LoggerUsageInfo>
    {
        new() {
            MessageTemplate = "Template with special chars: <>&\"'{}",
            Parameters = new List<MessageParameter>(),
            EventId = null // No event ID
        }
    }
};
```

### 10. LoggerReportGeneratorFactory (0-83% coverage)

**Location**: `src/LoggerUsage/ReportGenerator/LoggerReportGeneratorFactory.cs`

**Test Cases Needed**:

- Factory method selection logic
- Different report format handling
- Error scenarios

**Code Examples Not Currently Tested**:

```csharp
// 1. Factory method selection based on format
var factory = new LoggerReportGeneratorFactory();

// Test HTML format selection
var htmlGenerator = factory.CreateGenerator("html");
Assert.IsType<HtmlLoggerReportGenerator>(htmlGenerator);

// Test JSON format selection  
var jsonGenerator = factory.CreateGenerator("json");
Assert.IsType<JsonLoggerReportGenerator>(jsonGenerator);

// Test case-insensitive format handling
var htmlGeneratorUpperCase = factory.CreateGenerator("HTML");
var jsonGeneratorMixedCase = factory.CreateGenerator("Json");

// 2. Default format handling
var defaultGenerator = factory.CreateGenerator(null);
var emptyGenerator = factory.CreateGenerator("");

// 3. Invalid format handling (error scenarios)
try
{
    var invalidGenerator = factory.CreateGenerator("xml"); // Unsupported format
    // Should throw or return default
}
catch (ArgumentException ex)
{
    // Handle unsupported format exception
}

// 4. Format validation
bool isHtmlSupported = factory.IsSupportedFormat("html");
bool isJsonSupported = factory.IsSupportedFormat("json");
bool isXmlSupported = factory.IsSupportedFormat("xml"); // Should be false

// 5. Available formats enumeration
var supportedFormats = factory.GetSupportedFormats();
Assert.Contains("html", supportedFormats);
Assert.Contains("json", supportedFormats);
```

## Implementation Plan

### Phase 1: Critical Service Tests (Week 1-2)

1. **KeyValuePairExtractionService** - Create comprehensive test suite
2. **ScopeAnalysisService** - Test all parameter extraction scenarios
3. **AnonymousObjectParameterExtractor** - Basic extraction tests

### Phase 2: Parameter Extractors (Week 3)

1. **GenericTypeParameterExtractor** - Generic method handling
2. **MethodSignatureParameterExtractor** - Method signature analysis (currently 0% coverage)
3. Integration tests between extractors

**Note**: MethodSignatureParameterExtractor has 0% coverage as its main `TryExtractParameters` method returns false by design. The actual logic is in the static `TryExtractFromMethodSignature` method used by LoggerMessageAttributeAnalyzer.

### Phase 3: Analyzers (Week 4)

1. **EventIdExtractor** - Complex EventId scenarios
2. **LoggerMessageDefineAnalyzer** - Define method analysis
3. **BeginScopeAnalyzer** - Scope analysis integration

### Phase 4: Models and Infrastructure (Week 5)

1. Model classes property testing
2. Report generator testing
3. Factory pattern testing

### Phase 5: Integration and Edge Cases (Week 6)

1. End-to-end integration tests
2. Error handling scenarios
3. Performance test cases
4. Cross-component interaction tests

## Testing Strategy

### Test File Organization

- Create dedicated test files for each untested service
- Use descriptive test method names following the pattern: `Should_<ExpectedBehavior>_When_<Condition>`
- Group related tests using nested test classes or theory data

### Test Data Approach

- Use realistic C# code samples as test input
- Create helper methods for common test setup
- Use parameterized tests for multiple scenarios
- Mock dependencies appropriately

### Coverage Goals

- Target 90%+ line coverage for critical services
- Target 85%+ branch coverage for complex logic
- Ensure all public methods have at least one test
- Test both success and failure paths

### Key Testing Techniques Required

#### 1. Roslyn Testing Patterns

- Use `CSharpCompilation.Create()` to build test compilations
- Create `IOperation` trees for testing parameter extractors
- Mock `ILogger` and `LoggingTypes` dependencies
- Use `TestUtils.CreateCompilationAsync()` helper patterns

#### 2. Parameterized Testing

```csharp
[Theory]
[InlineData("new List<KeyValuePair<string, object?>> { new(\"key\", value) }", true)]
[InlineData("new Dictionary<string, object?> { [\"key\"] = value }", false)]
[InlineData("someVariable", true)] // if variable is KVP enumerable
public async Task TryExtractParameters_Should_DetectKeyValuePairTypes(
    string sourceCode, bool expectedResult)
{
    // Test implementation
}
```

#### 3. Mock Setup for Services

```csharp
var mockKeyValuePairService = new Mock<IKeyValuePairExtractionService>();
mockKeyValuePairService.Setup(x => x.IsKeyValuePairEnumerable(It.IsAny<ITypeSymbol>(), It.IsAny<LoggingTypes>()))
                       .Returns(true);

var scopeAnalysisService = new ScopeAnalysisService(
    mockKeyValuePairService.Object,
    new AnonymousObjectParameterExtractor(),
    loggerFactory);
```

#### 4. Exception Testing Patterns

```csharp
[Fact]
public void Should_HandleException_When_InvalidOperation()
{
    // Arrange
    var invalidOperation = CreateInvalidOperation();
    
    // Act & Assert
    var result = service.TryExtractParameters(invalidOperation, loggingTypes);
    Assert.Empty(result); // Should return empty list, not throw
}
```

#### 5. Integration Testing Between Components

```csharp
[Fact]
public async Task ScopeAnalysis_Should_IntegrateWith_KeyValuePairExtraction()
{
    // Test that ScopeAnalysisService correctly uses KeyValuePairExtractionService
    // and falls back to AnonymousObjectParameterExtractor when appropriate
}
```

### Verification

- Run coverage reports after each phase
- Verify coverage improvements in CI pipeline
- Document any intentionally untested code paths

## Expected Outcomes

After implementation:

- **Overall line coverage**: 85%+ (target: 90%)
- **Overall branch coverage**: 75%+ (target: 85%)
- **Critical services coverage**: 90%+
- **Model classes coverage**: 95%+

## New Test Files Required

Based on the coverage analysis, the following new test files should be created:

### Critical Priority Test Files

1. **`KeyValuePairExtractionServiceTests.cs`** - Comprehensive testing of KeyValuePair extraction logic
2. **`ScopeAnalysisServiceTests.cs`** - End-to-end scope analysis testing
3. **`AnonymousObjectParameterExtractorTests.cs`** - Anonymous object parameter extraction
4. **`GenericTypeParameterExtractorTests.cs`** - Generic type argument extraction
5. **`MethodSignatureParameterExtractorTests.cs`** - Method signature parameter extraction

### Moderate Priority Test Files

1. **`EventIdExtractorTests.cs`** - Enhanced EventId extraction scenarios
2. **`LoggerMessageDefineAnalyzerTests.cs`** - LoggerMessage.Define analysis
3. **`ModelClassesTests.cs`** - Combined testing for all model classes
4. **`HtmlReportGeneratorTests.cs`** - HTML report generation testing
5. **`ReportGeneratorFactoryTests.cs`** - Factory pattern testing

### Integration Test Files

1. **`ParameterExtractionIntegrationTests.cs`** - Cross-component integration testing
2. **`EndToEndAnalysisTests.cs`** - Complete workflow testing

## Estimated Test Coverage Impact

| Component | Current Coverage | Target Coverage | New Tests Needed |
|-----------|------------------|-----------------|------------------|
| KeyValuePairExtractionService | 2.4% | 90%+ | ~15-20 test methods |
| ScopeAnalysisService | 7.4% | 90%+ | ~12-15 test methods |
| AnonymousObjectParameterExtractor | 0% | 95%+ | ~8-10 test methods |
| GenericTypeParameterExtractor | 0% | 95%+ | ~6-8 test methods |
| MethodSignatureParameterExtractor | 0% | 85%+ | ~5-7 test methods |
| EventIdExtractor | 10.4% | 85%+ | ~8-10 test methods |
| Model Classes | 0-50% | 95%+ | ~20-25 test methods |
| Report Generators | 0-99% | 85%+ | ~10-12 test methods |

**Total estimated new test methods**: ~85-105 tests

This plan prioritizes the most impactful areas first, focusing on services that contain complex business logic and are central to the application's functionality.
