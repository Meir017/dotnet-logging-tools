using LoggerUsage.Models;
using Microsoft.Extensions.Logging;
using TUnit.Core;

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
    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Equal(2, loggerUsages.Results.Count);
        
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal(nameof(ILogger.BeginScope), beginScopeUsage.MethodName);
        Assert.Equal("Processing request {RequestId}", beginScopeUsage.MessageTemplate);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Equal(2, loggerUsages.Results.Count);
        
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal(nameof(ILogger.BeginScope), beginScopeUsage.MethodName);
        Assert.Null(beginScopeUsage.MessageTemplate);
    }

    [Test]
    [MethodDataSource(nameof(BeginScopeMessageParameterCases))]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        
        var parameters = beginScopeUsage.MessageParameters;
        Assert.Equal(expectedParameters.Count, parameters.Count);
        Assert.Equal(expectedParameters, parameters);
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

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal("Processing request {RequestId}", beginScopeUsage.MessageTemplate);
        Assert.Single(beginScopeUsage.MessageParameters);
        Assert.Equal("RequestId", beginScopeUsage.MessageParameters[0].Name);
        Assert.Equal("int", beginScopeUsage.MessageParameters[0].Type);
        Assert.Equal("Constant", beginScopeUsage.MessageParameters[0].Kind);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal("User {UserId} processing request {RequestId} with status {Status}", beginScopeUsage.MessageTemplate);
        Assert.Equal(3, beginScopeUsage.MessageParameters.Count);
        
        Assert.Equal("UserId", beginScopeUsage.MessageParameters[0].Name);
        Assert.Equal("string", beginScopeUsage.MessageParameters[0].Type);
        Assert.Equal("Constant", beginScopeUsage.MessageParameters[0].Kind);
        
        Assert.Equal("RequestId", beginScopeUsage.MessageParameters[1].Name);
        Assert.Equal("int", beginScopeUsage.MessageParameters[1].Type);
        Assert.Equal("Constant", beginScopeUsage.MessageParameters[1].Kind);
        
        Assert.Equal("Status", beginScopeUsage.MessageParameters[2].Name);
        Assert.Equal("bool", beginScopeUsage.MessageParameters[2].Type);
        Assert.Equal("Constant", beginScopeUsage.MessageParameters[2].Kind);
    }

    [Test]
    [MethodDataSource(nameof(KeyValuePairBeginScopeTestCases))]
    public async Task BeginScope_KeyValuePairCollections_DetectedCorrectly(string testCode, int expectedParameterCount, List<MessageParameter> expectedParameters)
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(testCode);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Equal(2, loggerUsages.Results.Count);
        
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal(nameof(ILogger.BeginScope), beginScopeUsage.MethodName);
        Assert.Null(beginScopeUsage.MessageTemplate);

        // Assert.Skip("TODO: parse KeyValuePair collections correctly");
        Assert.Equal(expectedParameterCount, beginScopeUsage.MessageParameters.Count);
        
        for (int i = 0; i < expectedParameters.Count; i++)
        {
            Assert.Equal(expectedParameters[i].Name, beginScopeUsage.MessageParameters[i].Name);
            Assert.Equal(expectedParameters[i].Type, beginScopeUsage.MessageParameters[i].Type);
            Assert.Equal(expectedParameters[i].Kind, beginScopeUsage.MessageParameters[i].Kind);
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

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal(nameof(ILogger.BeginScope), beginScopeUsage.MethodName);
        Assert.Null(beginScopeUsage.MessageTemplate);
        
        // Should extract parameters from anonymous object properties
        Assert.Equal(3, beginScopeUsage.MessageParameters.Count);
        
        var userIdParam = beginScopeUsage.MessageParameters.FirstOrDefault(p => p.Name == "UserId");
        Assert.NotNull(userIdParam);
        Assert.Equal("string", userIdParam.Type);
        Assert.Equal("ParameterReference", userIdParam.Kind);
        
        var requestIdParam = beginScopeUsage.MessageParameters.FirstOrDefault(p => p.Name == "RequestId");
        Assert.NotNull(requestIdParam);
        Assert.Equal("int", requestIdParam.Type);
        Assert.Equal("ParameterReference", requestIdParam.Kind);
        
        var timestampParam = beginScopeUsage.MessageParameters.FirstOrDefault(p => p.Name == "Timestamp");
        Assert.NotNull(timestampParam);
        Assert.Equal("System.DateTime", timestampParam.Type);
        Assert.Equal("PropertyReference", timestampParam.Kind);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal(4, beginScopeUsage.MessageParameters.Count);
        
        var operationIdParam = beginScopeUsage.MessageParameters.FirstOrDefault(p => p.Name == "OperationId");
        Assert.NotNull(operationIdParam);
        Assert.Equal("System.Guid", operationIdParam.Type);
        
        var dataParam = beginScopeUsage.MessageParameters.FirstOrDefault(p => p.Name == "Data");
        Assert.NotNull(dataParam);
        
        var flagsParam = beginScopeUsage.MessageParameters.FirstOrDefault(p => p.Name == "Flags");
        Assert.NotNull(flagsParam);
        Assert.Equal("System.Collections.Generic.List<string>", flagsParam.Type);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal(3, beginScopeUsage.MessageParameters.Count);
        
        var optionalParam = beginScopeUsage.MessageParameters.FirstOrDefault(p => p.Name == "OptionalData");
        Assert.NotNull(optionalParam);
        Assert.Equal("object", optionalParam.Type);
        
        var requiredParam = beginScopeUsage.MessageParameters.FirstOrDefault(p => p.Name == "RequiredData");
        Assert.NotNull(requiredParam);
        Assert.Equal("string", requiredParam.Type);
        
        var nullableParam = beginScopeUsage.MessageParameters.FirstOrDefault(p => p.Name == "NullableInt");
        Assert.NotNull(nullableParam);
        Assert.Equal("object", nullableParam.Type);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal("Processing {Operation} for user {UserId} at {Timestamp}", beginScopeUsage.MessageTemplate);
        Assert.Equal(3, beginScopeUsage.MessageParameters.Count);
        
        Assert.Equal("Operation", beginScopeUsage.MessageParameters[0].Name);
        Assert.Equal("string", beginScopeUsage.MessageParameters[0].Type);
        Assert.Equal("ParameterReference", beginScopeUsage.MessageParameters[0].Kind);
        
        Assert.Equal("UserId", beginScopeUsage.MessageParameters[1].Name);
        Assert.Equal("string", beginScopeUsage.MessageParameters[1].Type);
        Assert.Equal("ParameterReference", beginScopeUsage.MessageParameters[1].Kind);
        
        Assert.Equal("Timestamp", beginScopeUsage.MessageParameters[2].Name);
        Assert.Equal("System.DateTime", beginScopeUsage.MessageParameters[2].Type);
        Assert.Equal("PropertyReference", beginScopeUsage.MessageParameters[2].Kind);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal("Request {RequestId} from {Source}", beginScopeUsage.MessageTemplate);
        Assert.Equal(2, beginScopeUsage.MessageParameters.Count);
        
        Assert.Equal("RequestId", beginScopeUsage.MessageParameters[0].Name);
        Assert.Equal("string", beginScopeUsage.MessageParameters[0].Type);
        Assert.Equal("ParameterReference", beginScopeUsage.MessageParameters[0].Kind);
        
        Assert.Equal("Source", beginScopeUsage.MessageParameters[1].Name);
        Assert.Equal("string", beginScopeUsage.MessageParameters[1].Type);
        Assert.Equal("ParameterReference", beginScopeUsage.MessageParameters[1].Kind);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal("Complex operation {Op} with data {Data} and flags {Flags}", beginScopeUsage.MessageTemplate);
        Assert.Equal(3, beginScopeUsage.MessageParameters.Count);
        
        Assert.Equal("Op", beginScopeUsage.MessageParameters[0].Name);
        Assert.Equal("string", beginScopeUsage.MessageParameters[0].Type);
        Assert.Equal("ParameterReference", beginScopeUsage.MessageParameters[0].Kind);
        
        Assert.Equal("Data", beginScopeUsage.MessageParameters[1].Name);
        Assert.Equal("Flags", beginScopeUsage.MessageParameters[2].Name);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        // Message template should be null when not a literal string
        Assert.Null(beginScopeUsage.MessageTemplate);
        // Extension methods with non-literal templates don't extract parameters
        Assert.Empty(beginScopeUsage.MessageParameters);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal(nameof(ILogger.BeginScope), beginScopeUsage.MethodName);
        Assert.Null(beginScopeUsage.MessageTemplate);
        // Parameter extraction from local variable reference
        Assert.Single(beginScopeUsage.MessageParameters);
        Assert.Equal("<scopeData>", beginScopeUsage.MessageParameters[0].Name);
        Assert.Equal("LocalReference", beginScopeUsage.MessageParameters[0].Kind);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal(nameof(ILogger.BeginScope), beginScopeUsage.MethodName);
        Assert.Null(beginScopeUsage.MessageTemplate);
        // Parameter extraction from field reference
        Assert.Single(beginScopeUsage.MessageParameters);
        Assert.Equal("<_defaultScope>", beginScopeUsage.MessageParameters[0].Name);
        Assert.Equal("FieldReference", beginScopeUsage.MessageParameters[0].Kind);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal(nameof(ILogger.BeginScope), beginScopeUsage.MethodName);
        Assert.Null(beginScopeUsage.MessageTemplate);
        // Method invocation parameter extraction is not currently implemented
        Assert.Empty(beginScopeUsage.MessageParameters);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal(nameof(ILogger.BeginScope), beginScopeUsage.MethodName);
        Assert.Null(beginScopeUsage.MessageTemplate);
        // Currently extracts the local variable reference, not dictionary contents
        Assert.Single(beginScopeUsage.MessageParameters);
        
        var firstParam = beginScopeUsage.MessageParameters[0];
        Assert.Equal("<scope>", firstParam.Name);
        Assert.Equal("LocalReference", firstParam.Kind);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        Assert.Equal("", beginScopeUsage.MessageTemplate);
        Assert.Empty(beginScopeUsage.MessageParameters);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsage = loggerUsages.Results.FirstOrDefault(r => r.MethodType == LoggerUsageMethodType.BeginScope);
        Assert.NotNull(beginScopeUsage);
        // Should handle null template gracefully
        Assert.Null(beginScopeUsage.MessageTemplate);
        // Core methods don't extract simple variable references
        Assert.Empty(beginScopeUsage.MessageParameters);
    }

    [Test]
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
        var loggerUsages = extractor.ExtractLoggerUsages(compilation);

        // Assert
        var beginScopeUsages = loggerUsages.Results.Where(r => r.MethodType == LoggerUsageMethodType.BeginScope).ToList();
        Assert.Equal(2, beginScopeUsages.Count);
        
        var userScope = beginScopeUsages.FirstOrDefault(s => s.MessageTemplate?.Contains("UserId") == true);
        Assert.NotNull(userScope);
        Assert.Equal("User {UserId}", userScope.MessageTemplate);
        Assert.Single(userScope.MessageParameters);
        Assert.Equal("UserId", userScope.MessageParameters[0].Name);
        
        var requestScope = beginScopeUsages.FirstOrDefault(s => s.MessageTemplate?.Contains("RequestId") == true);
        Assert.NotNull(requestScope);
        Assert.Equal("Request {RequestId}", requestScope.MessageTemplate);
        Assert.Single(requestScope.MessageParameters);
        Assert.Equal("RequestId", requestScope.MessageParameters[0].Name);
    }
}
