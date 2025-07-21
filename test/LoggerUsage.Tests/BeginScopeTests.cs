using LoggerUsage.Models;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

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
        var extractor = new LoggerUsageExtractor();

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
        var extractor = new LoggerUsageExtractor();

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
        var extractor = new LoggerUsageExtractor();

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
        var extractor = new LoggerUsageExtractor();

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
        var extractor = new LoggerUsageExtractor();

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

    [Theory]
    [MemberData(nameof(KeyValuePairBeginScopeTestCases))]
    public async Task BeginScope_KeyValuePairCollections_DetectedCorrectly(string testCode, int expectedParameterCount, List<MessageParameter> expectedParameters)
    {
        // Arrange
        var compilation = await TestUtils.CreateCompilationAsync(testCode);
        var extractor = new LoggerUsageExtractor();

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
}
