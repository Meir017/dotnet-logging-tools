using LoggerUsage.Models;

namespace LoggerUsage.Models
{
    /// <summary>
    /// Represents the result of analyzing a BeginScope operation.
    /// </summary>
    internal class ScopeAnalysisResult
    {
        /// <summary>
        /// The extracted message template from the scope state, if any.
        /// </summary>
        public string? MessageTemplate { get; init; }

        /// <summary>
        /// The parameters extracted from the scope state.
        /// </summary>
        public List<MessageParameter> Parameters { get; init; } = [];

        /// <summary>
        /// Indicates whether the analyzed method is an extension method.
        /// </summary>
        public bool IsExtensionMethod { get; init; }

        /// <summary>
        /// Indicates whether the analysis was successful.
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// Any error message if the analysis failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Creates a successful analysis result.
        /// </summary>
        public static ScopeAnalysisResult Success(string? messageTemplate, List<MessageParameter> parameters, bool isExtensionMethod)
        {
            return new ScopeAnalysisResult
            {
                MessageTemplate = messageTemplate,
                Parameters = parameters,
                IsExtensionMethod = isExtensionMethod,
                IsSuccess = true
            };
        }

        /// <summary>
        /// Creates a failed analysis result.
        /// </summary>
        public static ScopeAnalysisResult Failure(string errorMessage)
        {
            return new ScopeAnalysisResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
