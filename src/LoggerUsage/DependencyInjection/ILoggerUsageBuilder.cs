

using LoggerUsage;
using LoggerUsage.Services;
using LoggerUsage.ReportGenerator;
using LoggerUsage.MessageTemplate;
using LoggerUsage.ParameterExtraction;
using LoggerUsage.Analyzers;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines a builder pattern for configuring logger usage analysis services.
/// </summary>
public interface ILoggerUsageBuilder
{
    /// <summary>
    /// Gets the service collection used to register logger usage services.
    /// </summary>
    IServiceCollection Services { get; }
}

/// <summary>
/// Provides extension methods for configuring logger usage analysis services.
/// </summary>
public static class LoggerUsageBuilderExtensions
{
    /// <summary>
    /// Adds logger usage extraction services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>An <see cref="ILoggerUsageBuilder"/> that can be used to further configure the services.</returns>
    public static ILoggerUsageBuilder AddLoggerUsageExtractor(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IScopeAnalysisService, ScopeAnalysisService>();
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
        services.AddSingleton<GenericTypeParameterExtractor>();

        return new LoggerUsageBuilder(services);
    }

    private class LoggerUsageBuilder(IServiceCollection services) : ILoggerUsageBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}