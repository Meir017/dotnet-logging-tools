

using LoggerUsage;
using LoggerUsage.Services;
using LoggerUsage.ReportGenerator;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public interface ILoggerUsageBuilder
{
    IServiceCollection Services { get; }
}

public static class LoggerUsageBuilderExtensions
{
    public static ILoggerUsageBuilder AddLoggerUsageExtractor(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IScopeAnalysisService, ScopeAnalysisService>();
        services.AddSingleton<IParameterExtractionService, ParameterExtractionService>();
        services.AddSingleton<IKeyValuePairExtractionService, KeyValuePairExtractionService>();
        
        // Main extractor and report generator
        services.AddSingleton<LoggerUsageExtractor>();
        services.AddSingleton<ILoggerReportGeneratorFactory, LoggerReportGeneratorFactory>();

        return new LoggerUsageBuilder(services);
    }

    private class LoggerUsageBuilder(IServiceCollection services) : ILoggerUsageBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}