using System.Threading.Channels;

namespace Hex1b.Diagnostics;

/// <summary>
/// The type of frame sent through an attach session.
/// </summary>
public enum AttachFrameType
{
    /// <summary>Raw terminal output (ANSI data).</summary>
    Output,

    /// <summary>Terminal was resized. Data is "cols,rows".</summary>
    Resize,

    /// <summary>Leader status changed. Data is "true" or "false".</summary>
    LeaderChanged,

    /// <summary>Terminal session ended.</summary>
    Exit
}

/// <summary>
/// A single frame received from an attach session.
/// </summary>
public readonly record struct AttachFrame(AttachFrameType Type, string? Data);

/// <summary>
/// Represents an active attach session to a terminal's diagnostics interface.
/// Provides streaming access to terminal output and the ability to send input.
/// </summary>
/// <remarks>
/// Created via <see cref="McpDiagnosticsPresentationFilter.CreateAttachSession"/>.
/// Dispose the session to disconnect and release resources.
/// </remarks>
public sealed class AttachSession : IAsyncDisposable
{
    private readonly McpDiagnosticsPresentationFilter _filter;
    private readonly Channel<AttachFrame> _channel;
    private bool _disposed;

    internal AttachSession(
        McpDiagnosticsPresentationFilter filter,
        Channel<AttachFrame> channel,
        int width,
        int height,
        bool isLeader,
        string? initialScreen)
    {
        _filter = filter;
        _channel = channel;
        Width = width;
        Height = height;
        IsLeader = isLeader;
        InitialScreen = initialScreen;
    }

    /// <summary>
    /// Reads output and control frames from the terminal.
    /// </summary>
    public ChannelReader<AttachFrame> Frames => _channel.Reader;

    /// <summary>
    /// Whether this session is the current resize leader.
    /// </summary>
    public bool IsLeader { get; internal set; }

    /// <summary>
    /// Current terminal width in columns.
    /// </summary>
    public int Width { get; internal set; }

    /// <summary>
    /// Current terminal height in rows.
    /// </summary>
    public int Height { get; internal set; }

    /// <summary>
    /// The initial ANSI screen capture at the time of attach.
    /// </summary>
    public string? InitialScreen { get; }

    /// <summary>
    /// Sends raw input bytes to the terminal.
    /// </summary>
    public Task SendInputAsync(byte[] data) => _filter.SendInputFromSessionAsync(this, data);

    /// <summary>
    /// Resizes the terminal. Only succeeds if this session is the leader.
    /// </summary>
    public Task SendResizeAsync(int width, int height) => _filter.SendResizeFromSessionAsync(this, width, height);

    /// <summary>
    /// Claims leadership for this session (controls resize).
    /// </summary>
    public Task ClaimLeadAsync() => _filter.ClaimLeadFromSessionAsync(this);

    /// <summary>
    /// Requests a graceful shutdown of the terminal host.
    /// </summary>
    public void RequestShutdown() => _filter.RequestShutdownFromSession();

    internal Channel<AttachFrame> Channel => _channel;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _filter.RemoveSession(this);
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
