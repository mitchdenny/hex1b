using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Hex1b.Hmp1;

/// <summary>
/// Extension methods for <see cref="Hex1bTerminalBuilder"/> to configure muxer adapters.
/// </summary>
public static class Hmp1BuilderExtensions
{
    /// <summary>
    /// Adds an HMP v1 server listener that accepts client connections from the given stream source.
    /// Can be called multiple times to serve over multiple transports simultaneously.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <param name="streamSource">
    /// A factory that produces an async enumerable of bidirectional streams (one per connecting client).
    /// The cancellation token is cancelled when the terminal session ends.
    /// </param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// // Serve over both UDS and a custom transport
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithPtyProcess("bash")
    ///     .WithHmp1Server(ct => Hmp1Transports.ListenUnixSocket("/tmp/my.sock", ct))
    ///     .WithHmp1Server(ct => MyCustomTransport.AcceptConnections(ct))
    ///     .Build();
    ///
    /// await terminal.RunAsync();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithHmp1Server(
        this Hex1bTerminalBuilder builder,
        Func<CancellationToken, IAsyncEnumerable<Stream>> streamSource)
    {
        ArgumentNullException.ThrowIfNull(streamSource);

        // Check if we already have a muxer filter — reuse it for additional listeners
        var filter = Hmp1ListenerStartFilter.GetOrCreate(builder, out var adapter);
        filter.AddStreamSource(streamSource);

        return builder;
    }

    /// <summary>
    /// Adds an HMP v1 server listener on a Unix domain socket.
    /// Can be called multiple times to serve over multiple sockets.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <param name="socketPath">Path to the Unix domain socket file.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithPtyProcess("bash")
    ///     .WithHmp1UdsServer("/tmp/my-terminal.sock")
    ///     .Build();
    ///
    /// await terminal.RunAsync();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithHmp1UdsServer(
        this Hex1bTerminalBuilder builder,
        string socketPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        return builder.WithHmp1Server(ct => Hmp1Transports.ListenUnixSocket(socketPath, ct));
    }

    /// <summary>
    /// Configures the terminal as a muxer client. The terminal connects to a remote
    /// muxer server over the Hex1b Muxer Protocol and displays the remote terminal locally.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <param name="configure">Callback to configure the muxer client options (e.g., connection).</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithHmp1Client(client => client.ConnectUnixSocket("/tmp/my-terminal.sock"))
    ///     .Build();
    ///
    /// await terminal.RunAsync();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithHmp1Client(
        this Hex1bTerminalBuilder builder,
        Action<Hmp1ClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new Hmp1ClientOptions();
        configure(options);

        if (options.StreamFactory is null)
            throw new InvalidOperationException(
                "Muxer client must have a connection configured. " +
                "Call ConnectUnixSocket() or ConnectStream() on the options.");

        var streamFactory = options.StreamFactory;
        var adapter = new Hmp1WorkloadAdapter(streamFactory);

        builder.SetWorkloadFactory(_ =>
        {
            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                await adapter.ConnectAsync(ct);

                var tcs = new TaskCompletionSource<int>();
                adapter.Disconnected += () => tcs.TrySetResult(0);

                using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

                return await tcs.Task;
            };

            return new Hex1bTerminalBuildContext(adapter, runCallback);
        });

        return builder;
    }

    /// <summary>
    /// Configures the terminal as a muxer client with an already-connected stream.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <param name="stream">A bidirectional stream connected to the muxer server.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public static Hex1bTerminalBuilder WithHmp1Client(
        this Hex1bTerminalBuilder builder,
        Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return builder.WithHmp1Client(opts => opts.ConnectStream(_ => Task.FromResult(stream)));
    }
}

/// <summary>
/// Built-in transport helpers for creating stream sources.
/// </summary>
public static class Hmp1Transports
{
    /// <summary>
    /// Listens on a Unix domain socket and yields a stream for each connecting client.
    /// </summary>
    /// <param name="path">Path to the Unix domain socket file.</param>
    /// <param name="ct">Cancellation token that stops listening when cancelled.</param>
    /// <returns>An async enumerable of bidirectional streams.</returns>
    public static async IAsyncEnumerable<Stream> ListenUnixSocket(
        string path, [EnumeratorCancellation] CancellationToken ct)
    {
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
                yield return new NetworkStream(clientSocket, ownsSocket: true);
            }
        }
        finally
        {
            try { File.Delete(path); }
            catch { /* ignore */ }
        }
    }
}
