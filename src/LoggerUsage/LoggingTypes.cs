using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace LoggerUsage
{
    public class LoggingTypes
    {
        public LoggingTypes(Compilation compilation, INamedTypeSymbol loggerInterface)
        {
            ILogger = loggerInterface;
            LoggerMessageAttribute = compilation.GetTypeByMetadataName(typeof(LoggerMessageAttribute).FullName!)!;
            LogPropertiesAttribute = compilation.GetTypeByMetadataName(typeof(LogPropertiesAttribute).FullName!)!;
            EventId = compilation.GetTypeByMetadataName(typeof(EventId).FullName!)!;
            LogLevel = compilation.GetTypeByMetadataName(typeof(LogLevel).FullName!)!;
            LoggerExtensions = compilation.GetTypeByMetadataName(typeof(LoggerExtensions).FullName!)!;
            LoggerMessage = compilation.GetTypeByMetadataName(typeof(LoggerMessage).FullName!)!;
            Exception = compilation.GetTypeByMetadataName(typeof(Exception).FullName!)!;
            LoggerExtensionModeler = new(this);

            var nullableObjectType = compilation.GetSpecialType(SpecialType.System_Object).WithNullableAnnotation(NullableAnnotation.Annotated);

            ObjectNullableArray = compilation.CreateArrayTypeSymbol(nullableObjectType);
            KeyValuePairGeneric = compilation.GetTypeByMetadataName(typeof(KeyValuePair<,>).FullName!)!;
            KeyValuePairOfStringNullableObject = KeyValuePairGeneric.Construct(compilation.GetSpecialType(SpecialType.System_String), nullableObjectType);
            IEnumerableOfKeyValuePair = compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T).Construct(KeyValuePairOfStringNullableObject);

        }

        public INamedTypeSymbol ILogger { get; }
        public INamedTypeSymbol LoggerMessageAttribute { get; }
        public INamedTypeSymbol EventId { get; }
        public INamedTypeSymbol LogLevel { get; }
        public INamedTypeSymbol LoggerExtensions { get; }
        public INamedTypeSymbol LoggerMessage { get; }
        public INamedTypeSymbol Exception { get; }
        public IArrayTypeSymbol ObjectNullableArray { get; }
        public INamedTypeSymbol LogPropertiesAttribute { get; }

        public INamedTypeSymbol KeyValuePairOfStringNullableObject { get; }
        public INamedTypeSymbol KeyValuePairGeneric { get; }
        public INamedTypeSymbol IEnumerableOfKeyValuePair { get; }

        public LoggerExtensionModeler LoggerExtensionModeler { get; }
    }
}
