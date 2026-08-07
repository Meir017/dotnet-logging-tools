using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LoggerUsage.Tests;

public class LoggerUsageExtractorSummaryTests
{
    private const string Source = """
        using Microsoft.Extensions.Logging;

        namespace TestNamespace;

        public class TestClass
        {
            public void TestMethod(ILogger logger, int orderId)
            {
                logger.LogInformation("Processing order {OrderId}", orderId);
            }
        }
        """;

    [Fact]
    public async Task ExtractLoggerUsagesWithSolutionAsync_PopulatesSummary()
    {
        var compilation = await TestUtils.CreateCompilationAsync(Source);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        result.Summary.TotalParameterUsageCount.Should().Be(1);
        result.Summary.UniqueParameterNameCount.Should().Be(1);
        result.Summary.ParameterTypesByName["OrderId"].Should().ContainSingle().Which.Should().Be("int");
        result.Summary.CommonParameterNames.Should().ContainSingle()
            .Which.Name.Should().Be("OrderId");
    }

    [Fact]
    public async Task PublicEntrypoints_ReturnEquivalentPopulatedSummaries()
    {
        var sourceCompilation = await TestUtils.CreateCompilationAsync(Source);
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(
            ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Default,
                "TestProject",
                "TestProject",
                LanguageNames.CSharp,
                compilationOptions: sourceCompilation.Options,
                metadataReferences: sourceCompilation.References));
        workspace.AddDocument(project.Id, "TestDocument.cs", SourceText.From(Source));
        var compilation = await workspace.CurrentSolution.GetProject(project.Id)!.GetCompilationAsync();
        compilation.Should().NotBeNull();
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        var compilationResult = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation!);
        var workspaceResult = await extractor.ExtractLoggerUsagesAsync(workspace);

        compilationResult.Summary.TotalParameterUsageCount.Should().BeGreaterThan(0);
        workspaceResult.Summary.Should().BeEquivalentTo(compilationResult.Summary);
    }
}
