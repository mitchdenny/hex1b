using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Hex1b.Input;

namespace Hex1b.Terminal;

/// <summary>
/// WebSocket presentation adapter for browser-based terminal connections.
/// </summary>
/// <remarks>
/// This adapter implements <see cref="IHex1bTerminalPresentationAdapter"/> for
/// WebSocket connections, allowing Hex1b applications to run in web browsers
/// through xterm.js or similar terminal emulators.
/// </remarks>
public sealed class WebSocketPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private readonly WebSocket _webSocket;
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _disposed;
    private bool _inTuiMode;
    private int _width;
    private int _height;
    private readonly bool _enableMouse;

    private const string EnterAlternateBuffer = "\x1b[?1049h";
    private const string ExitAlternateBuffer = "\x1b[?1049l";
    private const string ClearScreen = "\x1b[2J";
    private const string MoveCursorHome = "\x1b[H";
    private const string HideCursor = "\x1b[?25l";
    private const string ShowCursor = "\x1b[?25h";

    /// <summary>
    /// Creates a new WebSocket presentation adapter.
    /// </summary>
    /// <param name="webSocket">The WebSocket connection to use.</param>
    /// <param name="width">Initial terminal width in columns.</param>
    /// <param name="height">Initial terminal height in rows.</param>
    /// <param name="enableMouse">Whether to enable mouse tracking.</param>
    public WebSocketPresentationAdapter(WebSocket webSocket, int width, int height, bool enableMouse = false)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _width = width;
        _height = height;
        _enableMouse = enableMouse;
    }

    /// <inheritdoc />
    public int Width => _width;

    /// <inheritdoc />
    public int Height => _height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => new()
    {
        SupportsMouse = _enableMouse,
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = true
    };

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <summary>
    /// Updates the terminal dimensions, typically called when receiving a resize message from the client.
    /// </summary>
    /// <param name="width">New terminal width in columns.</param>
    /// <param name="height">New terminal height in rows.</param>
    public void Resize(int width, int height)
    {
        if (_width != width || _height != height)
        {
            _width = width;
            _height = height;
            Resized?.Invoke(width, height);
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open)
            return;

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
            // Use Text message type since terminal output is UTF-8 and JavaScript expects strings
            await _webSocket.SendAsync(data, WebSocketMessageType.Text, endOfMessage: true, linkedCts.Token);
        }
        catch (WebSocketException)
        {
            // Connection closed
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open)
            return ReadOnlyMemory<byte>.Empty;

        var buffer = new byte[4096];
        
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
            var result = await _webSocket.ReceiveAsync(buffer.AsMemory(), linkedCts.Token);
            
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return ReadOnlyMemory<byte>.Empty;
            }
            
            // Check for resize message (custom protocol)
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                // Handle JSON format: {"type":"resize","cols":80,"rows":24}
                if (text.StartsWith("{") && text.Contains("resize"))
                {
                    if (TryParseJsonResize(text, out var newWidth, out var newHeight))
                    {
                        Resize(newWidth, newHeight);
                        // Return empty for resize messages - not actual input
                        return await ReadInputAsync(ct);
                    }
                }
                
                // Handle legacy format: resize:80,24
                if (text.StartsWith("resize:"))
                {
                    var parts = text[7..].Split(',');
                    if (parts.Length == 2 && 
                        int.TryParse(parts[0], out var width) && 
                        int.TryParse(parts[1], out var height))
                    {
                        Resize(width, height);
                        // Return empty for resize messages - not actual input
                        return await ReadInputAsync(ct);
                    }
                }
            }
            
            return buffer.AsMemory(0, result.Count);
        }
        catch (WebSocketException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        // WebSocket sends are typically already unbuffered
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask EnterTuiModeAsync(CancellationToken ct = default)
    {
        if (_inTuiMode) return;
        _inTuiMode = true;

        var escapes = new StringBuilder();
        escapes.Append(EnterAlternateBuffer);
        escapes.Append(HideCursor);
        if (_enableMouse)
        {
            escapes.Append(MouseParser.EnableMouseTracking);
        }
        escapes.Append(ClearScreen);
        escapes.Append(MoveCursorHome);

        await WriteOutputAsync(Encoding.UTF8.GetBytes(escapes.ToString()), ct);
    }

    /// <inheritdoc />
    public async ValueTask ExitTuiModeAsync(CancellationToken ct = default)
    {
        if (!_inTuiMode) return;
        _inTuiMode = false;

        var escapes = new StringBuilder();
        if (_enableMouse)
        {
            escapes.Append(MouseParser.DisableMouseTracking);
        }
        escapes.Append(ShowCursor);
        escapes.Append(ExitAlternateBuffer);

        await WriteOutputAsync(Encoding.UTF8.GetBytes(escapes.ToString()), ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnected?.Invoke();

        if (_inTuiMode)
        {
            await ExitTuiModeAsync();
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();

        if (_webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
            }
            catch
            {
                // Ignore close errors
            }
        }
    }

    /// <summary>
    /// Attempts to parse a JSON resize message.
    /// </summary>
    private static bool TryParseJsonResize(string json, out int width, out int height)
    {
        width = 0;
        height = 0;

        try
        {
            // Simple parsing without full JSON deserializer
            // Expected format: {"type":"resize","cols":80,"rows":24}
            var colsMatch = System.Text.RegularExpressions.Regex.Match(json, @"""cols""\s*:\s*(\d+)");
            var rowsMatch = System.Text.RegularExpressions.Regex.Match(json, @"""rows""\s*:\s*(\d+)");

            if (colsMatch.Success && rowsMatch.Success)
            {
                width = int.Parse(colsMatch.Groups[1].Value);
                height = int.Parse(rowsMatch.Groups[1].Value);
                return true;
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return false;
    }
}
