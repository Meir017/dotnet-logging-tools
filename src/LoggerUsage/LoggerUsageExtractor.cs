using System.Diagnostics.Metrics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;

namespace LoggerUsage
{
    public class LoggerUsageInfo
    {
        public required string MethodName { get; set; }
        public string? MessageTemplate { get; set; }
        public LogLevel LogLevel { get; set; }
        public string? EventId { get; set; }
        public List<MessageParameter> MessageParameters { get; set; } = new();
        public required Location Location { get; set; }
    }

    public class MessageParameter
    {
        public required string Name { get; set; }
        public string? Type { get; set; }
        public string? Value { get; set; }
    }

    public class LoggerUsageExtractor
    {
        public List<LoggerUsageInfo> ExtractLoggerUsages(CSharpCompilation compilation)
        {
            var loggerInterface = compilation.GetTypeByMetadataName(typeof(ILogger).FullName!)!;
            if (loggerInterface == null) return [];

            var loggingTypes = new LoggingTypes(compilation, loggerInterface);
            if (loggingTypes.ILogger == null) return [];

            var results = new List<LoggerUsageInfo>();
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var root = syntaxTree.GetRoot();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                if (root == null || semanticModel == null) continue;

                var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    IMethodSymbol? method;
                    Optional<object?>[] constantValues;
                    if (semanticModel.GetOperation(invocation) is IInvocationOperation operation)
                    {
                        method = operation.TargetMethod;
                        constantValues = [.. operation.Arguments.Select(arg => arg.ConstantValue)];
                    }
                    else
                    {
                        (method, constantValues) = MethodSymbolHelper.GetMethodSymbol(invocation, semanticModel);
                    }

                    if (method == null) continue;

                    if (!loggingTypes.LoggerExtensionModeler.IsLoggerMethod(method)) continue;

                    var usage = new LoggerUsageInfo
                    {
                        MethodName = method.Name,
                        Location = invocation.GetLocation(),
                    };

                    if (TryExtractEventId(invocation, method, out var eventId))
                    {
                        usage.EventId = eventId;
                    }
                    if (TryExtractLogLevel(invocation, method, out var logLevel))
                    {
                        usage.LogLevel = logLevel;
                    }

                    // usage.MessageTemplate = ExtractMessageTemplate(operation);
                    // usage.MessageParameters = ExtractArguments(invocation, method, constantValues);

                    results.Add(usage);
                }
            }

            return results;
        }

        private static string? ExtractMessageTemplate(IInvocationOperation operation)
        {
            foreach (var arg in operation.Arguments)
            {
                if (arg.Parameter?.Name?.Equals("message") == true)
                {
                    return arg.Value.ConstantValue.HasValue ? arg.Value.ConstantValue.Value?.ToString() : null;
                }
            }
            return null;
        }

        private static bool TryExtractEventId(InvocationExpressionSyntax invocation, IMethodSymbol method, out string eventId)
        {
            var i = method.IsExtensionMethod ? 1 : 0;
            for (; i < method.Parameters.Length; i++)
            {
                if (method.Parameters[i].Type?.Name == "EventId")
                {
                    eventId = invocation.ArgumentList.Arguments[i].ToString();
                    return true;
                }
            }
            eventId = null!;
            return false;
        }

        private static bool TryExtractLogLevel(InvocationExpressionSyntax invocation, IMethodSymbol method, out LogLevel logLevel)
        {
            var i = method.IsExtensionMethod ? 1 : 0;
            for (; i < method.Parameters.Length; i++)
            {
                if (method.Parameters[i].Type?.Name == "LogLevel")
                {
                    var logLevelString = invocation.ArgumentList.Arguments[i].ToString();
                    if (Enum.TryParse(logLevelString, out LogLevel parsedLogLevel))
                    {
                        logLevel = parsedLogLevel;
                        return true;
                    }
                }
            }

            logLevel = default;
            return false;
        }

        private static List<MessageParameter> ExtractArguments(InvocationExpressionSyntax invocation, IMethodSymbol method, Optional<object?>[] constantValues)
        {
            var args = new List<MessageParameter>();
            var i = method.IsExtensionMethod ? 1 : 0;
            for (; i < method.Parameters.Length; i++)
            {
                var param = method.Parameters[i];
                if (param.Name != null && !param.Name.Equals("message") && param.Type?.Name != "EventId")
                {
                    args.Add(new MessageParameter
                    {
                        Name = param.Name,
                        Type = param.Type?.Name,
                        Value = constantValues.Length >= i && constantValues[i].HasValue ? constantValues[i].Value?.ToString() : null
                    });
                }
            }
            return args;
        }
    }
}
