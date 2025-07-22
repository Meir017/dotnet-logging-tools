

using Microsoft.Extensions.DependencyInjection;

namespace LoggerUsage.Analyzers;

public class LoggerUsageExtractorTester
{
    public static LoggerUsageExtractor CreateExtractor()
    {
        var services = new ServiceCollection();
        services.AddLoggerUsageExtractor();

        return services.BuildServiceProvider()
            .GetRequiredService<LoggerUsageExtractor>();
    }
}