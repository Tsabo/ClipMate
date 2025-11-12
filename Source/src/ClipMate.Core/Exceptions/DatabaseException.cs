namespace ClipMate.Core.Exceptions;

/// <summary>
/// Exception thrown when database operations fail.
/// </summary>
public class DatabaseException : AppException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseException"/> class.
    /// </summary>
    public DatabaseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DatabaseException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DatabaseException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets or sets the database operation that failed.
    /// </summary>
    public string? Operation { get; set; }

    /// <summary>
    /// Gets or sets the database file path.
    /// </summary>
    public string? DatabasePath { get; set; }
}
