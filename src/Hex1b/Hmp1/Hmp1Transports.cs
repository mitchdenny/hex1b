using System.Runtime.CompilerServices;

namespace Hex1b;

/// <summary>
/// Built-in transport helpers for creating stream sources used by
/// <see cref="Hmp1BuilderExtensions"/>.
/// </summary>
public static class Hmp1Transports
{
    /// <summary>
    /// Listens on a Unix domain socket and yields a stream for each
    /// connecting client.
    /// </summary>
    public static async IAsyncEnumerable<Stream> ListenUnixSocket(
        string path, [EnumeratorCancellation] CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Best-effort cleanup of a stale socket file from a prior run.
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch { /* best-effort */ }

        var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint(path);
        var listener = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.Unix,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Unspecified);

        listener.Bind(endpoint);
        listener.Listen(backlog: 16);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                System.Net.Sockets.Socket client;
                try
                {
                    client = await listener.AcceptAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    yield break;
                }

                yield return new System.Net.Sockets.NetworkStream(client, ownsSocket: true);
            }
        }
        finally
        {
            try { listener.Dispose(); } catch { }
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    /// <summary>
    /// Connects to a Unix domain socket and returns a bidirectional stream.
    /// </summary>
    public static async Task<Stream> ConnectUnixSocket(string path, CancellationToken ct)
    {
        var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint(path);
        var socket = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.Unix,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Unspecified);
        await socket.ConnectAsync(endpoint, ct).ConfigureAwait(false);
        return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
    }

    /// <summary>
    /// Returns a stream factory that connects to a Unix domain socket, retrying
    /// with bounded exponential backoff until the socket file appears and
    /// <see cref="System.Net.Sockets.Socket.ConnectAsync(System.Net.EndPoint,CancellationToken)"/>
    /// succeeds, or until the supplied <see cref="CancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="path">UDS path to connect to.</param>
    /// <param name="policy">Optional retry policy. Defaults to
    /// <see cref="RetryPolicy.DefaultUnixSocket"/>.</param>
    /// <remarks>
    /// <para>
    /// Designed for use with <see cref="Hmp1BuilderExtensions.WithHmp1Client(Hex1bTerminalBuilder, Func{CancellationToken, Task{Stream}})"/>
    /// when the producer might not yet be listening at terminal startup time
    /// (see also <see cref="PlaceholderWorkloadAdapter"/>).
    /// </para>
    /// <para>
    /// The retry policy's <see cref="RetryPolicy.OnAttemptFailed"/> hook is invoked
    /// per failed attempt with the attempt index, the next delay, and the
    /// underlying exception, so a placeholder UI can surface "retrying in N s"
    /// messaging.
    /// </para>
    /// </remarks>
    public static Func<CancellationToken, Task<Stream>> RetryingUnixSocket(
        string path,
        RetryPolicy? policy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var pol = policy ?? RetryPolicy.DefaultUnixSocket;

        return async ct =>
        {
            var attempt = 0;
            var delay = pol.InitialDelay;

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                attempt++;

                Exception? error = null;
                try
                {
                    if (File.Exists(path))
                    {
                        return await ConnectUnixSocket(path, ct).ConfigureAwait(false);
                    }
                    error = new FileNotFoundException($"UDS file '{path}' does not exist yet.", path);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                if (pol.MaxAttempts > 0 && attempt >= pol.MaxAttempts)
                {
                    throw new IOException(
                        $"RetryingUnixSocket gave up after {attempt} attempts connecting to '{path}'.",
                        error);
                }

                pol.OnAttemptFailed?.Invoke(new RetryAttemptFailedEventArgs(attempt, delay, error!));

                try
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }

                var nextMs = (long)(delay.TotalMilliseconds * pol.Multiplier);
                if (nextMs > pol.MaxDelay.TotalMilliseconds)
                {
                    nextMs = (long)pol.MaxDelay.TotalMilliseconds;
                }
                delay = TimeSpan.FromMilliseconds(Math.Max(1, nextMs));
            }
        };
    }
}
