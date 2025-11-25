namespace ClipMate.Core.Exceptions;

/// <summary>
/// Base exception class for all ClipMate application exceptions.
/// </summary>
public class AppException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppException"/> class.
    /// </summary>
    public AppException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public AppException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public AppException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
