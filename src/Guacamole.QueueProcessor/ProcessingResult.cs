namespace Guacamole.QueueProcessor;

/// <summary>
/// Result of message processing indicating success or failure.
/// </summary>
public sealed class ProcessingResult
{
    /// <summary>
    /// Indicates whether processing was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Whether to retry this message after failure.
    /// If false, message goes to dead-letter immediately.
    /// </summary>
    public bool ShouldRetry { get; init; } = true;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ProcessingResult Successful() => new() { Success = true };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ProcessingResult Failed(string errorMessage, Exception? exception = null, bool shouldRetry = true)
        => new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            ShouldRetry = shouldRetry
        };
}