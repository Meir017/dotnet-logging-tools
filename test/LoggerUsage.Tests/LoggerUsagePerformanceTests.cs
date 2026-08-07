using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;

namespace LoggerUsage.Tests;

public class LoggerUsagePerformanceTests(ITestOutputHelper output)
{
    private const int TargetLineCount = 5_000;
    private const int LoggingCallCount = 250;
    private const int ProjectFileCount = 100;
    private const double ConstitutionalTargetMilliseconds = 500;
    private const long ConstitutionalMemoryBytes = 500L * 1024 * 1024;

    [Fact]
    public async Task ExtractLoggerUsagesWithSolutionAsync_NearFiveThousandLines_WarmedMedianMeetsLatencyContract()
    {
        var source = CreateLargeSource();
        SourceText.From(source).Lines.Count.Should().Be(TargetLineCount);

        var compilation = await TestUtils.CreateCompilationAsync(source);
        var extractor = TestUtils.CreateLoggerUsageExtractor();

        var warmupResult = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);
        AssertComplete(warmupResult.Results.Select(result => result.MessageTemplate));

        var samples = new double[3];
        for (var index = 0; index < samples.Length; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);
            stopwatch.Stop();

            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
            AssertComplete(result.Results.Select(usage => usage.MessageTemplate));
        }

        Array.Sort(samples);
        var medianMilliseconds = samples[1];
        output.WriteLine(
            "Warmed extraction samples: {0}; median: {1:F1} ms",
            string.Join(", ", samples.Select(sample => $"{sample:F1} ms")),
            medianMilliseconds);

        medianMilliseconds.Should().BeLessThan(
            ConstitutionalTargetMilliseconds,
            "the warmed median rejects one noisy CI scheduling sample while enforcing the constitutional contract");
    }

    [Fact]
    public async Task ExtractLoggerUsagesWithSolutionAsync_HundredFiles_MeetsMemoryContract()
    {
        var sources = Enumerable.Range(0, ProjectFileCount)
            .Select(index => (
                $$"""
                using Microsoft.Extensions.Logging;

                namespace PerformanceBaseline;

                public sealed class LoggingService{{index}}
                {
                    public void Log(ILogger logger) =>
                        logger.LogInformation("Service {Index}", {{index}});
                }
                """,
                $"LoggingService{index}.cs"))
            .ToArray();
        var compilation = await TestUtils.CreateCompilationAsync(sources);
        var extractor = TestUtils.CreateLoggerUsageExtractor();
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);

        var result = await extractor.ExtractLoggerUsagesWithSolutionAsync(compilation);

        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        output.WriteLine("Hundred-file extraction allocated {0:N0} bytes", allocatedBytes);
        result.Results.Should().HaveCount(ProjectFileCount);
        allocatedBytes.Should().BeLessThan(ConstitutionalMemoryBytes);
    }

    private static string CreateLargeSource()
    {
        var lines = new List<string>(TargetLineCount)
        {
            "using Microsoft.Extensions.Logging;",
            "namespace PerformanceBaseline;",
            "public sealed class LargeLoggingClass",
            "{",
            "    public void LogAll(ILogger logger)",
            "    {"
        };

        for (var index = 0; index < LoggingCallCount; index++)
        {
            lines.Add($"        logger.LogInformation(\"Performance message {{Index}}\", {index});");
        }

        while (lines.Count < TargetLineCount - 3)
        {
            lines.Add("        _ = 0;");
        }

        lines.Add("    }");
        lines.Add("}");
        lines.Add(string.Empty);

        return string.Join(Environment.NewLine, lines);
    }

    private static void AssertComplete(IEnumerable<string?> messageTemplates)
    {
        var templates = messageTemplates.ToList();
        templates.Should().HaveCount(LoggingCallCount);
        templates.Should().OnlyContain(template => template == "Performance message {Index}");
    }
}
