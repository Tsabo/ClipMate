namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Thrown when a Database cannot be read right now: a local Database's file is missing, or a Remote Database's Peer
/// cannot be reached or no longer shares it.
/// </summary>
public sealed class DatabaseUnavailableException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    public DatabaseUnavailableException()
    {
    }

    /// <summary>
    /// Creates the exception with a message.
    /// </summary>
    public DatabaseUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with a message and the underlying failure.
    /// </summary>
    public DatabaseUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
