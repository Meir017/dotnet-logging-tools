using System.Text.Json;
using System.Text.Json.Serialization;
using LoggerUsage.Models;

namespace LoggerUsage.ReportGenerator;

internal class JsonLoggerReportGenerator : ILoggerReportGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string GenerateReport(LoggerUsageExtractionResult loggerUsage)
    {
        var reportWithMetadata = new
        {
            SchemaVersion = "2.0",
            GeneratedAt = DateTime.UtcNow,
            Features = new
            {
                TagNameSupport = true,
                TagProviderSupport = true,
                DataClassificationSupport = true,
                TransitivePropertiesSupport = true
            },
            Summary = new
            {
                TotalLoggerUsages = loggerUsage.Results.Count,
                UniqueParameterNames = loggerUsage.Summary.UniqueParameterNameCount,
                TotalParameterUsages = loggerUsage.Summary.TotalParameterUsageCount,
                ParameterInconsistencies = loggerUsage.Summary.InconsistentParameterNames.Count,
                Classification = loggerUsage.Summary.ClassificationStats.HasClassifications ? new
                {
                    ClassifiedParameters = loggerUsage.Summary.ClassificationStats.TotalClassifiedParameters,
                    ClassifiedProperties = loggerUsage.Summary.ClassificationStats.TotalClassifiedProperties,
                    SensitivePercentage = loggerUsage.Summary.ClassificationStats.SensitiveParameterPercentage,
                    loggerUsage.Summary.ClassificationStats.ByLevel
                } : null,
                Telemetry = loggerUsage.Summary.TelemetryStats.HasTelemetryFeatures ? new
                {
                    loggerUsage.Summary.TelemetryStats.ParametersWithCustomTagNames,
                    loggerUsage.Summary.TelemetryStats.PropertiesWithCustomTagNames,
                    loggerUsage.Summary.TelemetryStats.ParametersWithTagProviders,
                    TransitiveProperties = loggerUsage.Summary.TelemetryStats.TotalTransitiveProperties,
                    loggerUsage.Summary.TelemetryStats.CustomTagNameMappings,
                    loggerUsage.Summary.TelemetryStats.TagProviders
                } : null
            },
            LoggerUsages = loggerUsage.Results
        };

        return JsonSerializer.Serialize(reportWithMetadata, JsonOptions);
    }
}
