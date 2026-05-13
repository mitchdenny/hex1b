using System.Runtime.CompilerServices;

namespace Hex1b;

/// <summary>
/// Extension methods for <see cref="Hex1bTerminalBuilder"/> that wire
/// up the HMP v1 (Hex1b Muxer Protocol v1) server and client adapters.
/// </summary>
/// <remarks>
/// <para>
/// This is the recommended (<em>"easy path"</em>) surface for building
/// HMP v1 producers and consumers. The extensions construct the
/// underlying <see cref="Hmp1WorkloadAdapter"/> /
/// <see cref="Hmp1PresentationAdapter"/> internally and deliver an
/// <see cref="IHmp1ConnectionHandle"/> via
/// <see cref="Hmp1ClientOptions.OnConnected"/>, so user code never has
/// to traffic in adapter types.
/// </para>
/// <para>
/// Advanced consumers that need to drive
/// <see cref="Hmp1WorkloadAdapter.ConnectAsync"/> before assembling the
/// surrounding terminal (typically because the builder needs producer
/// dimensions before <c>Build()</c>) construct the adapter directly
/// and pass it to <c>WithWorkload</c>; that path stays available but
/// is deliberately outside this extension family.
/// </para>
/// </remarks>
public static class Hmp1BuilderExtensions
{
    // ─────────────────────────────────────────────────────────────────────
    // Server side
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds an HMP v1 server listener that accepts client connections from
    /// the given async stream source. Can be called multiple times to serve
    /// over multiple transports simultaneously.
    /// </summary>
    public static Hex1bTerminalBuilder WithHmp1Server(
        this Hex1bTerminalBuilder builder,
        Func<CancellationToken, IAsyncEnumerable<Stream>> streamSource)
        => WithHmp1Server(builder, streamSource, configure: null);

    /// <summary>
    /// Adds an HMP v1 server listener that accepts client connections from
    /// the given async stream source, with an
    /// <see cref="Hmp1ServerOptions"/> configuration callback for stream
    /// transforms and lifecycle event hooks.
    /// </summary>
    public static Hex1bTerminalBuilder WithHmp1Server(
        this Hex1bTerminalBuilder builder,
        Func<CancellationToken, IAsyncEnumerable<Stream>> streamSource,
        Action<Hmp1ServerOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(streamSource);

        Hmp1ServerOptions? options = null;
        if (configure is not null)
        {
            options = new Hmp1ServerOptions();
            configure(options);
        }

        var filter = Hmp1ListenerStartFilter.GetOrCreate(builder, options, out _);
        filter.AddStreamSource(streamSource, options?.StreamTransform);

        return builder;
    }

    /// <summary>
    /// Adds an HMP v1 server listener on a Unix domain socket. Can be
    /// called multiple times to serve over multiple sockets.
    /// </summary>
    public static Hex1bTerminalBuilder WithHmp1UdsServer(
        this Hex1bTerminalBuilder builder,
        string socketPath)
        => WithHmp1UdsServer(builder, socketPath, configure: null);

    /// <summary>
    /// Adds an HMP v1 server listener on a Unix domain socket, with an
    /// <see cref="Hmp1ServerOptions"/> configuration callback for stream
    /// transforms (e.g. TLS) and lifecycle event hooks.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithPtyProcess("bash")
    ///     .WithHmp1UdsServer("/tmp/my.sock", opt =>
    ///     {
    ///         opt.StreamTransform   = DemoTls.AuthenticateAsServerAsync;
    ///         opt.OnClientConnected = e =>
    ///             Console.WriteLine($"client {e.PeerId} ({e.DisplayName}) connected");
    ///     })
    ///     .Build();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithHmp1UdsServer(
        this Hex1bTerminalBuilder builder,
        string socketPath,
        Action<Hmp1ServerOptions>? configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        return WithHmp1Server(
            builder,
            ct => Hmp1Transports.ListenUnixSocket(socketPath, ct),
            configure);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Client side
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures the terminal as an HMP v1 client built from a fully
    /// populated options bag. Canonical overload — every other
    /// <c>WithHmp1Client</c> / <c>WithHmp1Stream</c> /
    /// <c>WithHmp1UdsClient</c> variant in this class delegates here.
    /// </summary>
    public static Hex1bTerminalBuilder WithHmp1Client(
        this Hex1bTerminalBuilder builder,
        Hmp1ClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var adapter = new Hmp1WorkloadAdapter(options);

        builder.SetWorkloadFactory(_ =>
        {
            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                await adapter.ConnectAsync(ct);

                // Use the adapter's TCS-backed DisconnectedTask instead of
                // subscribing to an event — keeps this internal wait
                // independent of the user's OnDisconnected callback
                // duration (which the framework should not block on)
                // and avoids racing with any callback the user supplied
                // via options.
                var disconnect = adapter.DisconnectedTask;
                var cancelled = Task.Delay(Timeout.Infinite, ct);
                var first = await Task.WhenAny(disconnect, cancelled).ConfigureAwait(false);
                if (first != disconnect)
                {
                    // Cancellation won the race; surface it.
                    ct.ThrowIfCancellationRequested();
                }

                return 0;
            };

            return new Hex1bTerminalBuildContext(adapter, runCallback);
        });

        return builder;
    }

    /// <summary>
    /// Configures the terminal as an HMP v1 client connecting through the
    /// given stream factory.
    /// </summary>
    public static Hex1bTerminalBuilder WithHmp1Client(
        this Hex1bTerminalBuilder builder,
        Func<CancellationToken, Task<Stream>> streamFactory)
        => WithHmp1Client(builder, streamFactory, configure: null);

    /// <summary>
    /// Configures the terminal as an HMP v1 client connecting through the
    /// given stream factory, with an <see cref="Hmp1ClientOptions"/>
    /// configure callback for handshake hints and event hooks. The
    /// transport is pre-populated and cannot be replaced from the
    /// callback.
    /// </summary>
    /// <example>
    /// <code>
    /// IHmp1ConnectionHandle? connection = null;
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithHmp1Client(ct => MyTransport.ConnectAsync(ct), opt =>
    ///     {
    ///         opt.DefaultRole = Hmp1Role.Secondary;
    ///         opt.OnConnected = e => connection = e.Connection;
    ///     })
    ///     .Build();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithHmp1Client(
        this Hex1bTerminalBuilder builder,
        Func<CancellationToken, Task<Stream>> streamFactory,
        Action<Hmp1ClientOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);

        var options = new Hmp1ClientOptions { StreamFactory = streamFactory };
        configure?.Invoke(options);

        return WithHmp1Client(builder, options);
    }

    /// <summary>
    /// Configures the terminal as an HMP v1 client over an already-connected
    /// bidirectional stream (e.g. an in-memory pipe pair, a WebSocket-backed
    /// stream, or a test fixture).
    /// </summary>
    public static Hex1bTerminalBuilder WithHmp1Stream(
        this Hex1bTerminalBuilder builder,
        Stream stream)
        => WithHmp1Stream(builder, stream, configure: null);

    /// <summary>
    /// Configures the terminal as an HMP v1 client over an already-connected
    /// bidirectional stream, with an <see cref="Hmp1ClientOptions"/>
    /// configure callback for handshake hints and event hooks.
    /// </summary>
    public static Hex1bTerminalBuilder WithHmp1Stream(
        this Hex1bTerminalBuilder builder,
        Stream stream,
        Action<Hmp1ClientOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return WithHmp1Client(builder, _ => Task.FromResult(stream), configure);
    }

    /// <summary>
    /// Configures the terminal as an HMP v1 client connecting to a Unix
    /// domain socket.
    /// </summary>
    public static Hex1bTerminalBuilder WithHmp1UdsClient(
        this Hex1bTerminalBuilder builder,
        string socketPath)
        => WithHmp1UdsClient(builder, socketPath, configure: null);

    /// <summary>
    /// Configures the terminal as an HMP v1 client connecting to a Unix
    /// domain socket, with an <see cref="Hmp1ClientOptions"/> configure
    /// callback for handshake hints, an optional stream transform (e.g.
    /// TLS), and event hooks.
    /// </summary>
    /// <example>
    /// <code>
    /// // TLS-wrapped UDS client (replaces previous Func&lt;Stream,Task&lt;Stream&gt;&gt; overload):
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithHmp1UdsClient("/tmp/my.sock", opt =>
    ///     {
    ///         opt.StreamTransform = DemoTls.AuthenticateAsClientAsync;
    ///         opt.DefaultRole     = Hmp1Role.Secondary;
    ///     })
    ///     .Build();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithHmp1UdsClient(
        this Hex1bTerminalBuilder builder,
        string socketPath,
        Action<Hmp1ClientOptions>? configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        return WithHmp1Client(
            builder,
            ct => Hmp1Transports.ConnectUnixSocket(socketPath, ct),
            configure);
    }
}

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

/// <summary>
/// Backoff policy used by retrying transports such as
/// <see cref="Hmp1Transports.RetryingUnixSocket(string, RetryPolicy?)"/>.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>Initial delay before the second attempt.</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Maximum delay between attempts.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Multiplier applied to the delay after each failed attempt.</summary>
    public double Multiplier { get; init; } = 1.5;

    /// <summary>
    /// Maximum number of attempts before giving up. Zero means infinite (the
    /// default) — retries continue until the supplied <see cref="CancellationToken"/>
    /// is cancelled.
    /// </summary>
    public int MaxAttempts { get; init; } = 0;

    /// <summary>
    /// Optional hook invoked after each failed attempt (before the delay).
    /// </summary>
    public Action<RetryAttemptFailedEventArgs>? OnAttemptFailed { get; init; }

    /// <summary>
    /// Reasonable defaults for waiting on a UDS producer to come online:
    /// 200 ms initial, 1.5× backoff, capped at 2 s, infinite attempts.
    /// </summary>
    public static RetryPolicy DefaultUnixSocket { get; } = new();
}

/// <summary>
/// Carries information about a single failed retry attempt.
/// </summary>
public sealed record RetryAttemptFailedEventArgs(int AttemptNumber, TimeSpan NextDelay, Exception Error);
