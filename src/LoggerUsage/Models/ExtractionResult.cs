using System;

namespace LoggerUsage.Models;

/// <summary>
/// Represents the result of an extraction operation with success/failure information and error details.
/// </summary>
/// <typeparam name="T">The type of the extracted value</typeparam>
public class ExtractionResult<T>
{
    /// <summary>
    /// Gets a value indicating whether the extraction was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the extracted value if the operation was successful.
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Creates a successful extraction result with the specified value.
    /// </summary>
    /// <param name="value">The extracted value</param>
    /// <returns>A successful extraction result</returns>
    public static ExtractionResult<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value
    };

    /// <summary>
    /// Creates a failed extraction result with the specified error message.
    /// </summary>
    /// <param name="error">The error message</param>
    /// <returns>A failed extraction result</returns>
    public static ExtractionResult<T> Failure(string error) => new()
    {
        IsSuccess = false,
        ErrorMessage = error
    };

    /// <summary>
    /// Creates a failed extraction result with the specified exception.
    /// </summary>
    /// <param name="ex">The exception that caused the failure</param>
    /// <returns>A failed extraction result</returns>
    public static ExtractionResult<T> Failure(Exception ex) => new()
    {
        IsSuccess = false,
        Exception = ex,
        ErrorMessage = ex.Message
    };

    /// <summary>
    /// Creates a failed extraction result with both an error message and exception.
    /// </summary>
    /// <param name="error">The error message</param>
    /// <param name="ex">The exception that caused the failure</param>
    /// <returns>A failed extraction result</returns>
    public static ExtractionResult<T> Failure(string error, Exception ex) => new()
    {
        IsSuccess = false,
        ErrorMessage = error,
        Exception = ex
    };

    /// <summary>
    /// Implicitly converts a value to a successful extraction result.
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <returns>A successful extraction result containing the value</returns>
    public static implicit operator ExtractionResult<T>(T value) => Success(value);

    /// <summary>
    /// Implicitly converts an exception to a failed extraction result.
    /// </summary>
    /// <param name="exception">The exception to convert</param>
    /// <returns>A failed extraction result containing the exception</returns>
    public static implicit operator ExtractionResult<T>(Exception exception) => Failure(exception);
}

/// <summary>
/// Non-generic version of ExtractionResult for operations that don't return a value.
/// </summary>
public class ExtractionResult : ExtractionResult<object?>
{
    /// <summary>
    /// Creates a successful extraction result with no value.
    /// </summary>
    /// <returns>A successful extraction result</returns>
    public static ExtractionResult Success() => new()
    {
        IsSuccess = true,
        Value = null
    };

    /// <summary>
    /// Creates a failed extraction result with the specified error message.
    /// </summary>
    /// <param name="error">The error message</param>
    /// <returns>A failed extraction result</returns>
    public static new ExtractionResult Failure(string error) => new()
    {
        IsSuccess = false,
        ErrorMessage = error
    };

    /// <summary>
    /// Creates a failed extraction result with the specified exception.
    /// </summary>
    /// <param name="ex">The exception that caused the failure</param>
    /// <returns>A failed extraction result</returns>
    public static new ExtractionResult Failure(Exception ex) => new()
    {
        IsSuccess = false,
        Exception = ex,
        ErrorMessage = ex.Message
    };

    /// <summary>
    /// Creates a failed extraction result with both an error message and exception.
    /// </summary>
    /// <param name="error">The error message</param>
    /// <param name="ex">The exception that caused the failure</param>
    /// <returns>A failed extraction result</returns>
    public static new ExtractionResult Failure(string error, Exception ex) => new()
    {
        IsSuccess = false,
        ErrorMessage = error,
        Exception = ex
    };
}
