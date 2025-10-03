using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace LoggerUsage
{
    /// <summary>
    /// Provides access to common logging-related types from the Microsoft.Extensions.Logging namespace
    /// and Microsoft.CodeAnalysis symbols for code analysis scenarios.
    /// </summary>
    public class LoggingTypes
    {
        internal LoggingTypes(Compilation compilation, INamedTypeSymbol loggerInterface)
        {
            ILogger = loggerInterface;
            LoggerMessageAttribute = compilation.GetTypeByMetadataName(typeof(LoggerMessageAttribute).FullName!)!;
            LogPropertiesAttribute = compilation.GetTypeByMetadataName(typeof(LogPropertiesAttribute).FullName!)!;
            TagNameAttribute = compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.TagNameAttribute");
            TagProviderAttribute = compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.TagProviderAttribute");
            ITagCollector = compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.ITagCollector");
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

            // Well-known simple types for LogProperties analysis
            DateTime = compilation.GetTypeByMetadataName(typeof(DateTime).FullName!)!;
            DateTimeOffset = compilation.GetTypeByMetadataName(typeof(DateTimeOffset).FullName!)!;
            TimeSpan = compilation.GetTypeByMetadataName(typeof(TimeSpan).FullName!)!;
            Guid = compilation.GetTypeByMetadataName(typeof(Guid).FullName!)!;
            Uri = compilation.GetTypeByMetadataName(typeof(Uri).FullName!)!;

            // Generic collection interface for LogProperties analysis
            IEnumerableGeneric = compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T);
        }

        /// <summary>
        /// Gets the ILogger interface type symbol from Microsoft.Extensions.Logging.
        /// </summary>
        public INamedTypeSymbol ILogger { get; }
        
        /// <summary>
        /// Gets the LoggerMessageAttribute type symbol from Microsoft.Extensions.Logging.
        /// </summary>
        public INamedTypeSymbol LoggerMessageAttribute { get; }
        
        /// <summary>
        /// Gets the EventId type symbol from Microsoft.Extensions.Logging.
        /// </summary>
        public INamedTypeSymbol EventId { get; }
        
        /// <summary>
        /// Gets the LogLevel type symbol from Microsoft.Extensions.Logging.
        /// </summary>
        public INamedTypeSymbol LogLevel { get; }
        
        /// <summary>
        /// Gets the LoggerExtensions type symbol from Microsoft.Extensions.Logging.
        /// </summary>
        public INamedTypeSymbol LoggerExtensions { get; }
        
        /// <summary>
        /// Gets the LoggerMessage type symbol from Microsoft.Extensions.Logging.
        /// </summary>
        public INamedTypeSymbol LoggerMessage { get; }
        
        /// <summary>
        /// Gets the Exception type symbol from System.
        /// </summary>
        public INamedTypeSymbol Exception { get; }
        
        /// <summary>
        /// Gets an array type symbol representing nullable object arrays (object?[]).
        /// </summary>
        public IArrayTypeSymbol ObjectNullableArray { get; }
        
        /// <summary>
        /// Gets the LogPropertiesAttribute type symbol from Microsoft.Extensions.Logging.
        /// </summary>
        public INamedTypeSymbol LogPropertiesAttribute { get; }

        /// <summary>
        /// Gets the TagNameAttribute type symbol from Microsoft.Extensions.Logging.
        /// Returns null if the attribute is not available in the compilation.
        /// </summary>
        public INamedTypeSymbol? TagNameAttribute { get; }

        /// <summary>
        /// Gets the TagProviderAttribute type symbol from Microsoft.Extensions.Logging.
        /// Returns null if the attribute is not available in the compilation.
        /// </summary>
        public INamedTypeSymbol? TagProviderAttribute { get; }

        /// <summary>
        /// Gets the ITagCollector interface type symbol from Microsoft.Extensions.Logging.
        /// Returns null if the interface is not available in the compilation.
        /// </summary>
        public INamedTypeSymbol? ITagCollector { get; }

        /// <summary>
        /// Gets a KeyValuePair&lt;string, object?&gt; type symbol used for structured logging parameters.
        /// </summary>
        public INamedTypeSymbol KeyValuePairOfStringNullableObject { get; }
        
        /// <summary>
        /// Gets the generic KeyValuePair&lt;,&gt; type symbol from System.Collections.Generic.
        /// </summary>
        public INamedTypeSymbol KeyValuePairGeneric { get; }
        
        /// <summary>
        /// Gets an IEnumerable&lt;KeyValuePair&lt;string, object?&gt;&gt; type symbol used for structured logging state.
        /// </summary>
        public INamedTypeSymbol IEnumerableOfKeyValuePair { get; }

        /// <summary>
        /// Gets the logger extension modeler used for analyzing logger extension methods.
        /// </summary>
        public LoggerExtensionModeler LoggerExtensionModeler { get; }

        /// <summary>
        /// Gets the DateTime type symbol from System.
        /// </summary>
        public INamedTypeSymbol DateTime { get; }

        /// <summary>
        /// Gets the DateTimeOffset type symbol from System.
        /// </summary>
        public INamedTypeSymbol DateTimeOffset { get; }

        /// <summary>
        /// Gets the TimeSpan type symbol from System.
        /// </summary>
        public INamedTypeSymbol TimeSpan { get; }

        /// <summary>
        /// Gets the Guid type symbol from System.
        /// </summary>
        public INamedTypeSymbol Guid { get; }

        /// <summary>
        /// Gets the Uri type symbol from System.
        /// </summary>
        public INamedTypeSymbol Uri { get; }

        /// <summary>
        /// Gets the generic IEnumerable&lt;T&gt; type symbol from System.Collections.Generic.
        /// </summary>
        public INamedTypeSymbol IEnumerableGeneric { get; }
    }
}
