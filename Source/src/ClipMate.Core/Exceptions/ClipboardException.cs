namespace ClipMate.Core.Exceptions;

/// <summary>
/// Exception thrown when clipboard operations fail.
/// </summary>
public class ClipboardException : AppException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardException"/> class.
    /// </summary>
    public ClipboardException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ClipboardException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ClipboardException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets or sets the Win32 error code associated with the clipboard operation.
    /// </summary>
    public int? Win32ErrorCode { get; set; }
}
