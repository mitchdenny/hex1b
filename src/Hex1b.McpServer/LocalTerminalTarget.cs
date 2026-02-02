using System.Text;
using Hex1b.Automation;

namespace Hex1b.McpServer;

/// <summary>
/// A terminal target wrapping a local terminal session (child process launched by MCP server).
/// </summary>
public sealed class LocalTerminalTarget : ITerminalTarget
{
    private readonly TerminalSession _session;

    /// <summary>
    /// Creates a new local terminal target from an existing session.
    /// </summary>
    public LocalTerminalTarget(TerminalSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <inheritdoc />
    public string Id => _session.Id;

    /// <inheritdoc />
    public TerminalTargetType TargetType => TerminalTargetType.Local;

    /// <inheritdoc />
    public int Width => _session.Width;

    /// <inheritdoc />
    public int Height => _session.Height;

    /// <inheritdoc />
    public int ProcessId => _session.ProcessId;

    /// <inheritdoc />
    public bool IsAlive => !_session.HasExited;

    /// <inheritdoc />
    public DateTimeOffset StartedAt => _session.StartedAt;

    /// <inheritdoc />
    public string Name => _session.Command;

    /// <summary>
    /// Gets the underlying terminal session.
    /// </summary>
    public TerminalSession Session => _session;

    /// <inheritdoc />
    public Task SendInputAsync(string text, CancellationToken ct = default)
        => _session.SendInputAsync(text, ct);

    /// <inheritdoc />
    public Task SendKeyAsync(string key, string[]? modifiers = null, CancellationToken ct = default)
        => _session.SendKeyAsync(key, modifiers, ct);

    /// <inheritdoc />
    public async Task SendMouseClickAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
    {
        // SGR mouse encoding: ESC [ < button ; column ; row M (press) m (release)
        // Columns and rows are 1-based in the protocol
        var col = x + 1;
        var row = y + 1;
        var btn = (int)button;
        
        var mouseSequence = $"\x1b[<{btn};{col};{row}M\x1b[<{btn};{col};{row}m";
        await _session.SendInputAsync(mouseSequence, ct);
    }

    /// <inheritdoc />
    public Task<string> CaptureTextAsync(CancellationToken ct = default)
        => Task.FromResult(_session.CaptureText());

    /// <inheritdoc />
    public Task<string> CaptureSvgAsync(TerminalSvgOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(_session.CaptureSvg(options));

    /// <inheritdoc />
    public Task<string> CaptureAnsiAsync(TerminalAnsiOptions? options = null, CancellationToken ct = default)
    {
        // TerminalSession doesn't have CaptureAnsi, we need to add it or use snapshot directly
        // For now, capture via text (we can enhance this later)
        var text = _session.CaptureText();
        return Task.FromResult(text);
    }

    /// <inheritdoc />
    public Task<bool> WaitForTextAsync(string text, TimeSpan timeout, CancellationToken ct = default)
        => _session.WaitForTextAsync(text, timeout, ct);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
