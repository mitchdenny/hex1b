using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Hex1b.Input;

namespace Hex1b;

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
    private int _width;
    private int _height;
    private int _cellPixelWidth = 10;
    private int _cellPixelHeight = 20;
    private double _actualCellPixelWidth = 10.0;
    private readonly bool _enableMouse;

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
        SupportsBracketedPaste = true,
        SupportsSixel = true,
        CellPixelWidth = _cellPixelWidth,
        CellPixelHeight = _cellPixelHeight,
        ActualCellPixelWidth = _actualCellPixelWidth
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
    /// <param name="cellPixelWidth">Optional cell width in pixels (integer).</param>
    /// <param name="cellPixelHeight">Optional cell height in pixels.</param>
    /// <param name="actualCellPixelWidth">Optional actual (floating-point) cell width.</param>
    public void Resize(int width, int height, int? cellPixelWidth = null, int? cellPixelHeight = null, double? actualCellPixelWidth = null)
    {
        System.IO.File.AppendAllText("/tmp/websocket-resize.log",
            $"[{DateTime.Now:HH:mm:ss.fff}] Resize: {width}x{height}, cellPixel: {cellPixelWidth}x{cellPixelHeight}, actual: {actualCellPixelWidth}\n");
        
        var sizeChanged = _width != width || _height != height;
        
        _width = width;
        _height = height;
        
        if (cellPixelWidth.HasValue && cellPixelWidth.Value > 0)
            _cellPixelWidth = cellPixelWidth.Value;
        if (cellPixelHeight.HasValue && cellPixelHeight.Value > 0)
            _cellPixelHeight = cellPixelHeight.Value;
        if (actualCellPixelWidth.HasValue && actualCellPixelWidth.Value > 0)
            _actualCellPixelWidth = actualCellPixelWidth.Value;
        else if (cellPixelWidth.HasValue && cellPixelWidth.Value > 0)
            _actualCellPixelWidth = cellPixelWidth.Value;
        
        if (sizeChanged)
            Resized?.Invoke(width, height);
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
                
                // Handle JSON format: {"type":"resize","cols":80,"rows":24,"cellWidth":8.4,"cellHeight":16}
                if (text.StartsWith("{") && text.Contains("resize"))
                {
                    if (TryParseJsonResize(text, out var newWidth, out var newHeight, out var cellWidth, out var cellHeight, out var actualCellWidth))
                    {
                        Resize(newWidth, newHeight, cellWidth, cellHeight, actualCellWidth);
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
    public ValueTask EnterRawModeAsync(CancellationToken ct = default)
    {
        // WebSocket is already "raw" - browser handles the terminal emulation
        // No escape sequences needed - screen mode is controlled by the workload
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ExitRawModeAsync(CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnected?.Invoke();

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
    private static bool TryParseJsonResize(string json, out int width, out int height, out int? cellWidth, out int? cellHeight, out double? actualCellWidth)
    {
        width = 0;
        height = 0;
        cellWidth = null;
        cellHeight = null;
        actualCellWidth = null;

        try
        {
            // Simple parsing without full JSON deserializer
            // Expected format: {"type":"resize","cols":80,"rows":24,"cellWidth":8.4,"cellHeight":16}
            var colsMatch = System.Text.RegularExpressions.Regex.Match(json, @"""cols""\s*:\s*(\d+)");
            var rowsMatch = System.Text.RegularExpressions.Regex.Match(json, @"""rows""\s*:\s*(\d+)");

            if (colsMatch.Success && rowsMatch.Success)
            {
                width = int.Parse(colsMatch.Groups[1].Value);
                height = int.Parse(rowsMatch.Groups[1].Value);
                
                // Parse optional cell dimensions (may be floating point)
                var cellWidthMatch = System.Text.RegularExpressions.Regex.Match(json, @"""cellWidth""\s*:\s*([\d.]+)");
                var cellHeightMatch = System.Text.RegularExpressions.Regex.Match(json, @"""cellHeight""\s*:\s*([\d.]+)");
                
                if (cellWidthMatch.Success && double.TryParse(cellWidthMatch.Groups[1].Value, 
                    System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    out var cw))
                {
                    actualCellWidth = cw;
                    cellWidth = (int)Math.Round(cw);
                }
                if (cellHeightMatch.Success && double.TryParse(cellHeightMatch.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var ch))
                    cellHeight = (int)Math.Round(ch);
                
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
