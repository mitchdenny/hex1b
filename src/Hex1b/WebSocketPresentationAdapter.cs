using System.Buffers;
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
    private readonly record struct PreparedUtf8Output(
        byte[] CompleteBytes,
        byte[] PendingBytes);

    private readonly record struct ReceivedInput(
        byte[] Buffer,
        ValueWebSocketReceiveResult Result);

    private const string ResizeTraceFileEnvironmentVariable = "HEX1B_WEBSOCKET_RESIZE_TRACE_FILE";
    private const string OutputTraceFileEnvironmentVariable = "HEX1B_WEBSOCKET_OUTPUT_TRACE_FILE";

    private readonly WebSocket _webSocket;
    private readonly CancellationTokenSource _readCts = new();
    private readonly SemaphoreSlim _outputWriteLock = new(1, 1);
    private readonly object _lifecycleLock = new();
    private readonly byte[] _pendingOutputUtf8 = new byte[3];
    private int _pendingOutputUtf8Count;
    private int _activeWriters;
    private int _activeReaders;
    private TaskCompletionSource? _writersDrained;
    private TaskCompletionSource? _readersDrained;
    private Task? _disposeTask;
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
        // Native describes protocol fidelity: raw Sixel DCS bytes reach the browser
        // client unmodified, with no translation to another image protocol. This
        // adapter still leaves AnswersProtocolQueriesDirectly at its default false, so
        // Hex1bTerminal (not the browser) remains the query-answering owner for DA1
        // and window operations — a managed WebSocket connection cannot autonomously
        // answer those the way a real terminal emulator's PTY does.
        SixelSupport = Sixel.SixelPresentationSupport.Native,
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
        TryTraceResize(width, height, cellPixelWidth, cellPixelHeight, actualCellPixelWidth);
        
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

    private static void TryTraceResize(
        int width,
        int height,
        int? cellPixelWidth,
        int? cellPixelHeight,
        double? actualCellPixelWidth)
    {
        var tracePath = Environment.GetEnvironmentVariable(ResizeTraceFileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(tracePath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(tracePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                tracePath,
                $"[{DateTime.UtcNow:O}] Resize: {width}x{height}, cellPixel: {cellPixelWidth}x{cellPixelHeight}, actual: {actualCellPixelWidth}{Environment.NewLine}");
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            NotSupportedException or
            ArgumentException)
        {
            // Optional resize tracing must never break websocket terminal sessions.
        }
    }

    private static void TryTraceOutput(ReadOnlyMemory<byte> data)
    {
        var tracePath = Environment.GetEnvironmentVariable(OutputTraceFileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(tracePath) || data.IsEmpty)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(tracePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var bytes = data.ToArray();
            var limit = Math.Min(bytes.Length, 256);
            var hex = BitConverter.ToString(bytes, 0, limit);
            if (bytes.Length > limit)
            {
                hex += $"... ({bytes.Length} bytes total)";
            }

            var text = Encoding.UTF8.GetString(bytes)
                .Replace("\x1b", "<ESC>", StringComparison.Ordinal)
                .Replace("\r", "<CR>", StringComparison.Ordinal)
                .Replace("\n", "<LF>\n", StringComparison.Ordinal);

            File.AppendAllText(
                tracePath,
                $"[{DateTime.UtcNow:O}] OUTPUT {bytes.Length} byte(s){Environment.NewLine}" +
                $"HEX: {hex}{Environment.NewLine}" +
                $"TEXT: {text}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            NotSupportedException or
            ArgumentException)
        {
            // Optional output tracing must never break websocket terminal sessions.
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (data.IsEmpty || !TryBeginWriter())
            return;

        var lockTaken = false;
        try
        {
            if (_webSocket.State != WebSocketState.Open)
                return;

            TryTraceOutput(data);
            await _outputWriteLock.WaitAsync(ct).ConfigureAwait(false);
            lockTaken = true;
            var prepared = PrepareUtf8Output(
                data.Span,
                flush: false);
            if (prepared.CompleteBytes.Length > 0)
            {
                await _webSocket.SendAsync(
                    prepared.CompleteBytes,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    ct).ConfigureAwait(false);
            }
            else
            {
                ct.ThrowIfCancellationRequested();
            }

            CommitPendingUtf8(prepared.PendingBytes);
        }
        catch (WebSocketException)
        {
            // Connection closed
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        finally
        {
            if (lockTaken)
                _outputWriteLock.Release();
            CompleteWriter();
        }
    }

    private bool TryBeginWriter()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
                return false;

            if (_activeWriters++ == 0)
            {
                _writersDrained = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }
            return true;
        }
    }

    private void CompleteWriter()
    {
        TaskCompletionSource? writersDrained = null;
        lock (_lifecycleLock)
        {
            _activeWriters--;
            if (_activeWriters == 0)
            {
                writersDrained = _writersDrained;
                _writersDrained = null;
            }
        }
        writersDrained?.TrySetResult();
    }

    private PreparedUtf8Output PrepareUtf8Output(
        ReadOnlySpan<byte> data,
        bool flush)
    {
        var input = new byte[_pendingOutputUtf8Count + data.Length];
        _pendingOutputUtf8
            .AsSpan(0, _pendingOutputUtf8Count)
            .CopyTo(input);
        data.CopyTo(input.AsSpan(_pendingOutputUtf8Count));

        var complete = new List<byte>(input.Length);
        byte[] pending = [];
        for (var offset = 0; offset < input.Length;)
        {
            var status = Rune.DecodeFromUtf8(
                input.AsSpan(offset),
                out _,
                out var consumed);
            switch (status)
            {
                case OperationStatus.Done:
                    complete.AddRange(
                        input.AsSpan(offset, consumed));
                    offset += consumed;
                    break;
                case OperationStatus.NeedMoreData when !flush:
                    pending = input[offset..];
                    offset = input.Length;
                    break;
                case OperationStatus.NeedMoreData:
                case OperationStatus.InvalidData:
                    complete.AddRange([0xEF, 0xBF, 0xBD]);
                    offset += Math.Max(consumed, 1);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported UTF-8 decode status: {status}.");
            }
        }

        return new PreparedUtf8Output(
            complete.ToArray(),
            pending);
    }

    private void CommitPendingUtf8(ReadOnlySpan<byte> pending)
    {
        if (pending.Length > _pendingOutputUtf8.Length)
        {
            throw new InvalidOperationException(
                "A UTF-8 scalar cannot require more than three pending bytes.");
        }

        pending.CopyTo(_pendingOutputUtf8);
        _pendingOutputUtf8Count = pending.Length;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        while (true)
        {
            var received = await ReceiveInputAsync(ct).ConfigureAwait(false);
            if (received is not { } input)
                return ReadOnlyMemory<byte>.Empty;

            var buffer = input.Buffer;
            var result = input.Result;
            if (result.MessageType == WebSocketMessageType.Close)
                return ReadOnlyMemory<byte>.Empty;

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
                        continue;
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
                        continue;
                    }
                }

            }
            return buffer.AsMemory(0, result.Count);
        }
    }

    private async ValueTask<ReceivedInput?> ReceiveInputAsync(
        CancellationToken ct)
    {
        if (!TryBeginReader())
            return null;

        try
        {
            if (_webSocket.State != WebSocketState.Open)
                return null;

            var buffer = new byte[4096];
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ct,
                _readCts.Token);
            var result = await _webSocket.ReceiveAsync(
                buffer.AsMemory(),
                linkedCts.Token).ConfigureAwait(false);
            return new ReceivedInput(buffer, result);
        }
        catch (WebSocketException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            CompleteReader();
        }
    }

    private bool TryBeginReader()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
                return false;

            if (_activeReaders++ == 0)
            {
                _readersDrained = new(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }
            return true;
        }
    }

    private void CompleteReader()
    {
        TaskCompletionSource? readersDrained = null;
        lock (_lifecycleLock)
        {
            _activeReaders--;
            if (_activeReaders == 0)
            {
                readersDrained = _readersDrained;
                _readersDrained = null;
            }
        }
        readersDrained?.TrySetResult();
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
    public (int Row, int Column) GetCursorPosition() => (0, 0);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Task disposeTask;
        lock (_lifecycleLock)
        {
            if (_disposeTask is null)
            {
                _disposed = true;
                _disposeTask = Task.Run(DisposeCoreAsync);
                _ = NotifyDisconnectedAfterDisposalAsync(_disposeTask);
            }
            disposeTask = _disposeTask;
        }

        return new ValueTask(disposeTask);
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            Task writersDrained;
            lock (_lifecycleLock)
            {
                writersDrained = _activeWriters == 0
                    ? Task.CompletedTask
                    : _writersDrained!.Task;
            }
            await writersDrained.ConfigureAwait(false);
            await _outputWriteLock.WaitAsync(CancellationToken.None)
                .ConfigureAwait(false);
            try
            {
                await FlushPendingOutputUnsafeAsync().ConfigureAwait(false);
                if (_webSocket.State is
                    WebSocketState.Open or
                    WebSocketState.CloseReceived)
                {
                    await _webSocket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Session ended",
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (WebSocketException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _outputWriteLock.Release();
            }
        }
        finally
        {
            try
            {
                _readCts.Cancel();
                Task readersDrained;
                lock (_lifecycleLock)
                {
                    readersDrained = _activeReaders == 0
                        ? Task.CompletedTask
                        : _readersDrained!.Task;
                }
                await readersDrained.ConfigureAwait(false);
            }
            finally
            {
                _readCts.Dispose();
                _outputWriteLock.Dispose();
            }
        }
    }

    private async Task NotifyDisconnectedAfterDisposalAsync(
        Task disposal)
    {
        try
        {
            await disposal.ConfigureAwait(false);
        }
        catch
        {
            // The shared disposal task carries the cleanup failure to callers.
        }

        try
        {
            Disconnected?.Invoke();
        }
        catch
        {
            // Observers run after cleanup and cannot alter disposal completion.
        }
    }

    private async ValueTask FlushPendingOutputUnsafeAsync()
    {
        try
        {
            var prepared = PrepareUtf8Output([], flush: true);
            if (prepared.CompleteBytes.Length > 0 &&
                _webSocket.State == WebSocketState.Open)
            {
                await _webSocket.SendAsync(
                    prepared.CompleteBytes,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            _pendingOutputUtf8Count = 0;
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
