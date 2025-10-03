using LoggerUsage.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class TagProviderAttributeTests
{
    private static string CreateMockGeneratedCode(string className, string methodSignature) =>
        $@"
// Mock generated code:
partial class {className}
{{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void {methodSignature} {{ }}
}}";

    #region Basic TagProvider Tests

    [Fact]
    public async Task TagProvider_BasicProvider_ExtractsCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
    public string Email { get; set; }
}

public static class UserTagProvider
{
    public static void AddTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), nameof(UserTagProvider.AddTags))]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);

        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.Equal("user", logPropsParam.ParameterName);
        Assert.NotNull(logPropsParam.TagProvider);
        
        var tagProvider = logPropsParam.TagProvider;
        Assert.Equal("user", tagProvider.ParameterName);
        Assert.Equal("TestNamespace.UserTagProvider", tagProvider.ProviderTypeName);
        Assert.Equal("AddTags", tagProvider.ProviderMethodName);
        Assert.False(tagProvider.OmitReferenceName);
        Assert.True(tagProvider.IsValid);
        Assert.Null(tagProvider.ValidationMessage);
    }

    [Fact]
    public async Task TagProvider_WithOmitReferenceName_ExtractsCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class Request
{
    public string Method { get; set; }
    public string Path { get; set; }
}

public static class RequestTagProvider
{
    public static void AddTags(ITagCollector collector, Request request)
    {
        collector.Add(""method"", request.Method);
        collector.Add(""path"", request.Path);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Request processed""
    )]
    public static partial void LogRequest(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(RequestTagProvider), ""AddTags"", OmitReferenceName = true)]
        Request request);
}" + CreateMockGeneratedCode("Log", "LogRequest(ILogger logger, Request request)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);

        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.NotNull(logPropsParam.TagProvider);
        
        var tagProvider = logPropsParam.TagProvider;
        Assert.Equal("request", tagProvider.ParameterName);
        Assert.True(tagProvider.OmitReferenceName);
        Assert.True(tagProvider.IsValid);
    }

    #endregion

    #region Invalid Provider Tests

    [Fact]
    public async Task TagProvider_NonExistentMethod_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    public static void SomeOtherMethod(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""NonExistentMethod"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.NotNull(logPropsParam.TagProvider);
        
        var tagProvider = logPropsParam.TagProvider;
        Assert.False(tagProvider.IsValid);
        Assert.Contains("not found", tagProvider.ValidationMessage);
    }

    [Fact]
    public async Task TagProvider_NonStaticMethod_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public class UserTagProvider
{
    public void AddTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.NotNull(logPropsParam.TagProvider);
        
        var tagProvider = logPropsParam.TagProvider;
        Assert.False(tagProvider.IsValid);
        Assert.Contains("must be static", tagProvider.ValidationMessage);
    }

    [Fact]
    public async Task TagProvider_PrivateMethod_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    private static void AddTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.NotNull(logPropsParam.TagProvider);
        
        var tagProvider = logPropsParam.TagProvider;
        Assert.False(tagProvider.IsValid);
        Assert.Contains("must be public or internal", tagProvider.ValidationMessage);
    }

    [Fact]
    public async Task TagProvider_WrongReturnType_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    public static int AddTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
        return 0;
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.NotNull(logPropsParam.TagProvider);
        
        var tagProvider = logPropsParam.TagProvider;
        Assert.False(tagProvider.IsValid);
        Assert.Contains("must return void", tagProvider.ValidationMessage);
    }

    [Fact]
    public async Task TagProvider_WrongParameterCount_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    public static void AddTags(ITagCollector collector, User user, string extra)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.NotNull(logPropsParam.TagProvider);
        
        var tagProvider = logPropsParam.TagProvider;
        Assert.False(tagProvider.IsValid);
        Assert.Contains("must have exactly 2 parameters", tagProvider.ValidationMessage);
    }

    [Fact]
    public async Task TagProvider_WrongFirstParameterType_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    public static void AddTags(string wrongType, User user)
    {
        // Wrong first parameter type
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.NotNull(logPropsParam.TagProvider);
        
        var tagProvider = logPropsParam.TagProvider;
        Assert.False(tagProvider.IsValid);
        Assert.Contains("First parameter must be ITagCollector", tagProvider.ValidationMessage);
    }

    [Fact]
    public async Task TagProvider_WrongSecondParameterType_ReportsInvalid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    public static void AddTags(ITagCollector collector, string wrongType)
    {
        // Wrong second parameter type
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.NotNull(logPropsParam.TagProvider);
        
        var tagProvider = logPropsParam.TagProvider;
        Assert.False(tagProvider.IsValid);
        Assert.Contains("Second parameter must be", tagProvider.ValidationMessage);
    }

    #endregion

    #region Multiple Parameters Tests

    [Fact]
    public async Task TagProvider_MultipleParameters_ExtractsAllCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public class Request
{
    public string Method { get; set; }
}

public static class UserTagProvider
{
    public static void AddUserTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static class RequestTagProvider
{
    public static void AddRequestTags(ITagCollector collector, Request request)
    {
        collector.Add(""request.method"", request.Method);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User request processed""
    )]
    public static partial void LogUserRequest(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddUserTags"")]
        User user,
        [LogProperties]
        [TagProvider(typeof(RequestTagProvider), ""AddRequestTags"")]
        Request request);
}" + CreateMockGeneratedCode("Log", "LogUserRequest(ILogger logger, User user, Request request)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Equal(2, usage.LogPropertiesParameters.Count);

        var userParam = usage.LogPropertiesParameters.FirstOrDefault(p => p.ParameterName == "user");
        Assert.NotNull(userParam);
        Assert.NotNull(userParam.TagProvider);
        Assert.Equal("AddUserTags", userParam.TagProvider.ProviderMethodName);
        Assert.True(userParam.TagProvider.IsValid);

        var requestParam = usage.LogPropertiesParameters.FirstOrDefault(p => p.ParameterName == "request");
        Assert.NotNull(requestParam);
        Assert.NotNull(requestParam.TagProvider);
        Assert.Equal("AddRequestTags", requestParam.TagProvider.ProviderMethodName);
        Assert.True(requestParam.TagProvider.IsValid);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task TagProvider_InternalProviderMethod_IsValid()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
}

public static class UserTagProvider
{
    internal static void AddTags(ITagCollector collector, User user)
    {
        collector.Add(""user.name"", user.Name);
    }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        [TagProvider(typeof(UserTagProvider), ""AddTags"")]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.NotNull(logPropsParam.TagProvider);
        Assert.True(logPropsParam.TagProvider.IsValid);
    }

    [Fact]
    public async Task LogProperties_WithoutTagProvider_NoTagProviderInfo()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class User
{
    public string Name { get; set; }
    public string Email { get; set; }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User action performed""
    )]
    public static partial void LogUserAction(
        ILogger logger,
        [LogProperties]
        User user);
}" + CreateMockGeneratedCode("Log", "LogUserAction(ILogger logger, User user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.LogPropertiesParameters);
        
        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.Null(logPropsParam.TagProvider);
    }

    #endregion
}
