#pragma warning disable HEX1B_SIXEL // Sixel API is experimental - internal usage is allowed

using System.Text;
using System.Threading.Channels;
using Hex1b.Input;

namespace Hex1b.Terminal;

/// <summary>
/// The core terminal that bridges workload and presentation adapters.
/// </summary>
/// <remarks>
/// <para>
/// This is the central component of the new terminal architecture. It:
/// <list type="bullet">
///   <item>Implements <see cref="IHex1bAppTerminalWorkloadAdapter"/> for Hex1bApp to use</item>
///   <item>Takes an <see cref="IHex1bTerminalPresentationAdapter"/> for actual I/O</item>
///   <item>Parses raw input bytes into <see cref="Hex1bEvent"/> objects</item>
///   <item>Forwards output to the presentation layer</item>
/// </list>
/// </para>
/// <para>
/// This is a minimal pass-through implementation. Future versions will add
/// pipeline layers for ANSI parsing, state tracking, delta rendering, etc.
/// </para>
/// </remarks>
public sealed class Hex1bTerminalCore : IHex1bAppTerminalWorkloadAdapter, IDisposable
{
    private readonly IHex1bTerminalPresentationAdapter _presentation;
    private readonly Channel<Hex1bEvent> _inputChannel;
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _disposed;
    private bool _inTuiMode;
    private Task? _inputProcessingTask;

    /// <summary>
    /// Creates a new terminal core with the specified presentation adapter.
    /// </summary>
    /// <param name="presentation">The presentation adapter for actual I/O.</param>
    public Hex1bTerminalCore(IHex1bTerminalPresentationAdapter presentation)
    {
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        _inputChannel = Channel.CreateUnbounded<Hex1bEvent>();

        // Subscribe to presentation events
        _presentation.Resized += OnPresentationResized;
        _presentation.Disconnected += OnPresentationDisconnected;
    }

    private void OnPresentationResized(int width, int height)
    {
        _inputChannel.Writer.TryWrite(new Hex1bResizeEvent(width, height));
    }

    private void OnPresentationDisconnected()
    {
        _inputChannel.Writer.TryComplete();
        Disconnected?.Invoke();
    }

    // === IHex1bAppTerminalWorkloadAdapter (app-side) ===

    /// <inheritdoc />
    public void Write(string text)
    {
        if (_disposed) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        // Fire and forget - presentation handles buffering
        _ = _presentation.WriteOutputAsync(bytes);
    }

    /// <inheritdoc />
    public void Write(ReadOnlySpan<byte> data)
    {
        if (_disposed) return;
        _ = _presentation.WriteOutputAsync(data.ToArray());
    }

    /// <inheritdoc />
    public void Flush()
    {
        if (_disposed) return;
        _presentation.FlushAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public ChannelReader<Hex1bEvent> InputEvents => _inputChannel.Reader;

    /// <inheritdoc />
    public int Width => _presentation.Width;

    /// <inheritdoc />
    public int Height => _presentation.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _presentation.Capabilities;

    /// <inheritdoc />
    public void EnterTuiMode()
    {
        if (_inTuiMode) return;
        _inTuiMode = true;
        _presentation.EnterTuiModeAsync().AsTask().GetAwaiter().GetResult();
        
        // Start the input processing loop (reads from presentation and writes to InputEvents channel)
        if (_inputProcessingTask == null)
        {
            _inputProcessingTask = Task.Run(() => ProcessInputAsync(_disposeCts.Token));
        }
    }

    /// <inheritdoc />
    public void ExitTuiMode()
    {
        if (!_inTuiMode) return;
        _inTuiMode = false;
        _presentation.ExitTuiModeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public void Clear()
    {
        Write("\x1b[2J\x1b[H");
    }

    /// <inheritdoc />
    public void SetCursorPosition(int left, int top)
    {
        Write($"\x1b[{top + 1};{left + 1}H");
    }

    // === IHex1bTerminalWorkloadAdapter (terminal-side) ===

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        // Not used in this direction - output goes directly to presentation
        return new ValueTask<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);
    }

    /// <inheritdoc />
    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        // Not used - input comes from presentation via input pump
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        // Resize events come through presentation.Resized event
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <summary>
    /// Starts the input processing loop. Call this to begin reading from presentation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task ProcessInputAsync(CancellationToken ct = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);

        try
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                var data = await _presentation.ReadInputAsync(linkedCts.Token);
                if (data.IsEmpty)
                {
                    break; // Disconnected
                }

                // Parse the input bytes into events
                await ParseAndDispatchInputAsync(data, linkedCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            _inputChannel.Writer.TryComplete();
        }
    }

    private async Task ParseAndDispatchInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var message = Encoding.UTF8.GetString(data.Span);
        var i = 0;

        while (i < message.Length && !ct.IsCancellationRequested)
        {
            // Check for DA1 response (terminal capability query response)
            if (TryParseDA1Response(message, i, out var da1Consumed))
            {
                i += da1Consumed;
                continue;
            }

            // Check for ANSI escape sequence
            if (message[i] == '\x1b' && i + 1 < message.Length)
            {
                if (message[i + 1] == '[')
                {
                    // CSI sequence
                    if (i + 2 < message.Length && message[i + 2] == '<')
                    {
                        // SGR mouse sequence
                        var (mouseEvent, mouseConsumed) = ParseSgrMouseSequence(message, i);
                        if (mouseEvent != null)
                        {
                            await _inputChannel.Writer.WriteAsync(mouseEvent, ct);
                            i += mouseConsumed;
                            continue;
                        }
                    }

                    var (csiEvent, csiConsumed) = ParseCsiSequence(message, i);
                    if (csiEvent != null)
                    {
                        await _inputChannel.Writer.WriteAsync(csiEvent, ct);
                    }
                    i += csiConsumed;
                }
                else if (message[i + 1] == 'O')
                {
                    // SS3 sequence
                    var (ss3Event, ss3Consumed) = ParseSS3Sequence(message, i);
                    if (ss3Event != null)
                    {
                        await _inputChannel.Writer.WriteAsync(ss3Event, ct);
                    }
                    i += ss3Consumed;
                }
                else
                {
                    // Plain escape
                    await _inputChannel.Writer.WriteAsync(
                        new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None), ct);
                    i++;
                }
            }
            else if (char.IsHighSurrogate(message[i]) && i + 1 < message.Length && char.IsLowSurrogate(message[i + 1]))
            {
                // Surrogate pair (emoji)
                var text = message.Substring(i, 2);
                await _inputChannel.Writer.WriteAsync(Hex1bKeyEvent.FromText(text), ct);
                i += 2;
            }
            else
            {
                // Regular character
                var keyEvent = ParseKeyInput(message[i]);
                if (keyEvent != null)
                {
                    await _inputChannel.Writer.WriteAsync(keyEvent, ct);
                }
                i++;
            }
        }
    }

    private static bool TryParseDA1Response(string message, int start, out int consumed)
    {
        consumed = 0;
        
        // DA1 response: ESC [ ? ... c
        if (start + 3 < message.Length && 
            message[start] == '\x1b' && 
            message[start + 1] == '[' && 
            message[start + 2] == '?')
        {
            // Find the terminating 'c'
            for (var j = start + 3; j < message.Length; j++)
            {
                if (message[j] == 'c')
                {
                    var response = message.Substring(start, j - start + 1);
                    Nodes.SixelNode.HandleDA1Response(response);
                    consumed = j - start + 1;
                    return true;
                }
            }
        }
        
        return false;
    }

    private static Hex1bKeyEvent? ParseKeyInput(char c)
    {
        return c switch
        {
            '\r' or '\n' => new Hex1bKeyEvent(Hex1bKey.Enter, c, Hex1bModifiers.None),
            '\t' => new Hex1bKeyEvent(Hex1bKey.Tab, c, Hex1bModifiers.None),
            '\x1b' => new Hex1bKeyEvent(Hex1bKey.Escape, c, Hex1bModifiers.None),
            '\x7f' or '\b' => new Hex1bKeyEvent(Hex1bKey.Backspace, c, Hex1bModifiers.None),
            ' ' => new Hex1bKeyEvent(Hex1bKey.Spacebar, c, Hex1bModifiers.None),
            >= 'a' and <= 'z' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'a'))), c, Hex1bModifiers.None),
            >= 'A' and <= 'Z' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'A'))), c, Hex1bModifiers.Shift),
            >= '0' and <= '9' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.D0 + (c - '0'))), c, Hex1bModifiers.None),
            >= '\x01' and <= '\x1a' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - '\x01'))), c, Hex1bModifiers.Control),
            _ when !char.IsControl(c) => new Hex1bKeyEvent(Hex1bKey.None, c, Hex1bModifiers.None),
            _ => null
        };
    }

    private static (Hex1bKeyEvent? Event, int Consumed) ParseCsiSequence(string message, int start)
    {
        if (start + 2 >= message.Length)
            return (null, 1);

        var i = start + 2; // Skip ESC [
        var param1 = 0;
        var param2 = 0;
        var hasParam2 = false;

        while (i < message.Length && char.IsDigit(message[i]))
        {
            param1 = param1 * 10 + (message[i] - '0');
            i++;
        }

        if (i < message.Length && message[i] == ';')
        {
            i++;
            while (i < message.Length && char.IsDigit(message[i]))
            {
                param2 = param2 * 10 + (message[i] - '0');
                hasParam2 = true;
                i++;
            }
        }

        if (i >= message.Length)
            return (null, i - start);

        var finalChar = message[i];
        i++;

        var modifiers = Hex1bModifiers.None;
        if (hasParam2 && param2 >= 2)
        {
            var modifierBits = param2 - 1;
            if ((modifierBits & 1) != 0) modifiers |= Hex1bModifiers.Shift;
            if ((modifierBits & 2) != 0) modifiers |= Hex1bModifiers.Alt;
            if ((modifierBits & 4) != 0) modifiers |= Hex1bModifiers.Control;
        }

        var key = finalChar switch
        {
            'A' => Hex1bKey.UpArrow,
            'B' => Hex1bKey.DownArrow,
            'C' => Hex1bKey.RightArrow,
            'D' => Hex1bKey.LeftArrow,
            'H' => Hex1bKey.Home,
            'F' => Hex1bKey.End,
            'Z' => Hex1bKey.Tab,
            '~' => ParseTildeSequence(param1),
            _ => Hex1bKey.None
        };

        if (key == Hex1bKey.None)
            return (null, i - start);

        if (finalChar == 'Z')
            modifiers |= Hex1bModifiers.Shift;

        return (new Hex1bKeyEvent(key, '\0', modifiers), i - start);
    }

    private static Hex1bKey ParseTildeSequence(int param)
    {
        return param switch
        {
            1 => Hex1bKey.Home,
            2 => Hex1bKey.Insert,
            3 => Hex1bKey.Delete,
            4 => Hex1bKey.End,
            5 => Hex1bKey.PageUp,
            6 => Hex1bKey.PageDown,
            _ => Hex1bKey.None
        };
    }

    private static (Hex1bKeyEvent? Event, int Consumed) ParseSS3Sequence(string message, int start)
    {
        if (start + 2 >= message.Length)
            return (null, 1);

        var finalChar = message[start + 2];

        var key = finalChar switch
        {
            'A' => Hex1bKey.UpArrow,
            'B' => Hex1bKey.DownArrow,
            'C' => Hex1bKey.RightArrow,
            'D' => Hex1bKey.LeftArrow,
            'H' => Hex1bKey.Home,
            'F' => Hex1bKey.End,
            'P' => Hex1bKey.F1,
            'Q' => Hex1bKey.F2,
            'R' => Hex1bKey.F3,
            'S' => Hex1bKey.F4,
            _ => Hex1bKey.None
        };

        if (key == Hex1bKey.None)
            return (null, 3);

        return (new Hex1bKeyEvent(key, '\0', Hex1bModifiers.None), 3);
    }

    private static (Hex1bMouseEvent? Event, int Consumed) ParseSgrMouseSequence(string message, int start)
    {
        if (start + 8 >= message.Length)
            return (null, 3);

        var i = start + 3; // Skip ESC [ <

        var terminatorIdx = -1;
        for (var j = i; j < message.Length; j++)
        {
            if (message[j] == 'M' || message[j] == 'm')
            {
                terminatorIdx = j;
                break;
            }
            if (!char.IsDigit(message[j]) && message[j] != ';')
            {
                return (null, 3);
            }
        }

        if (terminatorIdx < 0)
            return (null, 3);

        var sgrPart = message.Substring(i, terminatorIdx - i + 1);
        if (MouseParser.TryParseSgr(sgrPart, out var mouseEvent))
        {
            return (mouseEvent, terminatorIdx - start + 1);
        }

        return (null, 3);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _presentation.Resized -= OnPresentationResized;
        _presentation.Disconnected -= OnPresentationDisconnected;

        if (_inTuiMode)
        {
            ExitTuiMode();
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _inputChannel.Writer.TryComplete();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _presentation.Resized -= OnPresentationResized;
        _presentation.Disconnected -= OnPresentationDisconnected;

        if (_inTuiMode)
        {
            await _presentation.ExitTuiModeAsync();
            _inTuiMode = false;
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _inputChannel.Writer.TryComplete();

        await _presentation.DisposeAsync();
    }
}
