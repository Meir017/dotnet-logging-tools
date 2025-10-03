using LoggerUsage.Models;
using Xunit;

namespace LoggerUsage.Tests;

public class DataClassificationTests
{
    /// <summary>
    /// Helper to create mock generated code for LoggerMessage methods
    /// </summary>
    private static string CreateMockGeneratedCode(string className, string methodSignature)
    {
        return $@"

// Mock generated code
namespace TestNamespace
{{
    public static partial class {className}
    {{
        public static partial void {methodSignature}
        {{
            // Generated implementation
        }}
    }}
}}";
    }

    #region Graceful Degradation Tests - Most Important

    [Fact]
    public async Task LoggerMessage_WithoutCompliancePackage_DoesNotCrash()
    {
        // Arrange - no DataClassificationAttribute defined
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace
{
    public static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = ""User name: {Name}""
        )]
        public static partial void LogUser(
            ILogger logger,
            string Name);
    }
}" + CreateMockGeneratedCode("Log", "LogUser(ILogger logger, string Name)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert - should work fine without the compliance package
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        Assert.Single(usage.MessageParameters);
        Assert.Null(usage.MessageParameters[0].DataClassification);
    }

    [Fact]
    public async Task LoggerMessage_WithoutClassification_HasNullDataClassification()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace
{
    public static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = ""User name: {Name}""
        )]
        public static partial void LogUser(
            ILogger logger,
            string Name);
    }
}" + CreateMockGeneratedCode("Log", "LogUser(ILogger logger, string Name)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        var usage = loggerUsages.Results[0];
        Assert.Single(usage.MessageParameters);
        
        var parameter = usage.MessageParameters[0];
        Assert.Equal("Name", parameter.Name);
        Assert.Null(parameter.DataClassification);
    }

    [Fact]
    public async Task LoggerMessage_LogPropertiesWithoutClassification_PropertiesHaveNullClassification()
    {
        // Arrange
        var code = @"using Microsoft.Extensions.Logging;

namespace TestNamespace
{
    public class UserData
    {
        public string UserName { get; set; }
        public string Email { get; set; }
    }

    public static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = ""User data""
        )]
        public static partial void LogUser(
            ILogger logger,
            [LogProperties] UserData user);
    }
}" + CreateMockGeneratedCode("Log", "LogUser(ILogger logger, UserData user)");

        var compilation = await TestUtils.CreateCompilationAsync(code);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var loggerUsages = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        // Assert
        Assert.NotNull(loggerUsages);
        Assert.Single(loggerUsages.Results);
        
        var usage = loggerUsages.Results[0] as LoggerMessageUsageInfo;
        Assert.NotNull(usage);
        Assert.Single(usage.LogPropertiesParameters);
        
        var logPropertiesParam = usage.LogPropertiesParameters[0];
        Assert.Equal(2, logPropertiesParam.Properties.Count);
        
        // Both properties should have null classification
        foreach (var prop in logPropertiesParam.Properties)
        {
            Assert.Null(prop.DataClassification);
        }
    }

    #endregion

    // Note: We cannot easily test with DataClassificationAttribute in unit tests because:
    // 1. It requires the Microsoft.Extensions.Compliance.Classification package
    // 2. Mocking System.Attribute inheritance in test code causes compilation issues
    // 3. The feature is designed to gracefully handle missing packages
    //
    // The important functionality tested here is:
    // - The code doesn't crash when DataClassificationAttribute is not available
    // - Parameters and properties correctly have null DataClassification when no attributes are present
    // - The feature is opt-in and non-breaking
    //
    // Full integration testing with actual DataClassificationAttribute should be done
    // in a separate test project that references the compliance packages.
}

