using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using LoggerUsage.Models;

namespace LoggerUsage
{
    public class LoggerUsageExtractor
    {
        public List<LoggerUsageInfo> ExtractLoggerUsages(CSharpCompilation compilation)
        {
            var loggerInterface = compilation.GetTypeByMetadataName(typeof(ILogger).FullName!)!;
            if (loggerInterface == null) return [];

            var loggingTypes = new LoggingTypes(compilation, loggerInterface);

            var results = new List<LoggerUsageInfo>();
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var root = syntaxTree.GetRoot();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                if (root == null || semanticModel == null) continue;

                var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    if (semanticModel.GetOperation(invocation) is not IInvocationOperation operation)
                    {
                        continue;
                    }
                    
                    if (!loggingTypes.LoggerExtensionModeler.IsLoggerMethod(operation.TargetMethod)) continue;

                    var usage = new LoggerUsageInfo
                    {
                        MethodName = operation.TargetMethod.Name,
                        Location = new MethodCallLocation
                        {
                            LineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line,
                            ColumnNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Character,
                        },
                    };

                    if (TryExtractEventId(operation, loggingTypes, out var eventId))
                    {
                        usage.EventId = eventId;
                    }
                    if (TryExtractLogLevel(operation, loggingTypes, out var logLevel))
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

        private static bool TryExtractEventId(IInvocationOperation operation, LoggingTypes loggingTypes, out EventIdBase eventId)
        {
            int parameterStartIndex = operation.TargetMethod.IsExtensionMethod ? 1 : 0;
            for (var i = parameterStartIndex; i < operation.TargetMethod.Parameters.Length; i++)
            {
                if (!loggingTypes.EventId.Equals(operation.Arguments[i].Value.Type, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                var argumentOperation = operation.Arguments[i].Value.UnwrapConversion();

                // Attempt to create EventId from constructor arguments
                if (argumentOperation is IObjectCreationOperation objectCreationOperation &&
                    objectCreationOperation.Type?.Name == nameof(EventId))
                {
                    if (objectCreationOperation.Arguments.Length is 0) continue;

                    ConstantOrReference id = ConstantOrReference.Missing;
                    ConstantOrReference name = ConstantOrReference.Missing;

                    if (objectCreationOperation.Arguments.Length > 0)
                    {
                        if (objectCreationOperation.Arguments[0].Value.ConstantValue.Value is int idValue)
                        {
                            id = ConstantOrReference.Constant(idValue);
                        }
                        else
                        {
                            id = new ConstantOrReference(
                                objectCreationOperation.Arguments[0].Value.Kind.ToString(),
                                objectCreationOperation.Arguments[0].Value.Syntax.ToString()
                            );
                        }
                    }

                    if (objectCreationOperation.Arguments.Length > 1)
                    {
                        if (objectCreationOperation.Arguments[1].Value.ConstantValue.HasValue)
                        {
                            name = ConstantOrReference.Constant(objectCreationOperation.Arguments[1].Value.ConstantValue.Value!);
                        }
                        else
                        {
                            name = new ConstantOrReference(
                                objectCreationOperation.Arguments[1].Value.Kind.ToString(),
                                objectCreationOperation.Arguments[1].Value.Syntax.ToString()
                            );
                        }
                    }

                    eventId = new EventIdDetails(id, name);
                    return true;
                }
                else if (argumentOperation.Kind is OperationKind.DefaultValue)
                {
                    continue;
                }
                else
                {
                    eventId = new EventIdRef(
                        argumentOperation.Kind.ToString(),
                        argumentOperation.Syntax.ToString()
                    );
                    return true;
                }
            }

            eventId = default!;
            return false;
        }

        private static bool TryExtractLogLevel(IInvocationOperation operation, LoggingTypes loggingTypes, out LogLevel logLevel)
        {
            return operation.TargetMethod.Name switch
            {
                nameof(ILogger.Log) => TryGetLogLevelFromArguments(operation, loggingTypes, out logLevel),
                nameof(LoggerExtensions.LogTrace) => ReturnLogLevel(LogLevel.Trace, out logLevel),
                nameof(LoggerExtensions.LogDebug) => ReturnLogLevel(LogLevel.Debug, out logLevel),
                nameof(LoggerExtensions.LogInformation) => ReturnLogLevel(LogLevel.Information, out logLevel),
                nameof(LoggerExtensions.LogWarning) => ReturnLogLevel(LogLevel.Warning, out logLevel),
                nameof(LoggerExtensions.LogError) => ReturnLogLevel(LogLevel.Error, out logLevel),
                nameof(LoggerExtensions.LogCritical) => ReturnLogLevel(LogLevel.Critical, out logLevel),
                _ => NotFound(out logLevel)
            };

            static bool TryGetLogLevelFromArguments(IInvocationOperation operation, LoggingTypes loggingTypes, out LogLevel logLevel)
            {
                int parameterStartIndex = operation.TargetMethod.IsExtensionMethod ? 1 : 0;
                for (var i = parameterStartIndex; i < operation.TargetMethod.Parameters.Length; i++)
                {
                    if (loggingTypes.LogLevel.Equals(operation.TargetMethod.Parameters[i].Type, SymbolEqualityComparer.Default))
                    {
                        var argumentOperation = operation.Arguments[i].Value;
                        if (argumentOperation is not IFieldReferenceOperation fieldReferenceOperation) continue;

                        if (fieldReferenceOperation.ConstantValue.HasValue)
                        {
                            logLevel = (LogLevel)fieldReferenceOperation.ConstantValue.Value!;
                            return true;
                        }
                    }
                }

                logLevel = default;
                return false;
            }

            static bool ReturnLogLevel(LogLevel level, out LogLevel logLevel)
            {
                logLevel = level;
                return true;
            }

            static bool NotFound(out LogLevel logLevel)
            {
                logLevel = default;
                return false;
            }
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
