namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Peer networking settings. The Device identity (key and certificate) is never stored here.
/// </summary>
public class NetworkingConfiguration
{
    /// <summary>
    /// Default TCP port of the Peer listener.
    /// </summary>
    public const int DefaultPort = 47823;

    /// <summary>
    /// Gets or sets whether networking is enabled. Off by default: with networking off ClipMate opens no listening port.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the TCP port the Peer listener uses.
    /// </summary>
    public int Port { get; set; } = DefaultPort;

    /// <summary>
    /// Gets or sets this Device's Device Name, shown to other Devices. Defaults to the computer name.
    /// </summary>
    public string DeviceName { get; set; } = Environment.MachineName;
}
