#pragma warning disable HEX1B_SIXEL // Sixel API is experimental - internal usage is allowed

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b;

/// <summary>
/// WebSocket-based terminal implementation for browser-based TUI.
/// Bridges WebSocket connections to the Hex1b terminal abstraction.
/// </summary>
public sealed class WebSocketHex1bTerminal : IHex1bTerminal, IDisposable
{
    private readonly WebSocket _webSocket;
    private readonly Channel<Hex1bEvent> _inputChannel;
    private readonly CancellationTokenSource _disposeCts;
    private int _width;
    private int _height;
    private bool _disposed;
    private bool _mouseEnabled;

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
    /// <param name="enableMouse">Whether to enable mouse tracking.</param>
    public WebSocketHex1bTerminal(WebSocket webSocket, int width = 80, int height = 24, bool enableMouse = false)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _width = width;
        _height = height;
        _mouseEnabled = enableMouse;
        _inputChannel = Channel.CreateUnbounded<Hex1bEvent>();
        _disposeCts = new CancellationTokenSource();
        
        // Reset Sixel detection for new terminal session
        // This ensures each WebSocket session re-probes for Sixel support
        SixelNode.ResetGlobalSixelDetection();
    }

    /// <inheritdoc />
    public int Width => _width;

    /// <inheritdoc />
    public int Height => _height;

    /// <inheritdoc />
    public ChannelReader<Hex1bEvent> InputEvents => _inputChannel.Reader;

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
        var escapes = "\x1b[?1049h\x1b[?25l";
        if (_mouseEnabled)
        {
            escapes += MouseParser.EnableMouseTracking;
        }
        escapes += "\x1b[2J\x1b[H";
        Write(escapes);
    }

    /// <inheritdoc />
    public void ExitAlternateScreen()
    {
        var escapes = "";
        if (_mouseEnabled)
        {
            escapes += MouseParser.DisableMouseTracking;
        }
        escapes += "\x1b[?25h\x1b[?1049l";
        Write(escapes);
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

        // Check for DA1 response (ESC [ ? ... c) for Sixel support detection
        if (TryParseDA1Response(message, out var capabilityEvent))
        {
            // Send event to trigger re-render with updated capability info
            if (capabilityEvent != null)
            {
                await _inputChannel.Writer.WriteAsync(capabilityEvent, cancellationToken);
            }
            return;
        }

        // Process the message, looking for ANSI escape sequences
        var i = 0;
        while (i < message.Length)
        {
            // Check for ANSI escape sequence
            if (message[i] == '\x1b' && i + 1 < message.Length && message[i + 1] == '[')
            {
                // Check for SGR mouse sequence: ESC [ < ...
                if (i + 2 < message.Length && message[i + 2] == '<')
                {
                    // SGR mouse sequence - find the terminator (M or m)
                    var (mouseEvent, mouseConsumed) = ParseSgrMouseSequence(message, i);
                    if (mouseEvent != null)
                    {
                        await _inputChannel.Writer.WriteAsync(mouseEvent, cancellationToken);
                        i += mouseConsumed;
                        continue;
                    }
                }
                
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
            else if (char.IsHighSurrogate(message[i]) && i + 1 < message.Length && char.IsLowSurrogate(message[i + 1]))
            {
                // Surrogate pair (emoji or other supplementary Unicode character)
                // Combine into a single string and send as one event
                var text = message.Substring(i, 2);
                var inputEvent = Hex1bKeyEvent.FromText(text);
                await _inputChannel.Writer.WriteAsync(inputEvent, cancellationToken);
                i += 2;
            }
            else
            {
                // Regular character (BMP)
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
    /// Checks if the message is a DA1 (Primary Device Attributes) response.
    /// DA1 response format: ESC [ ? {params} c
    /// If it contains ";4" it indicates Sixel graphics support.
    /// </summary>
    private static bool TryParseDA1Response(string message, out Hex1bTerminalEvent? terminalEvent)
    {
        terminalEvent = null;
        
        // DA1 response starts with ESC [ ? and ends with c
        // Example: \x1b[?62;4;6;22c
        if (message.StartsWith("\x1b[?") && message.EndsWith("c"))
        {
            // This is a DA1 response - pass it to SixelNode for processing
            Console.Error.WriteLine($"[Sixel] Received DA1 response: {message.Replace("\x1b", "ESC")}");
            Nodes.SixelNode.HandleDA1Response(message);
            terminalEvent = new Hex1bTerminalEvent(message);
            return true;
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
        
        // Push a resize event to trigger re-render
        _inputChannel.Writer.TryWrite(new Hex1bResizeEvent(cols, rows));
    }

    private static Hex1bKeyEvent? ParseKeyInput(char c)
    {
        // Handle special characters
        return c switch
        {
            '\r' or '\n' => new Hex1bKeyEvent(Hex1bKey.Enter, c, Hex1bModifiers.None),
            '\t' => new Hex1bKeyEvent(Hex1bKey.Tab, c, Hex1bModifiers.None),
            '\x1b' => new Hex1bKeyEvent(Hex1bKey.Escape, c, Hex1bModifiers.None),
            '\x7f' or '\b' => new Hex1bKeyEvent(Hex1bKey.Backspace, c, Hex1bModifiers.None),
            ' ' => new Hex1bKeyEvent(Hex1bKey.Spacebar, c, Hex1bModifiers.None),
            >= 'a' and <= 'z' => new Hex1bKeyEvent(KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'a'))), c, Hex1bModifiers.None),
            >= 'A' and <= 'Z' => new Hex1bKeyEvent(KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'A'))), c, Hex1bModifiers.Shift),
            >= '0' and <= '9' => new Hex1bKeyEvent(KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.D0 + (c - '0'))), c, Hex1bModifiers.None),
            // Control characters (Ctrl+A through Ctrl+Z)
            >= '\x01' and <= '\x1a' => new Hex1bKeyEvent(KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - '\x01'))), c, Hex1bModifiers.Control),
            // Any non-control character (includes Unicode, emojis as surrogate pairs, etc.)
            _ when !char.IsControl(c) => new Hex1bKeyEvent(Hex1bKey.None, c, Hex1bModifiers.None),
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
    private static (Hex1bKeyEvent? Event, int Consumed) ParseAnsiSequence(string message, int start)
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
            'Z' => Hex1bKey.Tab, // Shift+Tab (backtab)
            '~' => ParseTildeSequence(param1),
            _ => Hex1bKey.None
        };

        if (key == Hex1bKey.None)
            return (null, i - start);

        // For 'Z' (Shift+Tab), always set shift
        if (finalChar == 'Z')
            modifiers |= Hex1bModifiers.Shift;

        return (new Hex1bKeyEvent(key, '\0', modifiers), i - start);
    }

    /// <summary>
    /// Parses tilde sequences like ESC [ 1 ~ (Home), ESC [ 4 ~ (End), etc.
    /// </summary>
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

    /// <summary>
    /// Parses an SS3 escape sequence (ESC O ...).
    /// Some terminals send arrow keys this way in application mode.
    /// </summary>
    private static (Hex1bKeyEvent? Event, int Consumed) ParseSS3Sequence(string message, int start)
    {
        // Minimum sequence is ESC O X (3 chars)
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

    /// <summary>
    /// Parses an SGR mouse escape sequence (ESC [ &lt; Cb ; Cx ; Cy M/m).
    /// </summary>
    private static (Hex1bMouseEvent? Event, int Consumed) ParseSgrMouseSequence(string message, int start)
    {
        // Minimum sequence is ESC [ < N ; N ; N M (at least 9 chars for single digits)
        // e.g., \x1b[<0;1;1M = 9 characters
        if (start + 8 >= message.Length)
            return (null, 3);

        var i = start + 3; // Skip ESC [ <
        
        // Find the terminator (M or m)
        var terminatorIdx = -1;
        for (var j = i; j < message.Length; j++)
        {
            if (message[j] == 'M' || message[j] == 'm')
            {
                terminatorIdx = j;
                break;
            }
            // If we hit something that's not a digit, semicolon, or terminator, abort
            if (!char.IsDigit(message[j]) && message[j] != ';')
            {
                return (null, 3);
            }
        }
        
        if (terminatorIdx < 0)
            return (null, 3);
        
        // Extract the parameter part and parse with MouseParser
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
        if (_disposed)
            return;

        _disposed = true;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _inputChannel.Writer.TryComplete();
    }
}
