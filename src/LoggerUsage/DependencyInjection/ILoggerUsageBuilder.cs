

using LoggerUsage;
using LoggerUsage.Services;
using LoggerUsage.ReportGenerator;
using LoggerUsage.MessageTemplate;
using LoggerUsage.ParameterExtraction;
using LoggerUsage.Configuration;
using LoggerUsage.Factories;
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
        
        // Message template and parameter factories
        services.AddSingleton<IMessageTemplateExtractor, MessageTemplateExtractor>();
        services.AddSingleton<IMessageParameterFactory, MessageParameterFactory>();
        
        // Analyzers
        services.AddSingleton<ILoggerUsageAnalyzer, LogMethodAnalyzer>();
        services.AddSingleton<ILoggerUsageAnalyzer, LoggerMessageAttributeAnalyzer>();
        services.AddSingleton<ILoggerUsageAnalyzer, LoggerMessageDefineAnalyzer>();
        services.AddSingleton<ILoggerUsageAnalyzer, BeginScopeAnalyzer>();
        
        // Main extractor and report generator
        services.AddSingleton<LoggerUsageExtractor>();
        services.AddSingleton<ILoggerReportGeneratorFactory, LoggerReportGeneratorFactory>();

        return new LoggerUsageBuilder(services);
    }

    /// <summary>
    /// Adds enhanced error handling services with detailed diagnostics and error reporting.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional configuration for error handling options</param>
    /// <returns>The logger usage builder</returns>
    public static ILoggerUsageBuilder AddEnhancedErrorHandling(this IServiceCollection services, Action<ErrorHandlingOptions>? configureOptions = null)
    {
        // Configure error handling options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<ErrorHandlingOptions>(options =>
            {
                options.UseEnhancedErrorHandling = true;
                options.LogExtractionFailures = true;
                options.ContinueOnExtractionFailure = true;
            });
        }

        // Enhanced services with detailed error reporting
        services.AddSingleton<IEnhancedMessageTemplateExtractor, EnhancedMessageTemplateExtractor>();
        services.AddSingleton<IEnhancedParameterExtractor, EnhancedKeyValuePairParameterExtractor>();
        
        // Statistics collection
        services.AddSingleton<ExtractionStatistics>();

        // Core services
        services.AddSingleton<IScopeAnalysisService, ScopeAnalysisService>();
        services.AddSingleton<IParameterExtractionService, ParameterExtractionService>();
        services.AddSingleton<IKeyValuePairExtractionService, KeyValuePairExtractionService>();
        
        // Message template and parameter factories
        services.AddSingleton<IMessageTemplateExtractor, MessageTemplateExtractor>();
        services.AddSingleton<IMessageParameterFactory, MessageParameterFactory>();
        
        // Analyzers
        services.AddSingleton<ILoggerUsageAnalyzer, LogMethodAnalyzer>();
        services.AddSingleton<ILoggerUsageAnalyzer, LoggerMessageAttributeAnalyzer>();
        services.AddSingleton<ILoggerUsageAnalyzer, LoggerMessageDefineAnalyzer>();
        services.AddSingleton<ILoggerUsageAnalyzer, BeginScopeAnalyzer>();
        
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