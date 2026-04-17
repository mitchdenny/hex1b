using System.Net.Sockets;

namespace Hex1b.Muxer;

/// <summary>
/// Configuration options for a muxer server (presentation adapter).
/// </summary>
public sealed class MuxerServerOptions
{
    internal Func<MuxerPresentationAdapter, CancellationToken, Task>? ListenerFactory { get; private set; }
    internal int Width { get; private set; } = 80;
    internal int Height { get; private set; } = 24;

    /// <summary>
    /// Configures the muxer server to listen on a Unix domain socket.
    /// Each incoming connection is added as a client to the presentation adapter.
    /// </summary>
    /// <param name="path">Path to the Unix domain socket file.</param>
    /// <returns>This options instance for chaining.</returns>
    public MuxerServerOptions ListenUnixSocket(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        ListenerFactory = async (adapter, ct) =>
        {
            // Ensure the socket directory exists and clean up stale socket
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch { /* ignore stale socket */ }
            }

            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Bind(new UnixDomainSocketEndPoint(path));
            socket.Listen(5);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var clientSocket = await socket.AcceptAsync(ct).ConfigureAwait(false);
                    var stream = new NetworkStream(clientSocket, ownsSocket: true);
                    // Fire and forget: add client, handle errors in background
                    _ = AddClientSafeAsync(adapter, stream, ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                // Clean up the socket file
                try { File.Delete(path); }
                catch { /* ignore */ }
            }
        };

        return this;
    }

    /// <summary>
    /// Configures the muxer server with a custom stream source that yields client streams.
    /// </summary>
    /// <param name="streamSource">
    /// An async enumerable that produces bidirectional streams for each connecting client.
    /// </param>
    /// <returns>This options instance for chaining.</returns>
    public MuxerServerOptions ListenStreams(Func<CancellationToken, IAsyncEnumerable<Stream>> streamSource)
    {
        ArgumentNullException.ThrowIfNull(streamSource);

        ListenerFactory = async (adapter, ct) =>
        {
            await foreach (var stream in streamSource(ct).WithCancellation(ct).ConfigureAwait(false))
            {
                _ = AddClientSafeAsync(adapter, stream, ct);
            }
        };

        return this;
    }

    /// <summary>
    /// Sets the initial terminal dimensions.
    /// </summary>
    /// <param name="width">Terminal width in columns.</param>
    /// <param name="height">Terminal height in rows.</param>
    /// <returns>This options instance for chaining.</returns>
    public MuxerServerOptions WithDimensions(int width, int height)
    {
        Width = width;
        Height = height;
        return this;
    }

    private static async Task AddClientSafeAsync(
        MuxerPresentationAdapter adapter, Stream stream, CancellationToken ct)
    {
        try
        {
            await adapter.AddClient(stream, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            try { await stream.DisposeAsync().ConfigureAwait(false); }
            catch { /* ignore */ }
        }
    }
}
