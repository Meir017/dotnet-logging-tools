

using LoggerUsage;
using LoggerUsage.Services;
using LoggerUsage.ReportGenerator;
using LoggerUsage.MessageTemplate;
using LoggerUsage.ParameterExtraction;
using LoggerUsage.Analyzers;

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

        services.AddSingleton<IMessageTemplateExtractor, MessageTemplateExtractor>();
        
        // Analyzers
        services.AddSingleton<ILoggerUsageAnalyzer, LogMethodAnalyzer>();
        services.AddSingleton<ILoggerUsageAnalyzer, LoggerMessageAttributeAnalyzer>();
        services.AddSingleton<ILoggerUsageAnalyzer, LoggerMessageDefineAnalyzer>();
        services.AddSingleton<ILoggerUsageAnalyzer, BeginScopeAnalyzer>();
        
        // Main extractor and report generator
        services.AddSingleton<LoggerUsageExtractor>();
        services.AddSingleton<ILoggerReportGeneratorFactory, LoggerReportGeneratorFactory>();

        // Parameter extraction
        services.AddSingleton<ArrayParameterExtractor>();
        services.AddSingleton<AnonymousObjectParameterExtractor>();

        return new LoggerUsageBuilder(services);
    }

    private class LoggerUsageBuilder(IServiceCollection services) : ILoggerUsageBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}