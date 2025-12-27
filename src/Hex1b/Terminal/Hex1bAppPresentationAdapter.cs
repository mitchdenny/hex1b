using System.Text;
using System.Threading.Channels;

namespace Hex1b.Terminal;

/// <summary>
/// Presentation adapter for embedding a Hex1bTerminal as a display target.
/// Allows a Hex1bApp to host other terminal experiences (like tmux-style nested terminals).
/// </summary>
/// <remarks>
/// <para>
/// This adapter bridges a Hex1bTerminal (which acts as a virtual terminal emulator)
/// with the IHex1bTerminalPresentationAdapter interface, allowing it to be used as
/// a presentation target for another terminal or application.
/// </para>
/// <para>
/// Data flow:
/// <list type="bullet">
///   <item>Terminal output goes TO the embedded Hex1bTerminal (which renders to a buffer)</item>
///   <item>User input comes FROM key events passed via SendInput methods</item>
/// </list>
/// </para>
/// <para>
/// The embedded terminal maintains its own screen buffer that can be queried
/// via GetRenderedOutput() for display in a parent TUI.
/// </para>
/// </remarks>
public sealed class Hex1bAppPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private readonly Hex1bTerminal _terminal;
    private readonly Channel<byte[]> _inputChannel;
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new presentation adapter wrapping a Hex1bTerminal.
    /// </summary>
    /// <param name="terminal">The terminal to use as the presentation target.</param>
    public Hex1bAppPresentationAdapter(Hex1bTerminal terminal)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        
        _inputChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public int Width => _terminal.Width;

    /// <inheritdoc />
    public int Height => _terminal.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _terminal.Capabilities;

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;
    
    /// <summary>
    /// Event fired when output is written to the terminal.
    /// Used by TerminalNode to know when to repaint.
    /// </summary>
    public event Action? OutputReceived;

    /// <summary>
    /// Write output TO the embedded terminal (render ANSI sequences).
    /// </summary>
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) return ValueTask.CompletedTask;
        
        // Convert bytes to string and process through the terminal
        var text = Encoding.UTF8.GetString(data.Span);
        _terminal.ProcessOutput(text);
        
        // Notify listeners that output was received (for TerminalNode dirty tracking)
        OutputReceived?.Invoke();
        
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Read input FROM the input channel (keyboard/mouse events as bytes).
    /// </summary>
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed) return ReadOnlyMemory<byte>.Empty;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);

        try
        {
            if (await _inputChannel.Reader.WaitToReadAsync(linkedCts.Token))
            {
                if (_inputChannel.Reader.TryRead(out var bytes))
                {
                    return bytes;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (ChannelClosedException)
        {
            // Channel completed
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <summary>
    /// Send input to the embedded terminal (for forwarding from parent app).
    /// </summary>
    /// <param name="data">Raw input bytes (ANSI sequences, key codes, etc.)</param>
    public void SendInput(ReadOnlySpan<byte> data)
    {
        if (_disposed) return;
        _inputChannel.Writer.TryWrite(data.ToArray());
    }

    /// <summary>
    /// Send text input to the embedded terminal.
    /// </summary>
    /// <param name="text">Text to send as input</param>
    public void SendInput(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        SendInput(Encoding.UTF8.GetBytes(text));
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        // The terminal handles its own buffering
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask EnterTuiModeAsync(CancellationToken ct = default)
    {
        // The embedded terminal is already in a virtual "TUI mode"
        // No special setup needed
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ExitTuiModeAsync(CancellationToken ct = default)
    {
        // No cleanup needed for embedded terminal
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Resize the embedded terminal.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (_disposed) return;
        
        _terminal.Resize(width, height);
        Resized?.Invoke(width, height);
    }

    /// <summary>
    /// Gets the rendered output from the terminal as text.
    /// </summary>
    /// <returns>String representing the terminal display.</returns>
    public string GetRenderedOutput()
    {
        if (_disposed) return string.Empty;
        return _terminal.GetScreenText();
    }
    
    /// <summary>
    /// Gets the rendered output from the terminal as a string array (one per line).
    /// </summary>
    /// <returns>Array of strings representing each line of the terminal display.</returns>
    public string[] GetRenderedLines()
    {
        if (_disposed) return Array.Empty<string>();
        var text = _terminal.GetScreenText();
        return text.Split('\n');
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _disposeCts.Cancel();
        _inputChannel.Writer.TryComplete();
        
        Disconnected?.Invoke();
        
        await _terminal.DisposeAsync();
        _disposeCts.Dispose();
    }
}
