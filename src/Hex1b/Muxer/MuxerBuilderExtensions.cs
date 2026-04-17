namespace Hex1b.Muxer;

/// <summary>
/// Extension methods for <see cref="Hex1bTerminalBuilder"/> to configure muxer adapters.
/// </summary>
public static class MuxerBuilderExtensions
{
    /// <summary>
    /// Configures the terminal as a muxer server. The terminal runs a workload
    /// (e.g., PTY process) and serves it to remote clients over the Hex1b Muxer Protocol.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <param name="configure">Callback to configure the muxer server options (e.g., listener).</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithPtyProcess("bash")
    ///     .WithMuxerServer(server => server.ListenUnixSocket("/tmp/my-terminal.sock"))
    ///     .Build();
    ///
    /// await terminal.RunAsync();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithMuxerServer(
        this Hex1bTerminalBuilder builder,
        Action<MuxerServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MuxerServerOptions();
        configure(options);

        if (options.ListenerFactory is null)
            throw new InvalidOperationException(
                "Muxer server must have a listener configured. " +
                "Call ListenUnixSocket() or ListenStreams() on the options.");

        var adapter = new MuxerPresentationAdapter(options.Width, options.Height);
        var listenerFactory = options.ListenerFactory;

        builder.WithPresentation(adapter);

        // Start the listener when the terminal starts running.
        // The listener runs in the background and adds clients as they connect.
        builder.AddPresentationFilter(new MuxerListenerStartFilter(adapter, listenerFactory));

        return builder;
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
    ///     .WithMuxerClient(client => client.ConnectUnixSocket("/tmp/my-terminal.sock"))
    ///     .Build();
    ///
    /// await terminal.RunAsync();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithMuxerClient(
        this Hex1bTerminalBuilder builder,
        Action<MuxerClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MuxerClientOptions();
        configure(options);

        if (options.StreamFactory is null)
            throw new InvalidOperationException(
                "Muxer client must have a connection configured. " +
                "Call ConnectUnixSocket() or ConnectStream() on the options.");

        var streamFactory = options.StreamFactory;
        var adapter = new MuxerWorkloadAdapter(streamFactory);

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
    public static Hex1bTerminalBuilder WithMuxerClient(
        this Hex1bTerminalBuilder builder,
        Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return builder.WithMuxerClient(opts => opts.ConnectStream(_ => Task.FromResult(stream)));
    }
}
