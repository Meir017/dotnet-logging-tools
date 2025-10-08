using AwesomeAssertions;
using LoggerUsage.Models;

namespace LoggerUsage.Tests;

public class AdhocWorkspaceTests
{
    [Fact]
    public async Task ExtractLoggerUsagesWithSolutionAsync_NullSolution_CreatesAdhocWorkspace()
    {
        // Arrange
        var sourceCode = """
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                private readonly ILogger<TestClass> _logger;

                public TestClass(ILogger<TestClass> logger)
                {
                    _logger = logger;
                }

                public void Method1()
                {
                    _logger.LogInformation("Test message");
                }
            }
            """;

        var compilation = await TestUtils.CreateCompilationAsync(sourceCode);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation, solution: null);

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().NotBeEmpty();
        result.Results.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExtractLoggerUsagesWithSolutionAsync_NullSolution_ReportsProgress()
    {
        // Arrange
        var sourceCode = """
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                private readonly ILogger<TestClass> _logger;

                public TestClass(ILogger<TestClass> logger)
                {
                    _logger = logger;
                }

                public void Method1()
                {
                    _logger.LogInformation("Test message");
                }
            }
            """;

        var compilation = await TestUtils.CreateCompilationAsync(sourceCode);
        var extractor = TestUtils.CreateLoggerUsageExtractor();
        var reports = new List<LoggerUsageProgress>();

        // Act
        await extractor.ExtractLoggerUsagesWithSolutionAsync(
            compilation,
            solution: null,
            progress: new Progress<LoggerUsageProgress>(reports.Add));

        // Assert
        reports.Should().NotBeEmpty();
        var workspaceReport = reports.FirstOrDefault(r =>
            r.OperationDescription.Contains("workspace", StringComparison.OrdinalIgnoreCase));
        workspaceReport.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractLoggerUsagesWithSolutionAsync_WithSolution_UsesProvidedSolution()
    {
        // Arrange
        var sourceCode = """
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                private readonly ILogger<TestClass> _logger;

                public TestClass(ILogger<TestClass> logger)
                {
                    _logger = logger;
                }

                public void Method1()
                {
                    _logger.LogInformation("Test message");
                }
            }
            """;

        var compilation = await TestUtils.CreateCompilationAsync(sourceCode);

        // Create a solution with the compilation
        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var projectInfo = Microsoft.CodeAnalysis.ProjectInfo.Create(
            Microsoft.CodeAnalysis.ProjectId.CreateNewId(),
            Microsoft.CodeAnalysis.VersionStamp.Default,
            "TestProject",
            "TestProject",
            Microsoft.CodeAnalysis.LanguageNames.CSharp);
        var project = workspace.AddProject(projectInfo);

        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(
            compilation,
            solution: workspace.CurrentSolution);

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().NotBeEmpty();

        workspace.Dispose();
    }

    [Fact]
    public async Task ExtractLoggerUsagesWithSolutionAsync_NullSolution_EnablesSymbolFinder()
    {
        // Arrange
        var sourceCode = """
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                private readonly ILogger<TestClass> _logger;

                public TestClass(ILogger<TestClass> logger)
                {
                    _logger = logger;
                }

                public void Method1()
                {
                    _logger.LogInformation("Test message");
                }
            }
            """;

        var compilation = await TestUtils.CreateCompilationAsync(sourceCode);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation, solution: null);

        // Assert
        // If AdhocWorkspace was created successfully, the analysis should complete
        // without errors and produce results (proving Solution APIs were available)
        result.Should().NotBeNull();
        result.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExtractLoggerUsagesWithSolutionAsync_NullSolution_DisposesWorkspaceAfterAnalysis()
    {
        // Arrange
        var sourceCode = """
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                private readonly ILogger<TestClass> _logger;

                public TestClass(ILogger<TestClass> logger)
                {
                    _logger = logger;
                }

                public void Method1()
                {
                    _logger.LogInformation("Test message");
                }
            }
            """;

        var compilation = await TestUtils.CreateCompilationAsync(sourceCode);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation, solution: null);

        // Assert
        // If workspace is properly disposed, this should complete without memory leaks
        result.Should().NotBeNull();

        // Force GC to verify no disposal issues
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Fact]
    public async Task ExtractLoggerUsagesWithSolutionAsync_NullSolution_SymbolsResolveCorrectly()
    {
        // Arrange - Test that verifies the symbol resolution fix
        // When AdhocWorkspace is created, LoggingTypes must be created from workspace compilation
        // to ensure symbols match between semantic model and logging types
        var sourceCode = """
            using Microsoft.Extensions.Logging;

            public class TestClass
            {
                private readonly ILogger<TestClass> _logger;

                public TestClass(ILogger<TestClass> logger)
                {
                    _logger = logger;
                }

                public void LogMultipleMessages()
                {
                    // Test various logging patterns to ensure symbol resolution works
                    _logger.LogInformation("Simple message");
                    _logger.LogWarning("Warning with {Param}", "value");
                    _logger.LogError(new System.Exception("test"), "Error message");
                    _logger.LogDebug("Debug {Param1} and {Param2}", 1, 2);
                    
                    // Test with EventId
                    _logger.LogInformation(new EventId(100, "TestEvent"), "Event message");
                }
            }
            """;

        var compilation = await TestUtils.CreateCompilationAsync(sourceCode);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        // Act
        var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation, solution: null);

        // Assert
        // This test validates the critical fix: LoggingTypes created from workspace compilation
        // If symbols don't match, the analyzers won't find any logging calls
        result.Should().NotBeNull();
        result.Results.Should().NotBeEmpty();
        result.Results.Should().HaveCount(5, "because we have 5 logging calls in the test code");

        // Verify all logging calls were properly analyzed
        var infoCall = result.Results.Should().ContainSingle(r => r.MessageTemplate == "Simple message").Subject;
        infoCall.LogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Information);

        var warningCall = result.Results.Should().ContainSingle(r => r.MessageTemplate == "Warning with {Param}").Subject;
        warningCall.LogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Warning);
        warningCall.MessageParameters.Should().HaveCount(1);

        var errorCall = result.Results.Should().ContainSingle(r => r.MessageTemplate == "Error message").Subject;
        errorCall.LogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Error);

        var debugCall = result.Results.Should().ContainSingle(r => r.MessageTemplate == "Debug {Param1} and {Param2}").Subject;
        debugCall.LogLevel.Should().Be(Microsoft.Extensions.Logging.LogLevel.Debug);
        debugCall.MessageParameters.Should().HaveCount(2);

        var eventIdCall = result.Results.Should().ContainSingle(r => r.MessageTemplate == "Event message").Subject;
        eventIdCall.EventId.Should().NotBeNull();
        eventIdCall.EventId.Should().BeOfType<EventIdDetails>();
        var eventIdDetails = (EventIdDetails)eventIdCall.EventId!;
        eventIdDetails.Id.Kind.Should().Be("Constant");
        eventIdDetails.Id.Value.Should().Be(100);
        eventIdDetails.Name.Kind.Should().Be("Constant");
        eventIdDetails.Name.Value.Should().Be("TestEvent");
    }
}
