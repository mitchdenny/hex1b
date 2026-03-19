using System.Runtime.CompilerServices;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Represents a frame received from the remote terminal during an attach session.
/// </summary>
internal readonly record struct AttachFrame(AttachFrameKind Kind, ReadOnlyMemory<byte> Data)
{
    public static AttachFrame Output(ReadOnlyMemory<byte> data) => new(AttachFrameKind.Output, data);

    public static AttachFrame Resize(int width, int height)
    {
        var bytes = new byte[8];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), width);
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), height);
        return new(AttachFrameKind.Resize, bytes);
    }

    public static AttachFrame LeaderChanged(bool isLeader)
        => new(AttachFrameKind.LeaderChanged, new byte[] { isLeader ? (byte)1 : (byte)0 });

    public static AttachFrame Exit() => new(AttachFrameKind.Exit, default);

    /// <summary>Decodes width and height from a Resize frame.</summary>
    public (int Width, int Height) GetResize()
    {
        var span = Data.Span;
        return (BitConverter.ToInt32(span[..4]), BitConverter.ToInt32(span[4..8]));
    }

    /// <summary>Decodes the leader flag from a LeaderChanged frame.</summary>
    public bool GetIsLeader() => Data.Span[0] != 0;
}

internal enum AttachFrameKind { Output, Resize, LeaderChanged, Exit }

/// <summary>
/// Result of connecting to a remote terminal.
/// </summary>
internal sealed record AttachResult(
    bool Success,
    int Width,
    int Height,
    bool IsLeader,
    string? InitialScreen,
    string? Error);

/// <summary>
/// Abstracts the transport between the attach client and terminal host.
/// </summary>
internal interface IAttachTransport : IAsyncDisposable
{
    /// <summary>Connect and perform the attach handshake.</summary>
    Task<AttachResult> ConnectAsync(CancellationToken ct);

    /// <summary>Send raw input bytes to the remote terminal.</summary>
    Task SendInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct);

    /// <summary>Send a resize request (only effective if leader).</summary>
    Task SendResizeAsync(int width, int height, CancellationToken ct);

    /// <summary>Claim leadership.</summary>
    Task ClaimLeadAsync(CancellationToken ct);

    /// <summary>Detach from the terminal.</summary>
    Task DetachAsync(CancellationToken ct);

    /// <summary>Request remote terminal shutdown.</summary>
    Task ShutdownAsync(CancellationToken ct);

    /// <summary>Read frames from the remote terminal.</summary>
    IAsyncEnumerable<AttachFrame> ReadFramesAsync(CancellationToken ct);
}
