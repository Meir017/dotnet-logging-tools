using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoggerUsage.Tests;

internal static class TestUtils
{
    public static async Task<Compilation> CreateCompilationAsync(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, default);
        references = references.Add(MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location));
        references = references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Logging.LogPropertiesAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic>
                {
                    ["CS0169"] = ReportDiagnostic.Suppress, // Suppress unused field warning
                    ["CS0649"] = ReportDiagnostic.Suppress, // Suppress unassigned field warning
                    ["CS0219"] = ReportDiagnostic.Suppress, // Suppress assigned but unused variable warning
                    ["CS0414"] = ReportDiagnostic.Suppress, // Suppress unassigned field warning
                    ["CS8602"] = ReportDiagnostic.Suppress, // Suppress dereference of a possibly null reference warning
                    ["CS8632"] = ReportDiagnostic.Suppress, // Suppress nullable reference types annotation warning
                    ["EXTEXP0003"] = ReportDiagnostic.Suppress, // Suppress experimental API warning
                }
            )
        );
        Assert.Empty(compilation.GetDiagnostics());
        return compilation;
    }

    public static LoggerUsageExtractor CreateLoggerUsageExtractor()
    {
        var services = new ServiceCollection();
        services.AddLoggerUsageExtractor();
        services.AddLogging();

        return services.BuildServiceProvider()
            .GetRequiredService<LoggerUsageExtractor>();
    }
}
