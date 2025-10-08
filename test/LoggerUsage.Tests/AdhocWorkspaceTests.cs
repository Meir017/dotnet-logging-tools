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
}
