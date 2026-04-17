using System.Buffers.Binary;
using System.Text.Json;

namespace Hex1b.Muxer;

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
public static class MuxerProtocol
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
        MuxerFrameType type,
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
        if (type != MuxerFrameType.Output && type != MuxerFrameType.Input)
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
    public static async ValueTask<MuxerFrame?> ReadFrameAsync(
        Stream stream,
        CancellationToken ct = default)
    {
        var header = new byte[HeaderSize];
        var headerRead = await ReadExactlyAsync(stream, header, ct).ConfigureAwait(false);
        if (headerRead == 0)
            return null; // Stream closed

        if (headerRead < HeaderSize)
            throw new InvalidOperationException("Incomplete frame header received.");

        var type = (MuxerFrameType)header[0];
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

        return new MuxerFrame(type, payload);
    }

    /// <summary>
    /// Writes a Hello frame with the current protocol version and terminal dimensions.
    /// </summary>
    public static ValueTask WriteHelloAsync(
        Stream stream, int width, int height, CancellationToken ct = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(
            new HelloPayload { Version = Version, Width = width, Height = height },
            MuxerJsonContext.Default.HelloPayload);
        return WriteFrameAsync(stream, MuxerFrameType.Hello, json, ct);
    }

    /// <summary>
    /// Parses a Hello frame payload.
    /// </summary>
    public static HelloPayload ParseHello(ReadOnlyMemory<byte> payload)
    {
        var hello = JsonSerializer.Deserialize(payload.Span, MuxerJsonContext.Default.HelloPayload)
            ?? throw new InvalidOperationException("Failed to parse Hello payload.");
        if (hello.Version != Version)
            throw new InvalidOperationException($"Unsupported protocol version: {hello.Version}. Expected: {Version}.");
        return hello;
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
        return WriteFrameAsync(stream, MuxerFrameType.Resize, payload, ct);
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
        return WriteFrameAsync(stream, MuxerFrameType.Exit, payload, ct);
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
public readonly record struct MuxerFrame(MuxerFrameType Type, ReadOnlyMemory<byte> Payload);

/// <summary>
/// JSON payload for the <see cref="MuxerFrameType.Hello"/> frame.
/// </summary>
public sealed class HelloPayload
{
    /// <summary>Protocol version.</summary>
    public int Version { get; set; }

    /// <summary>Terminal width in columns.</summary>
    public int Width { get; set; }

    /// <summary>Terminal height in rows.</summary>
    public int Height { get; set; }
}
