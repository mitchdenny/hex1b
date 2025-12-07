using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Theming;

namespace Hex1b;

/// <summary>
/// WebSocket-based terminal implementation for browser-based TUI.
/// Bridges WebSocket connections to the Hex1b terminal abstraction.
/// </summary>
public sealed class WebSocketHex1bTerminal : IHex1bTerminal, IDisposable
{
    private readonly WebSocket _webSocket;
    private readonly Channel<Hex1bInputEvent> _inputChannel;
    private readonly CancellationTokenSource _disposeCts;
    private int _width;
    private int _height;
    private bool _disposed;

    /// <summary>
    /// Event raised when the terminal is resized by the client.
    /// </summary>
    public event Action<int, int>? OnResize;

    /// <summary>
    /// Creates a new WebSocket terminal with the specified dimensions.
    /// </summary>
    /// <param name="webSocket">The WebSocket connection to the client.</param>
    /// <param name="width">Initial terminal width in characters.</param>
    /// <param name="height">Initial terminal height in lines.</param>
    public WebSocketHex1bTerminal(WebSocket webSocket, int width = 80, int height = 24)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _width = width;
        _height = height;
        _inputChannel = Channel.CreateUnbounded<Hex1bInputEvent>();
        _disposeCts = new CancellationTokenSource();
    }

    /// <inheritdoc />
    public int Width => _width;

    /// <inheritdoc />
    public int Height => _height;

    /// <inheritdoc />
    public ChannelReader<Hex1bInputEvent> InputEvents => _inputChannel.Reader;

    /// <inheritdoc />
    public void Write(string text)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open)
            return;

        var bytes = Encoding.UTF8.GetBytes(text);
        // Fire and forget - we don't want to block on writing
        _ = _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    /// <inheritdoc />
    public void Clear()
    {
        Write("\x1b[2J\x1b[H");
    }

    /// <inheritdoc />
    public void SetCursorPosition(int left, int top)
    {
        // ANSI cursor position is 1-based
        Write($"\x1b[{top + 1};{left + 1}H");
    }

    /// <inheritdoc />
    public void EnterAlternateScreen()
    {
        Write("\x1b[?1049h\x1b[?25l\x1b[2J\x1b[H");
    }

    /// <inheritdoc />
    public void ExitAlternateScreen()
    {
        Write("\x1b[?25h\x1b[?1049l");
    }

    /// <summary>
    /// Starts processing input from the WebSocket connection.
    /// This method runs until the WebSocket closes or cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop processing.</param>
    public async Task ProcessInputAsync(CancellationToken cancellationToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        var buffer = new byte[1024];

        try
        {
            while (_webSocket.State == WebSocketState.Open && !linkedCts.Token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(buffer, linkedCts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessageAsync(message, linkedCts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (WebSocketException)
        {
            // Connection closed
        }
        finally
        {
            _inputChannel.Writer.TryComplete();
        }
    }

    private async Task ProcessMessageAsync(string message, CancellationToken cancellationToken)
    {
        // Try to parse as JSON control message first
        if (TryParseControlMessage(message))
        {
            return;
        }

        // Process the message, looking for ANSI escape sequences
        var i = 0;
        while (i < message.Length)
        {
            // Check for ANSI escape sequence
            if (message[i] == '\x1b' && i + 1 < message.Length && message[i + 1] == '[')
            {
                var (inputEvent, consumed) = ParseAnsiSequence(message, i);
                if (inputEvent != null)
                {
                    await _inputChannel.Writer.WriteAsync(inputEvent, cancellationToken);
                }
                i += consumed;
            }
            else if (message[i] == '\x1b' && i + 1 < message.Length && message[i + 1] == 'O')
            {
                // SS3 sequences (e.g., \x1bOA for arrow keys in some modes)
                var (inputEvent, consumed) = ParseSS3Sequence(message, i);
                if (inputEvent != null)
                {
                    await _inputChannel.Writer.WriteAsync(inputEvent, cancellationToken);
                }
                i += consumed;
            }
            else
            {
                // Regular character
                var inputEvent = ParseKeyInput(message[i]);
                if (inputEvent != null)
                {
                    await _inputChannel.Writer.WriteAsync(inputEvent, cancellationToken);
                }
                i++;
            }
        }
    }

    private bool TryParseControlMessage(string message)
    {
        if (!message.StartsWith('{'))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(message);
            if (doc.RootElement.TryGetProperty("type", out var typeElement))
            {
                var type = typeElement.GetString();
                switch (type)
                {
                    case "resize":
                        var cols = doc.RootElement.GetProperty("cols").GetInt32();
                        var rows = doc.RootElement.GetProperty("rows").GetInt32();
                        Resize(cols, rows);
                        return true;
                }
            }
        }
        catch (JsonException)
        {
            // Not a valid JSON message
        }

        return false;
    }

    /// <summary>
    /// Resizes the terminal.
    /// </summary>
    public void Resize(int cols, int rows)
    {
        _width = cols;
        _height = rows;
        OnResize?.Invoke(cols, rows);
    }

    private static KeyInputEvent? ParseKeyInput(char c)
    {
        // Handle special characters
        return c switch
        {
            '\r' or '\n' => new KeyInputEvent(ConsoleKey.Enter, c, false, false, false),
            '\t' => new KeyInputEvent(ConsoleKey.Tab, c, false, false, false),
            '\x1b' => new KeyInputEvent(ConsoleKey.Escape, c, false, false, false),
            '\x7f' or '\b' => new KeyInputEvent(ConsoleKey.Backspace, c, false, false, false),
            ' ' => new KeyInputEvent(ConsoleKey.Spacebar, c, false, false, false),
            >= 'a' and <= 'z' => new KeyInputEvent((ConsoleKey)((int)ConsoleKey.A + (c - 'a')), c, false, false, false),
            >= 'A' and <= 'Z' => new KeyInputEvent((ConsoleKey)((int)ConsoleKey.A + (c - 'A')), c, true, false, false),
            >= '0' and <= '9' => new KeyInputEvent((ConsoleKey)((int)ConsoleKey.D0 + (c - '0')), c, false, false, false),
            // Control characters (Ctrl+A through Ctrl+Z)
            >= '\x01' and <= '\x1a' => new KeyInputEvent((ConsoleKey)((int)ConsoleKey.A + (c - '\x01')), c, false, false, true),
            _ when c >= ' ' && c <= '~' => new KeyInputEvent(ConsoleKey.NoName, c, false, false, false),
            _ => null
        };
    }

    /// <summary>
    /// Parses an ANSI CSI escape sequence (ESC [ ...).
    /// xterm sends arrow keys as:
    ///   - ESC [ A (Up), ESC [ B (Down), ESC [ C (Right), ESC [ D (Left)
    ///   - With modifiers: ESC [ 1 ; modifier code (e.g., ESC [ 1 ; 2 C for Shift+Right)
    /// Modifier codes: 2=Shift, 3=Alt, 4=Shift+Alt, 5=Ctrl, 6=Shift+Ctrl, 7=Alt+Ctrl, 8=Shift+Alt+Ctrl
    /// </summary>
    private static (KeyInputEvent? Event, int Consumed) ParseAnsiSequence(string message, int start)
    {
        // Minimum sequence is ESC [ X (3 chars)
        if (start + 2 >= message.Length)
            return (null, 1);

        var i = start + 2; // Skip ESC [
        
        // Collect parameters (numbers separated by semicolons)
        var param1 = 0;
        var param2 = 0;
        var hasParam2 = false;

        // Parse first parameter
        while (i < message.Length && char.IsDigit(message[i]))
        {
            param1 = param1 * 10 + (message[i] - '0');
            i++;
        }

        // Check for semicolon and second parameter
        if (i < message.Length && message[i] == ';')
        {
            i++; // Skip semicolon
            while (i < message.Length && char.IsDigit(message[i]))
            {
                param2 = param2 * 10 + (message[i] - '0');
                hasParam2 = true;
                i++;
            }
        }

        // The final character determines the key
        if (i >= message.Length)
            return (null, i - start);

        var finalChar = message[i];
        i++; // Include final char in consumed count

        // Parse modifiers from param2 (modifier code - 1 gives the modifier bits)
        var shift = false;
        var alt = false;
        var control = false;

        if (hasParam2 && param2 >= 2)
        {
            var modifierBits = param2 - 1;
            shift = (modifierBits & 1) != 0;
            alt = (modifierBits & 2) != 0;
            control = (modifierBits & 4) != 0;
        }

        var key = finalChar switch
        {
            'A' => ConsoleKey.UpArrow,
            'B' => ConsoleKey.DownArrow,
            'C' => ConsoleKey.RightArrow,
            'D' => ConsoleKey.LeftArrow,
            'H' => ConsoleKey.Home,
            'F' => ConsoleKey.End,
            'Z' => ConsoleKey.Tab, // Shift+Tab (backtab)
            '~' => ParseTildeSequence(param1),
            _ => ConsoleKey.NoName
        };

        if (key == ConsoleKey.NoName)
            return (null, i - start);

        // For 'Z' (Shift+Tab), always set shift=true
        if (finalChar == 'Z')
            shift = true;

        return (new KeyInputEvent(key, '\0', shift, alt, control), i - start);
    }

    /// <summary>
    /// Parses tilde sequences like ESC [ 1 ~ (Home), ESC [ 4 ~ (End), etc.
    /// </summary>
    private static ConsoleKey ParseTildeSequence(int param)
    {
        return param switch
        {
            1 => ConsoleKey.Home,
            2 => ConsoleKey.Insert,
            3 => ConsoleKey.Delete,
            4 => ConsoleKey.End,
            5 => ConsoleKey.PageUp,
            6 => ConsoleKey.PageDown,
            _ => ConsoleKey.NoName
        };
    }

    /// <summary>
    /// Parses an SS3 escape sequence (ESC O ...).
    /// Some terminals send arrow keys this way in application mode.
    /// </summary>
    private static (KeyInputEvent? Event, int Consumed) ParseSS3Sequence(string message, int start)
    {
        // Minimum sequence is ESC O X (3 chars)
        if (start + 2 >= message.Length)
            return (null, 1);

        var finalChar = message[start + 2];
        
        var key = finalChar switch
        {
            'A' => ConsoleKey.UpArrow,
            'B' => ConsoleKey.DownArrow,
            'C' => ConsoleKey.RightArrow,
            'D' => ConsoleKey.LeftArrow,
            'H' => ConsoleKey.Home,
            'F' => ConsoleKey.End,
            'P' => ConsoleKey.F1,
            'Q' => ConsoleKey.F2,
            'R' => ConsoleKey.F3,
            'S' => ConsoleKey.F4,
            _ => ConsoleKey.NoName
        };

        if (key == ConsoleKey.NoName)
            return (null, 3);

        return (new KeyInputEvent(key, '\0', false, false, false), 3);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _inputChannel.Writer.TryComplete();
    }
}
