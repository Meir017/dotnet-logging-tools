using LoggerUsage.Models;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

public class TagNameAttributeTests
{
    private static string CreateMockGeneratedCode(string className, string methodSignature) =>
        $@"
// Mock generated code:
partial class {className}
{{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""Microsoft.Gen.Logging"", ""9.5.0.0"")]
    public static partial void {methodSignature} {{ }}
}}";

    [Fact]
    public async Task LoggerMessage_ParameterWithTagName_ExtractsCustomTagName()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User logged in: {userName}""
    )]
    public static partial void LogUserLogin(
        ILogger logger,
        [TagName(""user.name"")] string userName);
}" + CreateMockGeneratedCode("Log", "LogUserLogin(ILogger logger, string userName)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Equal("LogUserLogin", usage.MethodName);
        Assert.Single(usage.MessageParameters);

        var parameter = usage.MessageParameters[0];
        Assert.Equal("userName", parameter.Name);
        Assert.Equal("string", parameter.Type);
        Assert.Equal("user.name", parameter.CustomTagName);
    }

    [Fact]
    public async Task LoggerMessage_MultipleParametersWithTagName_ExtractsAllCustomTagNames()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Request: {requestId}, User: {userId}""
    )]
    public static partial void LogRequest(
        ILogger logger,
        [TagName(""request.id"")] string requestId,
        [TagName(""user.id"")] int userId);
}" + CreateMockGeneratedCode("Log", "LogRequest(ILogger logger, string requestId, int userId)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Equal("LogRequest", usage.MethodName);
        Assert.Equal(2, usage.MessageParameters.Count);

        var requestIdParam = usage.MessageParameters[0];
        Assert.Equal("requestId", requestIdParam.Name);
        Assert.Equal("request.id", requestIdParam.CustomTagName);

        var userIdParam = usage.MessageParameters[1];
        Assert.Equal("userId", userIdParam.Name);
        Assert.Equal("user.id", userIdParam.CustomTagName);
    }

    [Fact]
    public async Task LoggerMessage_MixedParametersWithAndWithoutTagName_ExtractsCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Request: {requestId}, Status: {status}""
    )]
    public static partial void LogRequest(
        ILogger logger,
        [TagName(""request.id"")] string requestId,
        string status);
}" + CreateMockGeneratedCode("Log", "LogRequest(ILogger logger, string requestId, string status)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Equal(2, usage.MessageParameters.Count);

        var requestIdParam = usage.MessageParameters[0];
        Assert.Equal("requestId", requestIdParam.Name);
        Assert.Equal("request.id", requestIdParam.CustomTagName);

        var statusParam = usage.MessageParameters[1];
        Assert.Equal("status", statusParam.Name);
        Assert.Null(statusParam.CustomTagName); // No TagName attribute
    }

    [Fact]
    public async Task LoggerMessage_PropertyWithTagName_ExtractsCustomTagName()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class UserInfo
{
    [TagName(""user.id"")]
    public string UserId { get; set; }
    
    [TagName(""user.display_name"")]
    public string DisplayName { get; set; }
    
    public string Email { get; set; }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User logged in""
    )]
    public static partial void LogUser(
        ILogger logger,
        [LogProperties] UserInfo user);
}" + CreateMockGeneratedCode("Log", "LogUser(ILogger logger, UserInfo user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Equal("LogUser", usage.MethodName);
        Assert.Single(usage.LogPropertiesParameters);

        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.Equal("user", logPropsParam.ParameterName);
        Assert.Equal(3, logPropsParam.Properties.Count);

        // Verify properties with TagName
        var userIdProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "UserId");
        Assert.NotNull(userIdProp);
        Assert.Equal("user.id", userIdProp.CustomTagName);

        var displayNameProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "DisplayName");
        Assert.NotNull(displayNameProp);
        Assert.Equal("user.display_name", displayNameProp.CustomTagName);

        // Verify property without TagName
        var emailProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "Email");
        Assert.NotNull(emailProp);
        Assert.Null(emailProp.CustomTagName);
    }

    [Fact]
    public async Task LoggerMessage_NestedPropertiesWithTagName_ExtractsCustomTagNames()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class Address
{
    [TagName(""address.street"")]
    public string Street { get; set; }
    
    public string City { get; set; }
}

public class UserInfo
{
    [TagName(""user.id"")]
    public string UserId { get; set; }
    
    public Address Address { get; set; }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""User details""
    )]
    public static partial void LogUser(
        ILogger logger,
        [LogProperties(Transitive = true)] UserInfo user);
}" + CreateMockGeneratedCode("Log", "LogUser(ILogger logger, UserInfo user)");

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
        Assert.Equal(2, logPropsParam.Properties.Count);

        // Verify top-level property with TagName
        var userIdProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "UserId");
        Assert.NotNull(userIdProp);
        Assert.Equal("user.id", userIdProp.CustomTagName);

        // Verify nested property with TagName
        var addressProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "Address");
        Assert.NotNull(addressProp);
        Assert.NotNull(addressProp.NestedProperties);
        Assert.Equal(2, addressProp.NestedProperties.Count);

        var streetProp = addressProp.NestedProperties.FirstOrDefault(p => p.OriginalName == "Street");
        Assert.NotNull(streetProp);
        Assert.Equal("address.street", streetProp.CustomTagName);

        var cityProp = addressProp.NestedProperties.FirstOrDefault(p => p.OriginalName == "City");
        Assert.NotNull(cityProp);
        Assert.Null(cityProp.CustomTagName);
    }

    [Fact]
    public async Task LoggerMessage_CombinedParameterAndPropertyTagName_ExtractsBoth()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public class RequestDetails
{
    [TagName(""request.method"")]
    public string Method { get; set; }
    
    [TagName(""request.path"")]
    public string Path { get; set; }
}

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Request processed: {requestId}""
    )]
    public static partial void LogRequest(
        ILogger logger,
        [TagName(""request.id"")] string requestId,
        [LogProperties] RequestDetails details);
}" + CreateMockGeneratedCode("Log", "LogRequest(ILogger logger, string requestId, RequestDetails details)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        
        // Verify parameter with TagName
        Assert.Single(usage.MessageParameters);
        var requestIdParam = usage.MessageParameters[0];
        Assert.Equal("requestId", requestIdParam.Name);
        Assert.Equal("request.id", requestIdParam.CustomTagName);

        // Verify properties with TagName
        Assert.Single(usage.LogPropertiesParameters);
        var logPropsParam = usage.LogPropertiesParameters[0];
        Assert.Equal(2, logPropsParam.Properties.Count);

        var methodProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "Method");
        Assert.NotNull(methodProp);
        Assert.Equal("request.method", methodProp.CustomTagName);

        var pathProp = logPropsParam.Properties.FirstOrDefault(p => p.OriginalName == "Path");
        Assert.NotNull(pathProp);
        Assert.Equal("request.path", pathProp.CustomTagName);
    }

    [Fact]
    public async Task LoggerMessage_TagNameWithSpecialCharacters_ExtractsCorrectly()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

#pragma warning disable EXTEXP0003

namespace TestNamespace;

public static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = ""Event: {eventType}""
    )]
    public static partial void LogEvent(
        ILogger logger,
        [TagName(""event.type-category"")] string eventType);
}" + CreateMockGeneratedCode("Log", "LogEvent(ILogger logger, string eventType)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);

        var usage = Assert.IsType<LoggerMessageUsageInfo>(loggerUsages.Results[0]);
        Assert.Single(usage.MessageParameters);

        var parameter = usage.MessageParameters[0];
        Assert.Equal("eventType", parameter.Name);
        Assert.Equal("event.type-category", parameter.CustomTagName);
    }
}
