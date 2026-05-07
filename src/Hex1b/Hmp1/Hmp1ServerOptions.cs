namespace Hex1b;

/// <summary>
/// Configures an HMP v1 server listener built via the
/// <see cref="Hmp1BuilderExtensions.WithHmp1Server(Hex1bTerminalBuilder, System.Func{System.Threading.CancellationToken, System.Collections.Generic.IAsyncEnumerable{System.IO.Stream}}, System.Action{Hmp1ServerOptions}?)"/>
/// or
/// <see cref="Hmp1BuilderExtensions.WithHmp1UdsServer(Hex1bTerminalBuilder, string, System.Action{Hmp1ServerOptions}?)"/>
/// family of builder extensions.
/// </summary>
/// <remarks>
/// Carries an optional per-client stream wrap (TLS, compression,
/// framing) plus single-delegate event hooks invoked over the lifetime
/// of the server. Each hook is optional; null hooks are skipped.
/// </remarks>
public sealed class Hmp1ServerOptions
{
    /// <summary>
    /// Optional async stream-wrap applied to every accepted client
    /// connection before the server reads the ClientHello frame. Use
    /// this to layer TLS, compression, or other framing on top of the
    /// raw transport.
    /// </summary>
    public Func<Stream, Task<Stream>>? StreamTransform { get; set; }

    /// <summary>
    /// Invoked after a new HMP v1 client completes its handshake.
    /// </summary>
    public Action<Hmp1ClientConnectedEventArgs>? OnClientConnected { get; set; }

    /// <summary>
    /// Invoked when a per-client session ends (clean disconnect or
    /// transport failure).
    /// </summary>
    public Action<Hmp1ClientDisconnectedEventArgs>? OnClientDisconnected { get; set; }

    /// <summary>
    /// Invoked when the producer's PTY dimensions change at runtime
    /// (typically as a result of a primary-peer Resize).
    /// </summary>
    public Action<Hmp1ServerResizedEventArgs>? OnResized { get; set; }

    /// <summary>
    /// Invoked when the primary peer changes (including transitions to
    /// no-primary).
    /// </summary>
    public Action<Hmp1ServerPrimaryChangedEventArgs>? OnPrimaryChanged { get; set; }
}
