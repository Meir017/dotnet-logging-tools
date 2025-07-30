using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis;
using LoggerUsage.Models;
using Microsoft.Extensions.Logging;

namespace LoggerUsage.Analyzers
{
    internal static class EventIdExtractor
    {
        public static bool TryExtractFromInvocation(IInvocationOperation operation, LoggingTypes loggingTypes, out EventIdBase eventId)
        {
            int parameterStartIndex = operation.TargetMethod.IsExtensionMethod ? 1 : 0;
            for (var i = parameterStartIndex; i < operation.TargetMethod.Parameters.Length; i++)
            {
                if (!loggingTypes.EventId.Equals(operation.Arguments[i].Value.Type, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                var argumentOperation = operation.Arguments[i].Value.UnwrapConversion();

                // Handle default value - continue to next parameter instead of failing
                if (argumentOperation.Kind is OperationKind.DefaultValue)
                {
                    continue;
                }

                return TryExtractFromOperation(argumentOperation, out eventId);
            }

            eventId = default!;
            return false;
        }

        public static bool TryExtractFromArgument(IOperation argumentOperation, out EventIdBase eventId)
        {
            var unwrapped = argumentOperation.UnwrapConversion();

            // Handle default value - return false instead of continuing
            if (unwrapped.Kind is OperationKind.DefaultValue)
            {
                eventId = default!;
                return false;
            }

            return TryExtractFromOperation(unwrapped, out eventId);
        }

        private static bool TryExtractFromOperation(IOperation operation, out EventIdBase eventId)
        {
            // Handle EventId constructor
            if (operation is IObjectCreationOperation objectCreation &&
                objectCreation.Type?.Name == nameof(EventId))
            {
                if (objectCreation.Arguments.Length == 0)
                {
                    eventId = default!;
                    return false;
                }

                var (id, name) = ExtractIdAndNameFromConstructor(objectCreation);
                eventId = new EventIdDetails(id, name);
                return true;
            }

            // Handle literal operation
            if (operation is ILiteralOperation literalOperation)
            {
                if (literalOperation.ConstantValue.HasValue)
                {
                    eventId = new EventIdDetails(
                        ConstantOrReference.Constant(literalOperation.ConstantValue.Value!),
                        ConstantOrReference.Missing);
                    return true;
                }
                else
                {
                    eventId = CreateEventIdRef(literalOperation);
                    return true;
                }
            }

            // Handle constant value (for direct EventId values)
            if (operation.ConstantValue.HasValue)
            {
                eventId = new EventIdDetails(
                    ConstantOrReference.Constant(operation.ConstantValue.Value!),
                    ConstantOrReference.Missing);
                return true;
            }

            // Handle any other reference (field reference, variable reference, etc.)
            eventId = CreateEventIdRef(operation);
            return true;
        }

        private static (ConstantOrReference id, ConstantOrReference name) ExtractIdAndNameFromConstructor(IObjectCreationOperation objectCreation)
        {
            ConstantOrReference id = ConstantOrReference.Missing;
            ConstantOrReference name = ConstantOrReference.Missing;

            if (objectCreation.Arguments.Length > 0)
            {
                var idArg = objectCreation.Arguments[0].Value;
                // Check if the argument has a constant value (literal)
                if (idArg.ConstantValue.HasValue && idArg.ConstantValue.Value is int idValue)
                {
                    id = ConstantOrReference.Constant(idValue);
                }
                else
                {
                    id = new ConstantOrReference(idArg.Kind.ToString(), idArg.Syntax.ToString());
                }

                // If there's only one argument, leave name as Missing
                if (objectCreation.Arguments.Length == 1)
                {
                    name = ConstantOrReference.Missing;
                }
            }

            if (objectCreation.Arguments.Length > 1
                && !objectCreation.Arguments[1].Value.IsImplicit)
            {
                var nameArg = objectCreation.Arguments[1].Value;
                if (nameArg.ConstantValue.HasValue)
                {
                    var nameValue = nameArg.ConstantValue.Value;
                    if (nameValue != null)
                    {
                        name = ConstantOrReference.Constant(nameValue);
                    }
                    else
                    {
                        name = ConstantOrReference.Constant(null!);
                    }
                }
                else
                {
                    name = new ConstantOrReference(nameArg.Kind.ToString(), nameArg.Syntax.ToString());
                }
            }

            return (id, name);
        }

        private static EventIdRef CreateEventIdRef(IOperation operation)
        {
            return new EventIdRef(operation.Kind.ToString(), operation.Syntax.ToString());
        }
    }
}
