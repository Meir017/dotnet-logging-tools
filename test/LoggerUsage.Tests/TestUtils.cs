using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Tests;

internal static class TestUtils
{
    public static async Task<Compilation> CreateCompilationAsync(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var references = await ReferenceAssemblies.Net.Net90.ResolveAsync(LanguageNames.CSharp, default);
        references = references.Add(MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location));
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
                }
            )
        );
        Assert.Empty(compilation.GetDiagnostics());
        return compilation;
    }
}
