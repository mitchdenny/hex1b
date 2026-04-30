using System.IO.Pipes;
using System.Text;

namespace Hex1b.Kgp;

/// <summary>
/// Client-side of the KGP side-channel pipe. Used by child processes
/// to send KGP APC sequences to the parent terminal when ConPTY
/// would strip them from stdout.
/// </summary>
/// <remarks>
/// Detected automatically via the HEX1B_KGP_PIPE environment variable.
/// </remarks>
public sealed class KgpPipeClient : IDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private bool _connected;
    private bool _disposed;

    /// <summary>
    /// Creates a client for the KGP side-channel pipe.
    /// </summary>
    /// <param name="pipeName">The pipe name from HEX1B_KGP_PIPE env var.</param>
    public KgpPipeClient(string pipeName)
    {
        _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
    }

    /// <summary>
    /// Attempts to connect to the parent's KGP pipe server.
    /// </summary>
    /// <param name="timeoutMs">Connection timeout in milliseconds.</param>
    /// <returns>True if connected successfully.</returns>
    public bool TryConnect(int timeoutMs = 3000)
    {
        if (_disposed) return false;
        try
        {
            _pipe.Connect(timeoutMs);
            _connected = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Writes raw APC bytes (KGP sequences) to the pipe.
    /// </summary>
    public void Write(ReadOnlySpan<byte> data)
    {
        if (!_connected || _disposed) return;
        try
        {
            _pipe.Write(data);
            _pipe.Flush();
        }
        catch
        {
            _connected = false;
        }
    }

    /// <summary>
    /// Writes a string (KGP APC sequence) to the pipe.
    /// </summary>
    public void Write(string apcSequence)
    {
        Write(Encoding.UTF8.GetBytes(apcSequence));
    }

    /// <summary>
    /// Whether the client is connected to the parent pipe.
    /// </summary>
    public bool IsConnected => _connected && !_disposed;

    /// <summary>
    /// Tries to create a KGP pipe client from the environment variable.
    /// Returns null if the env var is not set or connection fails.
    /// </summary>
    public static KgpPipeClient? TryCreateFromEnvironment()
    {
        var pipeName = Environment.GetEnvironmentVariable("HEX1B_KGP_PIPE");
        if (string.IsNullOrEmpty(pipeName))
            return null;

        var client = new KgpPipeClient(pipeName);
        if (client.TryConnect())
            return client;

        client.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipe.Dispose();
    }
}
