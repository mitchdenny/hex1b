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
    /// <remarks>
    /// Awaited inline by the per-client write pump after the handshake
    /// frames have been written but before output streaming begins. A
    /// slow handler back-pressures handshake completion for that client
    /// only. Multicast (<c>+=</c>) is supported.
    /// </remarks>
    public Func<Hmp1ClientConnectedEventArgs, CancellationToken, Task>? OnClientConnected { get; set; }

    /// <summary>
    /// Invoked when a per-client session ends (clean disconnect or
    /// transport failure).
    /// </summary>
    /// <remarks>
    /// Per-session disposal runs in parallel with this callback so a
    /// slow handler does not delay transport cleanup. Receives
    /// <see cref="CancellationToken.None"/>.
    /// </remarks>
    public Func<Hmp1ClientDisconnectedEventArgs, CancellationToken, Task>? OnClientDisconnected { get; set; }

    /// <summary>
    /// Invoked when the producer's PTY dimensions change at runtime
    /// (typically as a result of a primary-peer Resize).
    /// </summary>
    /// <remarks>
    /// Awaited inline by the per-client read pump that processed the
    /// triggering frame. Multicast supported.
    /// </remarks>
    public Func<Hmp1ServerResizedEventArgs, CancellationToken, Task>? OnResized { get; set; }

    /// <summary>
    /// Invoked when the primary peer changes (including transitions to
    /// no-primary).
    /// </summary>
    /// <remarks>
    /// Awaited inline by the per-client read pump (or by
    /// <c>RemoveSession</c> when the previous primary disconnects).
    /// Multicast supported.
    /// </remarks>
    public Func<Hmp1ServerPrimaryChangedEventArgs, CancellationToken, Task>? OnPrimaryChanged { get; set; }
}
