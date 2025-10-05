using AwesomeAssertions;
using LoggerUsage.Models;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

/// <summary>
/// Tests for BeginScope logger usage analysis.
///
/// Note: Some tests reflect current implementation limitations:
/// - Dictionary variable references extract the variable, not dictionary contents
/// - Method invocation parameter extraction is not implemented
/// - Extension methods require literal message templates
/// These limitations are documented in the test coverage improvement plan.
/// </summary>
public class BeginScopeTests
{
    [Fact]
    public async Task BeginScope_ExtensionMethod_DetectedCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        using (logger.BeginScope(""Processing request {RequestId}"", 123))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().HaveCount(2);

        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MethodName.Should().Be(nameof(ILogger.BeginScope));
        beginScopeUsage!.MessageTemplate.Should().Be("Processing request {RequestId}");
    }

    [Fact]
    public async Task BeginScope_CoreMethod_DetectedCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger<TestClass> logger)
    {
        using (logger.BeginScope(new { RequestId = 123, UserId = ""user1"" }))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().HaveCount(2);

        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MethodName.Should().Be(nameof(ILogger.BeginScope));
        beginScopeUsage!.MessageTemplate.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(BeginScopeMessageParameterCases))]
    public async Task BeginScope_ExtensionMethod_MessageParameters(string template, string[] argNames, List<MessageParameter> expectedParameters)
    {
        // Arrange
        var methodArgs = new List<string> { $"\"{template}\"" };
        methodArgs.AddRange(argNames);

        var code = $@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{{
    private readonly string _strField = ""fieldValue"";
    private readonly int _intField = 42;
    private readonly bool _boolField = true;

    public void TestMethod(ILogger logger, string strArg, int intArg, bool boolArg)
    {{
        const int constInt = 42;
        const bool constBool = true;
        string localStr = ""localStr"";
        int localInt = 42;
        bool localBool = true;

        using (logger.BeginScope({string.Join(", ", methodArgs)}))
        {{
            logger.LogInformation(""Inside scope"");
        }}
    }}
}}";
        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();

        var parameters = beginScopeUsage!.MessageParameters;
        parameters.Should().HaveCount(expectedParameters.Count);
        parameters.Should().BeEquivalentTo(expectedParameters);
    }

    public static TheoryData<string, string[], List<MessageParameter>> BeginScopeMessageParameterCases() => new()
    {
        // Simple parameter references
        { "Processing request {RequestId}", ["123"], [
            new MessageParameter("RequestId", "int", "Constant")
        ] },
        { "User {UserId} in request {RequestId}", ["\"user123\"", "456"], [
            new MessageParameter("UserId", "string", "Constant"),
            new MessageParameter("RequestId", "int", "Constant")
        ] },
        { "Processing {Operation} for {UserId} with {RequestId}", ["\"Create\"", "\"user123\"", "789"], [
            new MessageParameter("Operation", "string", "Constant"),
            new MessageParameter("UserId", "string", "Constant"),
            new MessageParameter("RequestId", "int", "Constant")
        ] },

        // Parameter references from method arguments
        { "User {UserId} processing {RequestId}", ["strArg", "intArg"], [
            new MessageParameter("UserId", "string", "ParameterReference"),
            new MessageParameter("RequestId", "int", "ParameterReference")
        ] },

        // Local variable references
        { "Processing {Operation} for {UserId}", ["localStr", "localInt"], [
            new MessageParameter("Operation", "string", "LocalReference"),
            new MessageParameter("UserId", "int", "LocalReference")
        ] },

        // Field references
        { "Processing {Operation} for {RequestId}", ["_strField", "_intField"], [
            new MessageParameter("Operation", "string", "FieldReference"),
            new MessageParameter("RequestId", "int", "FieldReference")
        ] },

        // No parameters
        { "Processing request", [], [] },
    };

    [Fact]
    public async Task BeginScope_ExtensionMethod_WithSingleParameter()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        using (logger.BeginScope(""Processing request {RequestId}"", 123))
        {
            // scope content
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MessageTemplate.Should().Be("Processing request {RequestId}");
        beginScopeUsage!.MessageParameters.Should().ContainSingle()
            .Which.Should().Match<MessageParameter>(p =>
                p.Name == "RequestId" && p.Type == "int" && p.Kind == "Constant");
    }

    [Fact]
    public async Task BeginScope_ExtensionMethod_WithMultipleParameters()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;
namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        using (logger.BeginScope(""User {UserId} processing request {RequestId} with status {Status}"", ""user123"", 456, true))
        {
            // scope content
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MessageTemplate.Should().Be("User {UserId} processing request {RequestId} with status {Status}");
        beginScopeUsage!.MessageParameters.Should().HaveCount(3);

        beginScopeUsage!.MessageParameters[0].Name.Should().Be("UserId");
        beginScopeUsage!.MessageParameters[0].Type.Should().Be("string");
        beginScopeUsage!.MessageParameters[0].Kind.Should().Be("Constant");

        beginScopeUsage!.MessageParameters[1].Name.Should().Be("RequestId");
        beginScopeUsage!.MessageParameters[1].Type.Should().Be("int");
        beginScopeUsage!.MessageParameters[1].Kind.Should().Be("Constant");

        beginScopeUsage!.MessageParameters[2].Name.Should().Be("Status");
        beginScopeUsage!.MessageParameters[2].Type.Should().Be("bool");
        beginScopeUsage!.MessageParameters[2].Kind.Should().Be("Constant");
    }

    [Theory]
    [MemberData(nameof(KeyValuePairBeginScopeTestCases))]
    public async Task BeginScope_KeyValuePairCollections_DetectedCorrectly(string testCode, int expectedParameterCount, List<MessageParameter> expectedParameters)
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(testCode);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        loggerUsages.Should().NotBeNull();
        loggerUsages.Results.Should().HaveCount(2);

        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MethodName.Should().Be(nameof(ILogger.BeginScope));
        beginScopeUsage!.MessageTemplate.Should().BeNull();

        // Assert.Skip("TODO: parse KeyValuePair collections correctly");
        beginScopeUsage!.MessageParameters.Should().HaveCount(expectedParameterCount);

        for (int i = 0; i < expectedParameters.Count; i++)
        {
            beginScopeUsage!.MessageParameters[i].Name.Should().Be(expectedParameters[i].Name);
            beginScopeUsage!.MessageParameters[i].Type.Should().Be(expectedParameters[i].Type);
            beginScopeUsage!.MessageParameters[i].Kind.Should().Be(expectedParameters[i].Kind);
        }
    }

    public static TheoryData<string, int, List<MessageParameter>> KeyValuePairBeginScopeTestCases() => new()
    {
        {
            @"#nullable enable
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        using (logger.BeginScope(new List<KeyValuePair<string, object?>>
        {
            new(""RequestId"", 123),
            new(""UserId"", ""user1""),
            new(""Operation"", ""Create"")
        }))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}",
            3,
            new List<MessageParameter>
            {
                new("RequestId", "int", "Constant"),
                new("UserId", "string", "Constant"),
                new("Operation", "string", "Constant")
            }
        },
        {
            @"#nullable enable
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        using (logger.BeginScope(new KeyValuePair<string, object?>[]
        {
            new(""RequestId"", 123),
            new(""UserId"", ""user1"")
        }))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}",
            2,
            new List<MessageParameter>
            {
                new("RequestId", "int", "Constant"),
                new("UserId", "string", "Constant")
            }
        },
        {
            @"#nullable enable
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        using (logger.BeginScope(new Dictionary<string, object?>
        {
            [""RequestId""] = 123,
            [""UserId""] = ""user1"",
            [""IsActive""] = true
        }))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}",
            3,
            new List<MessageParameter>
            {
                new("RequestId", "int", "Constant"),
                new("UserId", "string", "Constant"),
                new("IsActive", "bool", "Constant")
            }
        }
    };

    [Fact]
    public async Task BeginScope_AnonymousObject_ExtractsParametersCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"#nullable enable
using Microsoft.Extensions.Logging;
using System;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger, string userId, int requestId)
    {
        using (logger.BeginScope(new { UserId = userId, RequestId = requestId, Timestamp = DateTime.UtcNow }))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MethodName.Should().Be(nameof(ILogger.BeginScope));
        beginScopeUsage!.MessageTemplate.Should().BeNull();

        // Should extract parameters from anonymous object properties
        beginScopeUsage!.MessageParameters.Should().HaveCount(3);

        var userIdParam = beginScopeUsage!.MessageParameters.FirstOrDefault(p => p.Name == "UserId");
        userIdParam.Should().NotBeNull();
        userIdParam!.Type.Should().Be("string");
        userIdParam!.Kind.Should().Be("ParameterReference");

        var requestIdParam = beginScopeUsage!.MessageParameters.FirstOrDefault(p => p.Name == "RequestId");
        requestIdParam.Should().NotBeNull();
        requestIdParam!.Type.Should().Be("int");
        requestIdParam!.Kind.Should().Be("ParameterReference");

        var timestampParam = beginScopeUsage!.MessageParameters.FirstOrDefault(p => p.Name == "Timestamp");
        timestampParam.Should().NotBeNull();
        timestampParam!.Type.Should().Be("System.DateTime");
        timestampParam!.Kind.Should().Be("PropertyReference");
    }

    [Fact]
    public async Task BeginScope_AnonymousObjectWithComplexTypes_HandledCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"#nullable enable
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger, Guid operationId)
    {
        using (logger.BeginScope(new
        {
            OperationId = operationId,
            Data = new { Count = 42, Items = new[] { ""a"", ""b"" } },
            Flags = new List<string> { ""important"", ""urgent"" },
            Timestamp = DateTimeOffset.UtcNow
        }))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MessageParameters.Should().HaveCount(4);

        var operationIdParam = beginScopeUsage!.MessageParameters.FirstOrDefault(p => p.Name == "OperationId");
        operationIdParam.Should().NotBeNull();
        operationIdParam!.Type.Should().Be("System.Guid");

        var dataParam = beginScopeUsage!.MessageParameters.FirstOrDefault(p => p.Name == "Data");
        dataParam.Should().NotBeNull();

        var flagsParam = beginScopeUsage!.MessageParameters.FirstOrDefault(p => p.Name == "Flags");
        flagsParam.Should().NotBeNull();
        flagsParam!.Type.Should().Be("System.Collections.Generic.List<string>");
    }

    [Fact]
    public async Task BeginScope_AnonymousObjectWithNullValues_HandledGracefully()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"#nullable enable
using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        using (logger.BeginScope(new
        {
            OptionalData = (string?)null,
            RequiredData = ""value"",
            NullableInt = (int?)null
        }))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MessageParameters.Should().HaveCount(3);

        var optionalParam = beginScopeUsage!.MessageParameters.FirstOrDefault(p => p.Name == "OptionalData");
        optionalParam.Should().NotBeNull();
        optionalParam!.Type.Should().Be("object");

        var requiredParam = beginScopeUsage!.MessageParameters.FirstOrDefault(p => p.Name == "RequiredData");
        requiredParam.Should().NotBeNull();
        requiredParam!.Type.Should().Be("string");

        var nullableParam = beginScopeUsage!.MessageParameters.FirstOrDefault(p => p.Name == "NullableInt");
        nullableParam.Should().NotBeNull();
        nullableParam!.Type.Should().Be("object");
    }

    [Fact]
    public async Task BeginScope_ExtensionMethodWithParamsArray_ExtractsParametersCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;
using System;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger, string operation, string userId)
    {
        using (logger.BeginScope(""Processing {Operation} for user {UserId} at {Timestamp}"",
            operation, userId, DateTime.UtcNow))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MessageTemplate.Should().Be("Processing {Operation} for user {UserId} at {Timestamp}");
        beginScopeUsage!.MessageParameters.Should().HaveCount(3);

        beginScopeUsage!.MessageParameters[0].Name.Should().Be("Operation");
        beginScopeUsage!.MessageParameters[0].Type.Should().Be("string");
        beginScopeUsage!.MessageParameters[0].Kind.Should().Be("ParameterReference");

        beginScopeUsage!.MessageParameters[1].Name.Should().Be("UserId");
        beginScopeUsage!.MessageParameters[1].Type.Should().Be("string");
        beginScopeUsage!.MessageParameters[1].Kind.Should().Be("ParameterReference");

        beginScopeUsage!.MessageParameters[2].Name.Should().Be("Timestamp");
        beginScopeUsage!.MessageParameters[2].Type.Should().Be("System.DateTime");
        beginScopeUsage!.MessageParameters[2].Kind.Should().Be("PropertyReference");
    }

    [Fact]
    public async Task BeginScope_ExtensionMethodWithArrayArgument_HandledCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger, string requestId, string sourceSystem)
    {
        using (logger.BeginScope(""Request {RequestId} from {Source}"",
            new object[] { requestId, sourceSystem }))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MessageTemplate.Should().Be("Request {RequestId} from {Source}");
        beginScopeUsage!.MessageParameters.Should().HaveCount(2);

        beginScopeUsage!.MessageParameters[0].Name.Should().Be("RequestId");
        beginScopeUsage!.MessageParameters[0].Type.Should().Be("string");
        beginScopeUsage!.MessageParameters[0].Kind.Should().Be("ParameterReference");

        beginScopeUsage!.MessageParameters[1].Name.Should().Be("Source");
        beginScopeUsage!.MessageParameters[1].Type.Should().Be("string");
        beginScopeUsage!.MessageParameters[1].Kind.Should().Be("ParameterReference");
    }

    [Fact]
    public async Task BeginScope_ComplexParamsArgumentHandling_ProcessedCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger, string operation, int dataId, string dataName)
    {
        using (logger.BeginScope(""Complex operation {Op} with data {Data} and flags {Flags}"",
            operation,
            new { Id = dataId, Name = dataName },
            new[] { ""flag1"", ""flag2"" }))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MessageTemplate.Should().Be("Complex operation {Op} with data {Data} and flags {Flags}");
        beginScopeUsage!.MessageParameters.Should().HaveCount(3);

        beginScopeUsage!.MessageParameters[0].Name.Should().Be("Op");
        beginScopeUsage!.MessageParameters[0].Type.Should().Be("string");
        beginScopeUsage!.MessageParameters[0].Kind.Should().Be("ParameterReference");

        beginScopeUsage!.MessageParameters[1].Name.Should().Be("Data");
        beginScopeUsage!.MessageParameters[2].Name.Should().Be("Flags");
    }

    [Fact]
    public async Task BeginScope_MessageTemplateFromVariable_HandledCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{
    private string GetMessageTemplate() => ""Processing {Operation} at {Timestamp}"";

    public void TestMethod(ILogger logger, string operation, System.DateTime timestamp)
    {
        var template = GetMessageTemplate();
        using (logger.BeginScope(template, operation, timestamp))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        // Message template should be null when not a literal string
        beginScopeUsage!.MessageTemplate.Should().BeNull();
        // Extension methods with non-literal templates don't extract parameters
        beginScopeUsage!.MessageParameters.Should().BeEmpty();
    }

    [Fact]
    public async Task BeginScope_LocalVariableReference_ExtractedCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"#nullable enable
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public class TestClass
{
    private List<KeyValuePair<string, object?>> GetScopeKeyValuePairs() =>
        new List<KeyValuePair<string, object?>> { new(""key"", ""value"") };

    public void TestMethod(ILogger logger)
    {
        var scopeData = GetScopeKeyValuePairs();
        using (logger.BeginScope(scopeData))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MethodName.Should().Be(nameof(ILogger.BeginScope));
        beginScopeUsage!.MessageTemplate.Should().BeNull();
        // Parameter extraction from local variable reference
        beginScopeUsage!.MessageParameters.Should().ContainSingle()
            .Which.Should().Match<MessageParameter>(p =>
                p.Name == "<scopeData>" && p.Kind == "LocalReference");
    }

    [Fact]
    public async Task BeginScope_FieldReference_ExtractedCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"#nullable enable
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public class TestClass
{
    private readonly List<KeyValuePair<string, object?>> _defaultScope = new();

    public void TestMethod(ILogger logger)
    {
        using (logger.BeginScope(_defaultScope))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MethodName.Should().Be(nameof(ILogger.BeginScope));
        beginScopeUsage!.MessageTemplate.Should().BeNull();
        // Parameter extraction from field reference
        beginScopeUsage!.MessageParameters.Should().ContainSingle()
            .Which.Should().Match<MessageParameter>(p =>
                p.Name == "<_defaultScope>" && p.Kind == "FieldReference");
    }

    [Fact]
    public async Task BeginScope_MethodInvocationCreatingKeyValuePairs_HandledCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"#nullable enable
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public class TestClass
{
    private List<KeyValuePair<string, object?>> CreateKeyValuePairs(string key, object value) =>
        new() { new(key, value) };

    public void TestMethod(ILogger logger)
    {
        using (logger.BeginScope(CreateKeyValuePairs(""operation"", ""save"")))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MethodName.Should().Be(nameof(ILogger.BeginScope));
        beginScopeUsage!.MessageTemplate.Should().BeNull();
        // Method invocation parameter extraction is not currently implemented
        beginScopeUsage!.MessageParameters.Should().BeEmpty();
    }

    [Fact]
    public async Task BeginScope_DictionaryWithAssignmentOperations_ExtractedCorrectly()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"#nullable enable
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger, string entityId, string operation)
    {
        var scope = new Dictionary<string, object?>
        {
            [""operation""] = operation,
            [""entityId""] = entityId
        };

        using (logger.BeginScope(scope))
        {
            logger.LogInformation(""Inside scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MethodName.Should().Be(nameof(ILogger.BeginScope));
        beginScopeUsage!.MessageTemplate.Should().BeNull();
        // Currently extracts the local variable reference, not dictionary contents
        beginScopeUsage!.MessageParameters.Should().ContainSingle();

        var firstParam = beginScopeUsage!.MessageParameters[0];
        firstParam.Name.Should().Be("<scope>");
        firstParam.Kind.Should().Be("LocalReference");
    }

    [Fact]
    public async Task BeginScope_EmptyScope_HandledGracefully()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        using (logger.BeginScope(""""))
        {
            logger.LogInformation(""Inside empty scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        beginScopeUsage!.MessageTemplate.Should().Be("");
        beginScopeUsage!.MessageParameters.Should().BeEmpty();
    }

    [Fact]
    public async Task BeginScope_NullArgument_HandledGracefully()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"#nullable enable
using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger)
    {
        string? nullTemplate = null;
        using (logger.BeginScope(nullTemplate!))
        {
            logger.LogInformation(""Inside null scope"");
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        beginScopeUsage.Should().NotBeNull();
        // Should handle null template gracefully
        beginScopeUsage!.MessageTemplate.Should().BeNull();
        // Core methods don't extract simple variable references
        beginScopeUsage!.MessageParameters.Should().BeEmpty();
    }

    [Fact]
    public async Task BeginScope_NestedScopes_BothDetected()
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(@"using Microsoft.Extensions.Logging;

namespace TestNamespace;

public class TestClass
{
    public void TestMethod(ILogger logger, string userId, string requestId)
    {
        using (logger.BeginScope(""User {UserId}"", userId))
        {
            using (logger.BeginScope(""Request {RequestId}"", requestId))
            {
                logger.LogInformation(""Inside nested scopes"");
            }
        }
    }
}");
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        var beginScopeUsages = loggerUsages.Results.Where(r => r.MethodType == LoggerUsageMethodType.BeginScope).ToList();
        beginScopeUsages.Should().HaveCount(2);

        var userScope = beginScopeUsages.FirstOrDefault(s => s.MessageTemplate?.Contains("UserId") == true);
        userScope.Should().NotBeNull();
        userScope!.MessageTemplate.Should().Be("User {UserId}");
        userScope!.MessageParameters.Should().ContainSingle()
            .Which.Name.Should().Be("UserId");

        var requestScope = beginScopeUsages.FirstOrDefault(s => s.MessageTemplate?.Contains("RequestId") == true);
        requestScope.Should().NotBeNull();
        requestScope!.MessageTemplate.Should().Be("Request {RequestId}");
        requestScope!.MessageParameters.Should().ContainSingle()
            .Which.Name.Should().Be("RequestId");
    }
}
