using System.Buffers.Binary;
using System.Text.Json;

namespace Hex1b;

/// <summary>
/// Reads and writes Hex1b Muxer Protocol (HMP) frames over a <see cref="Stream"/>.
/// </summary>
/// <remarks>
/// <para>
/// Frame format: <c>[type:1B][length:4B LE][payload:N bytes]</c>.
/// </para>
/// <para>
/// See docs/muxer-protocol.md for the full protocol specification.
/// </para>
/// </remarks>
internal static class Hmp1Protocol
{
    /// <summary>
    /// Current protocol version.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// Size of the frame header in bytes (1 byte type + 4 bytes length).
    /// </summary>
    internal const int HeaderSize = 5;

    /// <summary>
    /// Maximum payload size (16 MB) to prevent runaway reads.
    /// </summary>
    internal const int MaxPayloadSize = 16 * 1024 * 1024;

    /// <summary>
    /// Writes a frame to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="type">The frame type.</param>
    /// <param name="payload">The frame payload.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async ValueTask WriteFrameAsync(
        Stream stream,
        Hmp1FrameType type,
        ReadOnlyMemory<byte> payload,
        CancellationToken ct = default)
    {
        var header = new byte[HeaderSize];
        header[0] = (byte)type;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1), payload.Length);

        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        if (payload.Length > 0)
        {
            await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        }

        // Only flush control frames (Hello, StateSync, Resize, Exit), not Output.
        // Output frames are high-frequency and flushing each one defeats batching.
        if (type != Hmp1FrameType.Output && type != Hmp1FrameType.Input)
        {
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads a complete frame from the stream.
    /// Returns <c>null</c> if the stream has ended.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The frame, or <c>null</c> if the stream is closed.</returns>
    /// <exception cref="InvalidOperationException">The frame is malformed or exceeds size limits.</exception>
    public static async ValueTask<Hmp1Frame?> ReadFrameAsync(
        Stream stream,
        CancellationToken ct = default)
    {
        var header = new byte[HeaderSize];
        var headerRead = await ReadExactlyAsync(stream, header, ct).ConfigureAwait(false);
        if (headerRead == 0)
            return null; // Stream closed

        if (headerRead < HeaderSize)
            throw new InvalidOperationException("Incomplete frame header received.");

        var type = (Hmp1FrameType)header[0];
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1));

        if (length < 0 || length > MaxPayloadSize)
            throw new InvalidOperationException($"Invalid frame payload length: {length}");

        ReadOnlyMemory<byte> payload;
        if (length > 0)
        {
            var buffer = new byte[length];
            var payloadRead = await ReadExactlyAsync(stream, buffer, ct).ConfigureAwait(false);
            if (payloadRead < length)
                throw new InvalidOperationException("Incomplete frame payload received.");
            payload = buffer;
        }
        else
        {
            payload = ReadOnlyMemory<byte>.Empty;
        }

        return new Hmp1Frame(type, payload);
    }

    /// <summary>
    /// Writes a Hello frame with the current protocol version, terminal dimensions,
    /// the assigned peer ID, the current primary peer ID, and the roster of existing peers.
    /// </summary>
    public static ValueTask WriteHelloAsync(
        Stream stream,
        int width,
        int height,
        string peerId,
        string? primaryPeerId,
        IReadOnlyList<HelloPeerInfo> peers,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(
            new HelloPayload
            {
                Version = Version,
                Width = width,
                Height = height,
                PeerId = peerId,
                PrimaryPeerId = primaryPeerId,
                Peers = peers.ToList()
            },
            Hmp1JsonContext.Default.HelloPayload);
        return WriteFrameAsync(stream, Hmp1FrameType.Hello, json, ct);
    }

    /// <summary>
    /// Writes a Hello frame with only dimensions populated (used internally for
    /// cases where peer routing is not yet wired — kept for backward source
    /// compatibility with single-client HMP1 callers and test helpers).
    /// </summary>
    public static ValueTask WriteHelloAsync(
        Stream stream, int width, int height, CancellationToken ct = default)
        => WriteHelloAsync(stream, width, height, peerId: string.Empty, primaryPeerId: null, peers: Array.Empty<HelloPeerInfo>(), ct);

    /// <summary>
    /// Parses a Hello frame payload.
    /// </summary>
    public static HelloPayload ParseHello(ReadOnlyMemory<byte> payload)
    {
        var hello = JsonSerializer.Deserialize(payload.Span, Hmp1JsonContext.Default.HelloPayload)
            ?? throw new InvalidOperationException("Failed to parse Hello payload.");
        if (hello.Version != Version)
            throw new InvalidOperationException($"Unsupported protocol version: {hello.Version}. Expected: {Version}.");
        // Peers list defaults to empty rather than null for ergonomic enumeration.
        hello.Peers ??= [];
        return hello;
    }

    /// <summary>
    /// Writes a ClientHello frame with the client's friendly name and role hint.
    /// Sent by clients immediately on connect, before the server's Hello frame.
    /// </summary>
    public static ValueTask WriteClientHelloAsync(
        Stream stream,
        string? displayName,
        string? defaultRole,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(
            new ClientHelloPayload
            {
                DisplayName = displayName,
                DefaultRole = defaultRole
            },
            Hmp1JsonContext.Default.ClientHelloPayload);
        return WriteFrameAsync(stream, Hmp1FrameType.ClientHello, json, ct);
    }

    /// <summary>
    /// Parses a ClientHello frame payload. An empty / missing payload is treated as
    /// an anonymous client with no defaults.
    /// </summary>
    public static ClientHelloPayload ParseClientHello(ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty)
            return new ClientHelloPayload();
        return JsonSerializer.Deserialize(payload.Span, Hmp1JsonContext.Default.ClientHelloPayload)
            ?? new ClientHelloPayload();
    }

    /// <summary>
    /// Writes a RequestPrimary frame.
    /// </summary>
    public static ValueTask WriteRequestPrimaryAsync(
        Stream stream, int cols, int rows, CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(
            new RequestPrimaryPayload { Cols = cols, Rows = rows },
            Hmp1JsonContext.Default.RequestPrimaryPayload);
        return WriteFrameAsync(stream, Hmp1FrameType.RequestPrimary, json, ct);
    }

    /// <summary>
    /// Parses a RequestPrimary frame payload.
    /// </summary>
    public static RequestPrimaryPayload ParseRequestPrimary(ReadOnlyMemory<byte> payload)
    {
        return JsonSerializer.Deserialize(payload.Span, Hmp1JsonContext.Default.RequestPrimaryPayload)
            ?? throw new InvalidOperationException("Failed to parse RequestPrimary payload.");
    }

    /// <summary>
    /// Writes a RoleChange frame announcing the new primary (or null when the
    /// previous primary disconnected with no replacement).
    /// </summary>
    public static ValueTask WriteRoleChangeAsync(
        Stream stream,
        string? primaryPeerId,
        int width,
        int height,
        string reason,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(
            new RoleChangePayload
            {
                PrimaryPeerId = primaryPeerId,
                Width = width,
                Height = height,
                Reason = reason
            },
            Hmp1JsonContext.Default.RoleChangePayload);
        return WriteFrameAsync(stream, Hmp1FrameType.RoleChange, json, ct);
    }

    /// <summary>
    /// Parses a RoleChange frame payload.
    /// </summary>
    public static RoleChangePayload ParseRoleChange(ReadOnlyMemory<byte> payload)
    {
        return JsonSerializer.Deserialize(payload.Span, Hmp1JsonContext.Default.RoleChangePayload)
            ?? throw new InvalidOperationException("Failed to parse RoleChange payload.");
    }

    /// <summary>
    /// Writes a PeerJoin frame.
    /// </summary>
    public static ValueTask WritePeerJoinAsync(
        Stream stream, string peerId, string? displayName, CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(
            new PeerJoinPayload { PeerId = peerId, DisplayName = displayName },
            Hmp1JsonContext.Default.PeerJoinPayload);
        return WriteFrameAsync(stream, Hmp1FrameType.PeerJoin, json, ct);
    }

    /// <summary>
    /// Parses a PeerJoin frame payload.
    /// </summary>
    public static PeerJoinPayload ParsePeerJoin(ReadOnlyMemory<byte> payload)
    {
        return JsonSerializer.Deserialize(payload.Span, Hmp1JsonContext.Default.PeerJoinPayload)
            ?? throw new InvalidOperationException("Failed to parse PeerJoin payload.");
    }

    /// <summary>
    /// Writes a PeerLeave frame.
    /// </summary>
    public static ValueTask WritePeerLeaveAsync(
        Stream stream, string peerId, CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(
            new PeerLeavePayload { PeerId = peerId },
            Hmp1JsonContext.Default.PeerLeavePayload);
        return WriteFrameAsync(stream, Hmp1FrameType.PeerLeave, json, ct);
    }

    /// <summary>
    /// Parses a PeerLeave frame payload.
    /// </summary>
    public static PeerLeavePayload ParsePeerLeave(ReadOnlyMemory<byte> payload)
    {
        return JsonSerializer.Deserialize(payload.Span, Hmp1JsonContext.Default.PeerLeavePayload)
            ?? throw new InvalidOperationException("Failed to parse PeerLeave payload.");
    }

    /// <summary>
    /// Writes a Resize frame.
    /// </summary>
    public static ValueTask WriteResizeAsync(
        Stream stream, int width, int height, CancellationToken ct = default)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0), width);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), height);
        return WriteFrameAsync(stream, Hmp1FrameType.Resize, payload, ct);
    }

    /// <summary>
    /// Parses a Resize frame payload into width and height.
    /// </summary>
    public static (int Width, int Height) ParseResize(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length < 8)
            throw new InvalidOperationException("Resize payload too short.");
        var width = BinaryPrimitives.ReadInt32LittleEndian(payload.Span);
        var height = BinaryPrimitives.ReadInt32LittleEndian(payload.Span[4..]);
        return (width, height);
    }

    /// <summary>
    /// Writes an Exit frame.
    /// </summary>
    public static ValueTask WriteExitAsync(
        Stream stream, int exitCode, CancellationToken ct = default)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(payload, exitCode);
        return WriteFrameAsync(stream, Hmp1FrameType.Exit, payload, ct);
    }

    /// <summary>
    /// Parses an Exit frame payload into an exit code.
    /// </summary>
    public static int ParseExitCode(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length < 4)
            return 0;
        return BinaryPrimitives.ReadInt32LittleEndian(payload.Span);
    }

    /// <summary>
    /// Reads exactly <paramref name="buffer"/>.Length bytes from the stream.
    /// Returns the number of bytes actually read (0 means stream closed, less than requested means premature close).
    /// </summary>
    private static async ValueTask<int> ReadExactlyAsync(
        Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead), ct).ConfigureAwait(false);
            if (read == 0)
                return totalRead;
            totalRead += read;
        }
        return totalRead;
    }
}

/// <summary>
/// A single HMP protocol frame.
/// </summary>
/// <param name="Type">The frame type.</param>
/// <param name="Payload">The raw payload bytes.</param>
internal readonly record struct Hmp1Frame(Hmp1FrameType Type, ReadOnlyMemory<byte> Payload);

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.Hello"/> frame.
/// </summary>
internal sealed class HelloPayload
{
    /// <summary>Protocol version. Always <see cref="Hmp1Protocol.Version"/>.</summary>
    public int Version { get; set; }

    /// <summary>Terminal width in columns.</summary>
    public int Width { get; set; }

    /// <summary>Terminal height in rows.</summary>
    public int Height { get; set; }

    /// <summary>
    /// Peer ID assigned to the receiving client by the server. Stable for
    /// the lifetime of the connection.
    /// </summary>
    public string? PeerId { get; set; }

    /// <summary>
    /// The peer ID of the current primary, or null when no peer is currently
    /// primary (initial state, or after the previous primary disconnected).
    /// </summary>
    public string? PrimaryPeerId { get; set; }

    /// <summary>
    /// Roster of other peers currently connected to the same producer
    /// (excluding the receiving client).
    /// </summary>
    public List<HelloPeerInfo>? Peers { get; set; }
}

/// <summary>
/// Roster entry inside <see cref="HelloPayload.Peers"/> and
/// <see cref="Hmp1FrameType.PeerJoin"/> payloads.
/// </summary>
internal sealed class HelloPeerInfo
{
    /// <summary>Peer ID.</summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>Optional human-readable label.</summary>
    public string? DisplayName { get; set; }
}

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.ClientHello"/> frame
/// (client → server, sent immediately on connect).
/// </summary>
internal sealed class ClientHelloPayload
{
    /// <summary>
    /// Optional human-readable label that the producer surfaces in its peer
    /// roster (e.g. "dashboard", "aspire-cli").
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional default-role hint:
    /// <c>"viewer"</c> requests that the client not be auto-promoted on first
    /// attach; <c>"interactive"</c> is the inverse hint. Currently the producer
    /// never auto-promotes, so this is preserved only as a UX hint reachable
    /// to the server-side consumer code.
    /// </summary>
    public string? DefaultRole { get; set; }
}

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.RequestPrimary"/> frame.
/// </summary>
internal sealed class RequestPrimaryPayload
{
    /// <summary>Requested PTY width in columns.</summary>
    public int Cols { get; set; }

    /// <summary>Requested PTY height in rows.</summary>
    public int Rows { get; set; }
}

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.RoleChange"/> frame.
/// </summary>
internal sealed class RoleChangePayload
{
    /// <summary>The peer ID of the new primary, or null when no peer is primary.</summary>
    public string? PrimaryPeerId { get; set; }

    /// <summary>Current PTY width.</summary>
    public int Width { get; set; }

    /// <summary>Current PTY height.</summary>
    public int Height { get; set; }

    /// <summary>
    /// Reason for the change (free-form, currently one of <c>"RequestPrimary"</c>
    /// or <c>"PrimaryDisconnected"</c>).
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.PeerJoin"/> frame.
/// </summary>
internal sealed class PeerJoinPayload
{
    /// <summary>Peer ID of the joining client.</summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>Optional human-readable label of the joining client.</summary>
    public string? DisplayName { get; set; }
}

/// <summary>
/// JSON payload for the <see cref="Hmp1FrameType.PeerLeave"/> frame.
/// </summary>
internal sealed class PeerLeavePayload
{
    /// <summary>Peer ID of the leaving client.</summary>
    public string PeerId { get; set; } = string.Empty;
}
