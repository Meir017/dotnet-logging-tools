using System.Text.Json;
using AwesomeAssertions;
using LoggerUsage.Models;
using LoggerUsage.ReportGenerator;
using Microsoft.Extensions.DependencyInjection;

namespace LoggerUsage.Tests;

public class SarifLoggerReportGeneratorTests
{
    [Fact]
    public void GenerateReport_WithParameterInconsistencies_ProducesDeterministicSarif()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), "logger-usage-sarif");
        var result = CreateExtractionResult(sourceRoot);
        var reversedResult = CreateExtractionResult(sourceRoot);
        reversedResult.Results.Reverse();
        new LoggerUsageSummarizer().PopulateSummary(reversedResult);
        var generator = CreateGenerator();
        var context = new ReportGenerationContext(sourceRoot);

        var first = generator.GenerateReport(result, context);
        var second = generator.GenerateReport(reversedResult, context);

        first.Should().Be(second);

        using var document = JsonDocument.Parse(first);
        var root = document.RootElement;
        root.GetProperty("version").GetString().Should().Be("2.1.0");
        root.TryGetProperty("generatedAt", out _).Should().BeFalse();

        var run = root.GetProperty("runs")[0];
        var rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");
        rules.GetArrayLength().Should().Be(2);
        rules[0].GetProperty("id").GetString().Should().Be("LUT001");
        rules[1].GetProperty("id").GetString().Should().Be("LUT002");

        var results = run.GetProperty("results");
        results.GetArrayLength().Should().Be(5);
        results.EnumerateArray()
            .Select(item => item.GetProperty("ruleId").GetString())
            .Should().Contain(["LUT001", "LUT002"]);

        foreach (var sarifResult in results.EnumerateArray())
        {
            var artifact = sarifResult
                .GetProperty("locations")[0]
                .GetProperty("physicalLocation")
                .GetProperty("artifactLocation");
            artifact.GetProperty("uri").GetString().Should().Be("src/Service.cs");
            artifact.GetProperty("uriBaseId").GetString().Should().Be("%SRCROOT%");
            sarifResult.GetProperty("partialFingerprints")
                .GetProperty("loggerUsage/v1")
                .GetString()
                .Should().MatchRegex("^[0-9a-f]{64}$");
        }
    }

    [Fact]
    public void GetReportGenerator_WithSarifExtension_ReturnsSarifGenerator()
    {
        var generator = CreateGenerator();

        var report = generator.GenerateReport(new LoggerUsageExtractionResult());

        using var document = JsonDocument.Parse(report);
        document.RootElement.GetProperty("version").GetString().Should().Be("2.1.0");
    }

    [Fact]
    public void GenerateReport_WithFileOutsideSourceRoot_Throws()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), "logger-usage-sarif-root");
        var otherRoot = Path.Combine(Path.GetTempPath(), "logger-usage-sarif-other");
        var result = CreateExtractionResult(otherRoot);
        var generator = CreateGenerator();

        var action = () => generator.GenerateReport(result, new ReportGenerationContext(sourceRoot));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the SARIF source root*");
    }

    private static ILoggerReportGenerator CreateGenerator()
    {
        var services = new ServiceCollection();
        services.AddLoggerUsageExtractor();
        return services.BuildServiceProvider()
            .GetRequiredService<ILoggerReportGeneratorFactory>()
            .GetReportGenerator(".sarif");
    }

    private static LoggerUsageExtractionResult CreateExtractionResult(string sourceRoot)
    {
        var filePath = Path.Combine(sourceRoot, "src", "Service.cs");
        var result = new LoggerUsageExtractionResult
        {
            Results =
            [
                CreateUsage(filePath, 10, "userId", "string"),
                CreateUsage(filePath, 20, "userId", "int"),
                CreateUsage(filePath, 30, "UserId", "string")
            ]
        };
        new LoggerUsageSummarizer().PopulateSummary(result);
        return result;
    }

    private static LoggerUsageInfo CreateUsage(
        string filePath,
        int line,
        string parameterName,
        string parameterType) =>
        new()
        {
            MethodName = "Log",
            MethodType = LoggerUsageMethodType.LoggerExtensions,
            MessageTemplate = "Processed {UserId}",
            Location = new MethodCallLocation
            {
                FilePath = filePath,
                StartLineNumber = line,
                EndLineNumber = line
            },
            MessageParameters =
            [
                new MessageParameter(parameterName, parameterType, "ParameterReference")
            ]
        };
}
