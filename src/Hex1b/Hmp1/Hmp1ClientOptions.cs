using System.Net.Sockets;

namespace Hex1b.Hmp1;

/// <summary>
/// Configuration options for a muxer client (workload adapter).
/// </summary>
public sealed class Hmp1ClientOptions
{
    internal Func<CancellationToken, Task<Stream>>? StreamFactory { get; private set; }

    /// <summary>
    /// Configures the muxer client to connect to a Unix domain socket.
    /// </summary>
    /// <param name="path">Path to the Unix domain socket file.</param>
    /// <returns>This options instance for chaining.</returns>
    public Hmp1ClientOptions ConnectUnixSocket(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        StreamFactory = async ct =>
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), ct).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };

        return this;
    }

    /// <summary>
    /// Configures the muxer client with a custom stream factory.
    /// </summary>
    /// <param name="streamFactory">
    /// Factory that creates a bidirectional stream to the server.
    /// </param>
    /// <returns>This options instance for chaining.</returns>
    public Hmp1ClientOptions ConnectStream(Func<CancellationToken, Task<Stream>> streamFactory)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        StreamFactory = streamFactory;
        return this;
    }
}
