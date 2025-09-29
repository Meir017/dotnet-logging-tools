using LoggerUsage.Models;
using LoggerUsage.ReportGenerator;
using Microsoft.Extensions.DependencyInjection;

namespace LoggerUsage.Tests;

public class MarkdownLoggerReportGeneratorTests
{
    private readonly ILoggerReportGeneratorFactory _factory;

    public MarkdownLoggerReportGeneratorTests()
    {
        var services = new ServiceCollection();
        services.AddLoggerUsageExtractor();
        _factory = services.BuildServiceProvider().GetRequiredService<ILoggerReportGeneratorFactory>();
    }

    [Fact]
    public void GenerateReport_WithEmptyResults_ReturnsValidMarkdown()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".md");
        var result = new LoggerUsageExtractionResult
        {
            Results = [],
            Summary = new LoggerUsageExtractionSummary()
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("# Logger Usage Report", markdown);
        Assert.Contains("## 📊 Summary", markdown);
        Assert.Contains("*No logger usages found.*", markdown);
    }

    [Fact]
    public void GenerateReport_WithSampleResults_ReturnsValidMarkdown()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".markdown");
        var result = new LoggerUsageExtractionResult
        {
            Results = [
                new LoggerUsageInfo
                {
                    MethodName = "LogInformation",
                    MethodType = LoggerUsageMethodType.LoggerMethod,
                    LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                    MessageTemplate = "User {UserId} performed action {Action}",
                    Location = new MethodCallLocation
                    {
                        FilePath = @"C:\Source\MyApp\Controllers\UserController.cs",
                        StartLineNumber = 42,
                        EndLineNumber = 42
                    },
                    MessageParameters = [
                        new MessageParameter("UserId", "int", "Argument"),
                        new MessageParameter("Action", "string", "Argument")
                    ],
                    EventId = new EventIdDetails(ConstantOrReference.Constant(1001), ConstantOrReference.Constant("UserAction"))
                }
            ],
            Summary = new LoggerUsageExtractionSummary
            {
                UniqueParameterNameCount = 2,
                TotalParameterUsageCount = 2,
                CommonParameterNames = [
                    new LoggerUsageExtractionSummary.CommonParameterNameInfo("UserId", 1, "int"),
                    new LoggerUsageExtractionSummary.CommonParameterNameInfo("Action", 1, "string")
                ]
            }
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("# Logger Usage Report", markdown);
        Assert.Contains("## 📊 Summary", markdown);
        Assert.Contains("| Total Log Usages | 1 |", markdown);
        Assert.Contains("| Unique Parameter Names | 2 |", markdown);
        Assert.Contains("ℹ️ Line 43: Information - LoggerMethod", markdown);
        Assert.Contains("User {UserId} performed action {Action}", markdown);
        Assert.Contains("| `UserId` | `int` | Argument |", markdown);
        Assert.Contains("| `Action` | `string` | Argument |", markdown);
        Assert.Contains("1001 (UserAction)", markdown);
        Assert.Contains("UserController.cs", markdown);
    }

    [Fact]
    public void GenerateReport_WithParameterInconsistencies_IncludesInconsistencySection()
    {
        // Arrange
        var generator = _factory.GetReportGenerator(".md");
        var result = new LoggerUsageExtractionResult
        {
            Results = [],
            Summary = new LoggerUsageExtractionSummary
            {
                InconsistentParameterNames = [
                    new LoggerUsageExtractionSummary.InconsistentParameterNameInfo(
                        Names: [
                            new LoggerUsageExtractionSummary.NameTypePair("userId", "int"),
                            new LoggerUsageExtractionSummary.NameTypePair("UserId", "string")
                        ],
                        IssueTypes: ["Case inconsistency", "Type mismatch"]
                    )
                ]
            }
        };

        // Act
        var markdown = generator.GenerateReport(result);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("## ⚠️ Parameter Inconsistencies", markdown);
        Assert.Contains("| `userId` | `int` |", markdown);
        Assert.Contains("| `UserId` | `string` |", markdown);
        Assert.Contains("- Case inconsistency", markdown);
        Assert.Contains("- Type mismatch", markdown);
    }
}
