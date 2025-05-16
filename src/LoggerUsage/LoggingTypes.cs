using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace LoggerUsage
{
    internal class LoggingTypes
    {
        public LoggingTypes(Compilation compilation, INamedTypeSymbol loggerInterface)
        {
            ILogger = loggerInterface;
            LoggerMessageAttribute = compilation.GetTypeByMetadataName(typeof(LoggerMessageAttribute).FullName!)!;
            EventId = compilation.GetTypeByMetadataName(typeof(EventId).FullName!)!;
            LogLevel = compilation.GetTypeByMetadataName(typeof(LogLevel).FullName!)!;
            LoggerExtensions = compilation.GetTypeByMetadataName(typeof(LoggerExtensions).FullName!)!;
            Exception = compilation.GetTypeByMetadataName(typeof(System.Exception).FullName!)!;
            LoggerExtensionModeler = new(this);
            ObjectNullableArray = compilation.CreateArrayTypeSymbol(compilation.GetSpecialType(SpecialType.System_Object), 
                elementNullableAnnotation: NullableAnnotation.Annotated);
        }

        public INamedTypeSymbol ILogger { get; }
        public INamedTypeSymbol LoggerMessageAttribute { get; }
        public INamedTypeSymbol EventId { get; }
        public INamedTypeSymbol LogLevel { get; }
        public INamedTypeSymbol LoggerExtensions { get; }
        public INamedTypeSymbol Exception { get; }
        public IArrayTypeSymbol ObjectNullableArray { get; }

        public LoggerExtensionModeler LoggerExtensionModeler { get; }
    }
}
