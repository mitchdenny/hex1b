#pragma warning disable HEX1B_SIXEL // Sixel API is experimental - internal usage is allowed

using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Theming;

namespace Hex1b.Terminal;

/// <summary>
/// A virtual terminal that bridges workload and presentation layers, with optional
/// screen buffer capture for testing and debugging.
/// </summary>
/// <remarks>
/// <para>
/// Hex1bTerminal serves as a mediator between:
/// <list type="bullet">
///   <item><see cref="IHex1bTerminalPresentationAdapter"/> - the raw I/O layer (console, WebSocket, etc.)</item>
///   <item><see cref="IHex1bTerminalWorkloadAdapter"/> - the workload (app, process, etc.)</item>
/// </list>
/// </para>
/// <para>
/// The workload adapter is provided by the caller. For Hex1bApp, use <see cref="Hex1bAppWorkloadAdapter"/>.
/// This design keeps the terminal decoupled from specific workload types.
/// </para>
/// <para>
/// When no presentation adapter is provided (null), the terminal operates in "headless" mode,
/// capturing all output to the screen buffer. This is ideal for testing.
/// </para>
/// </remarks>
/// <example>
/// <para>Production usage:</para>
/// <code>
/// var presentation = new ConsolePresentationAdapter();
/// var workload = new Hex1bAppWorkloadAdapter(presentation.Width, presentation.Height);
/// var terminal = new Hex1bTerminal(presentation, workload);
/// var app = new Hex1bApp(workload, ctx => ctx.Text("Hello"));
/// await app.RunAsync();
/// </code>
/// <para>Testing usage:</para>
/// <code>
/// var workload = new Hex1bAppWorkloadAdapter(80, 24);
/// var terminal = new Hex1bTerminal(workload);  // headless
/// var app = new Hex1bApp(workload, ctx => ctx.Text("Hello"));
/// // ... run and test ...
/// Assert.True(terminal.ContainsText("Hello"));
/// </code>
/// </example>
public sealed class Hex1bTerminal : IDisposable
{
    private readonly IHex1bTerminalPresentationAdapter? _presentation;
    private readonly IHex1bTerminalWorkloadAdapter _workload;
    private readonly IReadOnlyList<IHex1bTerminalWorkloadFilter> _workloadFilters;
    private readonly IReadOnlyList<IHex1bTerminalPresentationFilter> _presentationFilters;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly DateTimeOffset _sessionStart;
    private readonly TrackedObjectStore _trackedObjects = new();
    private readonly TimeProvider _timeProvider;
    private TerminalCell[,] _screenBuffer;
    private int _cursorX;
    private int _cursorY;
    private Hex1bColor? _currentForeground;
    private Hex1bColor? _currentBackground;
    private CellAttributes _currentAttributes;
    private bool _disposed;
    private bool _inAlternateScreen;
    private Task? _inputProcessingTask;
    private Task? _outputProcessingTask;
    private long _writeSequence; // Monotonically increasing write order counter



    /// <summary>
    /// Creates a new headless terminal for testing with the specified workload adapter and dimensions.
    /// </summary>
    /// <param name="workload">The workload adapter (e.g., Hex1bAppWorkloadAdapter).</param>
    /// <param name="width">Terminal width in characters.</param>
    /// <param name="height">Terminal height in lines.</param>
    public Hex1bTerminal(IHex1bTerminalWorkloadAdapter workload, int width, int height)
        : this(presentation: null, workload: workload, width: width, height: height, timeProvider: TimeProvider.System)
    {
    }

    /// <summary>
    /// Creates a new terminal with the specified options.
    /// </summary>
    /// <param name="options">Terminal configuration options.</param>
    public Hex1bTerminal(Hex1bTerminalOptions options)
        : this(
            presentation: options.PresentationAdapter,
            workload: options.WorkloadAdapter ?? throw new ArgumentNullException(nameof(options), "WorkloadAdapter is required"),
            width: options.Width,
            height: options.Height,
            workloadFilters: options.WorkloadFilters,
            presentationFilters: options.PresentationFilters,
            timeProvider: options.TimeProvider)
    {
    }

    /// <summary>
    /// Creates a new terminal with the specified presentation and workload adapters.
    /// </summary>
    /// <param name="presentation">The presentation adapter for actual I/O. Pass null for headless/test mode.</param>
    /// <param name="workload">The workload adapter (e.g., Hex1bAppWorkloadAdapter).</param>
    /// <param name="width">Terminal width (used when presentation is null). Ignored if presentation is provided.</param>
    /// <param name="height">Terminal height (used when presentation is null). Ignored if presentation is provided.</param>
    /// <param name="workloadFilters">Filters applied on the workload side.</param>
    /// <param name="presentationFilters">Filters applied on the presentation side.</param>
    /// <param name="timeProvider">The time provider for all time-related operations. Defaults to system time.</param>
    public Hex1bTerminal(
        IHex1bTerminalPresentationAdapter? presentation,
        IHex1bTerminalWorkloadAdapter workload,
        int width = 80,
        int height = 24,
        IEnumerable<IHex1bTerminalWorkloadFilter>? workloadFilters = null,
        IEnumerable<IHex1bTerminalPresentationFilter>? presentationFilters = null,
        TimeProvider? timeProvider = null)
    {
        _presentation = presentation;
        _workload = workload ?? throw new ArgumentNullException(nameof(workload));
        _workloadFilters = workloadFilters?.ToList() ?? [];
        _presentationFilters = presentationFilters?.ToList() ?? [];
        _timeProvider = timeProvider ?? TimeProvider.System;
        _sessionStart = _timeProvider.GetUtcNow();
        
        // Get dimensions from presentation if available, otherwise use provided dimensions
        _width = presentation?.Width ?? width;
        _height = presentation?.Height ?? height;
        
        // Notify workload of initial dimensions (ResizeAsync handles not firing event on init)
        _ = _workload.ResizeAsync(_width, _height);
        
        _screenBuffer = new TerminalCell[_height, _width];
        
        ClearBuffer();

        // Subscribe to presentation events if present
        if (_presentation != null)
        {
            _presentation.Resized += OnPresentationResized;
        }

        // Notify filters of session start
        // Note: We fire-and-forget here since the constructor can't be async
        // Filters should handle this gracefully
        _ = NotifyWorkloadFiltersSessionStartAsync();
        _ = NotifyPresentationFiltersSessionStartAsync();

        // Auto-start I/O pumps when presentation is provided (production mode)
        if (_presentation != null)
        {
            Start();
        }
    }

    private int _width;
    private int _height;

    private void OnPresentationResized(int width, int height)
    {
        // IMPORTANT: Call Resize() first before updating _width/_height
        // because Resize() needs the OLD dimensions to know how much to copy
        Resize(width, height);
        
        // Notify filters of resize
        _ = NotifyPresentationFiltersResizeAsync(width, height);
        _ = NotifyWorkloadFiltersResizeAsync(width, height);
        
        // Notify workload of resize
        _ = _workload.ResizeAsync(width, height);
    }

    // === Configuration ===

    /// <summary>
    /// Terminal width.
    /// </summary>
    internal int Width => _width;

    /// <summary>
    /// Terminal height.
    /// </summary>
    internal int Height => _height;
    
    /// <summary>
    /// Terminal capabilities from the presentation adapter, workload adapter, or defaults.
    /// Priority: presentation adapter > workload adapter (if IHex1bAppTerminalWorkloadAdapter) > defaults.
    /// </summary>
    internal TerminalCapabilities Capabilities => 
        _presentation?.Capabilities 
        ?? (_workload as IHex1bAppTerminalWorkloadAdapter)?.Capabilities 
        ?? TerminalCapabilities.Modern;

    // === Terminal Control ===

    /// <summary>
    /// Starts the terminal's I/O pump loops.
    /// Called automatically when a presentation adapter is provided.
    /// </summary>
    private void Start()
    {
        // Enter TUI mode on presentation if present (enables raw mode for input capture)
        if (_presentation != null)
        {
            // Fire and forget - EnterTuiModeAsync is typically synchronous for console
            _ = _presentation.EnterTuiModeAsync();
        }

        // Start pumping presentation input → workload (if presentation exists)
        if (_presentation != null && _inputProcessingTask == null)
        {
            _inputProcessingTask = Task.Run(() => PumpPresentationInputAsync(_disposeCts.Token));
        }

        // Start pumping workload output → presentation
        if (_outputProcessingTask == null)
        {
            _outputProcessingTask = Task.Run(() => PumpWorkloadOutputAsync(_disposeCts.Token));
        }
    }

    /// <summary>
    /// Synchronously drains any pending output from the workload and processes it
    /// into the screen buffer.
    /// </summary>
    /// <remarks>
    /// This is called automatically by screen buffer read operations (GetScreenText, 
    /// ContainsText, etc.) so callers don't need to call it directly.
    /// </remarks>
    internal void FlushOutput()
    {
        if (_workload is not Hex1bAppWorkloadAdapter appWorkload)
            return;

        // Drain all available output synchronously using non-blocking reads
        while (appWorkload.TryReadOutput(out var data))
        {
            if (data.IsEmpty)
            {
                break;
            }

            // Notify workload filters of output FROM workload (fire-and-forget in sync context)
            _ = NotifyWorkloadFiltersOutputAsync(data);

            var text = Encoding.UTF8.GetString(data.Span);
            ProcessOutput(text);
        }

        // Channel drained - notify frame complete (fire-and-forget in sync context)
        _ = NotifyWorkloadFiltersFrameCompleteAsync();
    }

    // === I/O Pump Tasks ===

    private async Task PumpPresentationInputAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _presentation != null)
            {
                var data = await _presentation.ReadInputAsync(ct);
                if (data.IsEmpty)
                {
                    break;
                }

                // Notify presentation filters of input FROM presentation
                await NotifyPresentationFiltersInputAsync(data);

                // Notify workload filters of input going TO workload
                await NotifyWorkloadFiltersInputAsync(data);

                // For Hex1bAppWorkloadAdapter, we parse input and send events directly
                if (_workload is Hex1bAppWorkloadAdapter appWorkload)
                {
                    await ParseAndDispatchInputAsync(data, appWorkload, ct);
                }
                else
                {
                    // For other workloads, forward raw bytes
                    await _workload.WriteInputAsync(data, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async Task PumpWorkloadOutputAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var data = await _workload.ReadOutputAsync(ct);
                if (data.IsEmpty)
                {
                    // Channel empty - this is a frame boundary
                    await NotifyWorkloadFiltersFrameCompleteAsync();
                    
                    // Small delay to prevent busy-waiting in headless mode
                    await Task.Delay(10, ct);
                    continue;
                }

                // Notify workload filters of output FROM workload
                await NotifyWorkloadFiltersOutputAsync(data);

                var text = Encoding.UTF8.GetString(data.Span);
                ProcessOutput(text);

                // Forward to presentation if present
                if (_presentation != null)
                {
                    // Notify presentation filters of output TO presentation
                    await NotifyPresentationFiltersOutputAsync(data);
                    
                    await _presentation.WriteOutputAsync(data);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async Task ParseAndDispatchInputAsync(ReadOnlyMemory<byte> data, Hex1bAppWorkloadAdapter workload, CancellationToken ct)
    {
        var message = Encoding.UTF8.GetString(data.Span);
        var i = 0;

        while (i < message.Length && !ct.IsCancellationRequested)
        {
            // Check for DA1 response
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
                        var (mouseEvent, mouseConsumed) = ParseSgrMouseSequence(message, i);
                        if (mouseEvent != null)
                        {
                            await workload.WriteInputEventAsync(mouseEvent, ct);
                            i += mouseConsumed;
                            continue;
                        }
                    }

                    var (csiEvent, csiConsumed) = ParseCsiSequence(message, i);
                    if (csiEvent != null)
                    {
                        await workload.WriteInputEventAsync(csiEvent, ct);
                    }
                    i += csiConsumed;
                }
                else if (message[i + 1] == 'O')
                {
                    var (ss3Event, ss3Consumed) = ParseSS3Sequence(message, i);
                    if (ss3Event != null)
                    {
                        await workload.WriteInputEventAsync(ss3Event, ct);
                    }
                    i += ss3Consumed;
                }
                else
                {
                    await workload.WriteInputEventAsync(
                        new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None), ct);
                    i++;
                }
            }
            else if (char.IsHighSurrogate(message[i]) && i + 1 < message.Length && char.IsLowSurrogate(message[i + 1]))
            {
                var text = message.Substring(i, 2);
                await workload.WriteInputEventAsync(Hex1bKeyEvent.FromText(text), ct);
                i += 2;
            }
            else
            {
                var keyEvent = ParseKeyInput(message[i]);
                if (keyEvent != null)
                {
                    await workload.WriteInputEventAsync(keyEvent, ct);
                }
                i++;
            }
        }
    }

    // === Screen Buffer APIs (Testing/Debugging) ===

    /// <summary>
    /// Gets the current cursor X position (0-based).
    /// Automatically flushes pending output before returning.
    /// </summary>
    internal int CursorX
    {
        get
        {
            FlushOutput();
            return _cursorX;
        }
    }

    /// <summary>
    /// Gets the current cursor Y position (0-based).
    /// Automatically flushes pending output before returning.
    /// </summary>
    internal int CursorY
    {
        get
        {
            FlushOutput();
            return _cursorY;
        }
    }

    /// <summary>
    /// Gets whether the terminal is currently in alternate screen mode.
    /// Automatically flushes pending output before returning.
    /// </summary>
    internal bool InAlternateScreen
    {
        get
        {
            FlushOutput();
            return _inAlternateScreen;
        }
    }

    /// <summary>
    /// Enters alternate screen mode (for testing purposes).
    /// In headless mode, this just sets the flag and clears the buffer.
    /// </summary>
    internal void EnterAlternateScreen()
    {
        ProcessOutput("\x1b[?1049h");
    }

    /// <summary>
    /// Exits alternate screen mode (for testing purposes).
    /// In headless mode, this just clears the flag.
    /// </summary>
    internal void ExitAlternateScreen()
    {
        ProcessOutput("\x1b[?1049l");
    }

    /// <summary>
    /// Gets a copy of the current screen buffer.
    /// Automatically flushes pending output before returning.
    /// </summary>
    /// <param name="addTrackedObjectRefs">
    /// If true, adds references to tracked objects in the copied cells.
    /// The caller is responsible for releasing these refs when done.
    /// </param>
    internal TerminalCell[,] GetScreenBuffer(bool addTrackedObjectRefs = false)
    {
        FlushOutput();
        var copy = new TerminalCell[_height, _width];
        Array.Copy(_screenBuffer, copy, _screenBuffer.Length);
        
        if (addTrackedObjectRefs)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    copy[y, x].TrackedSixel?.AddRef();
                }
            }
        }
        
        return copy;
    }

    /// <summary>
    /// Gets the text content of the screen buffer as a string, with lines separated by newlines.
    /// Automatically flushes pending output before returning.
    /// </summary>
    internal string GetScreenText()
    {
        FlushOutput();
        return GetScreenTextInternal();
    }

    // Internal version that doesn't flush (for use after already flushing)
    private string GetScreenTextInternal()
    {
        var sb = new StringBuilder();
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                var ch = _screenBuffer[y, x].Character;
                // Skip empty continuation cells (used for wide characters)
                if (ch.Length > 0)
                {
                    sb.Append(ch);
                }
            }
            if (y < _height - 1)
            {
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets the text content of a specific line (0-based).
    /// Automatically flushes pending output before returning.
    /// </summary>
    internal string GetLine(int lineIndex)
    {
        FlushOutput();
        return GetLineInternal(lineIndex);
    }

    // Internal version that doesn't flush (for use after already flushing)
    private string GetLineInternal(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= _height)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        var sb = new StringBuilder(_width);
        for (int x = 0; x < _width; x++)
        {
            var ch = _screenBuffer[lineIndex, x].Character;
            // Skip empty continuation cells (used for wide characters)
            if (ch.Length > 0)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets the text content of a specific line, trimmed of trailing whitespace.
    /// Automatically flushes pending output before returning.
    /// </summary>
    internal string GetLineTrimmed(int lineIndex) => GetLine(lineIndex).TrimEnd();

    /// <summary>
    /// Gets all non-empty lines from the screen buffer.
    /// Automatically flushes pending output before returning.
    /// </summary>
    internal IEnumerable<string> GetNonEmptyLines()
    {
        FlushOutput();
        for (int y = 0; y < _height; y++)
        {
            var line = GetLineInternal(y).TrimEnd();
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    /// <summary>
    /// Checks if the screen contains the specified text anywhere.
    /// Automatically flushes pending output before checking.
    /// </summary>
    internal bool ContainsText(string text)
    {
        FlushOutput();
        var screenText = GetScreenTextInternal();
        return screenText.Contains(text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds all occurrences of the specified text on the screen.
    /// Returns a list of (line, column) positions.
    /// Automatically flushes pending output before searching.
    /// </summary>
    internal List<(int Line, int Column)> FindText(string text)
    {
        FlushOutput();
        var results = new List<(int, int)>();
        for (int y = 0; y < _height; y++)
        {
            var line = GetLineInternal(y);
            var index = 0;
            while ((index = line.IndexOf(text, index, StringComparison.Ordinal)) >= 0)
            {
                results.Add((y, index));
                index++;
            }
        }
        return results;
    }

    // === Input Injection APIs (Testing) ===

    /// <summary>
    /// Sends any input event to the terminal (for testing).
    /// This is the unified API for injecting keyboard, mouse, and other events.
    /// Only works with Hex1bAppWorkloadAdapter.
    /// </summary>
    /// <param name="evt">The event to send.</param>
    internal void SendEvent(Hex1bEvent evt)
    {
        if (_workload is Hex1bAppWorkloadAdapter appWorkload)
        {
            appWorkload.TryWriteInputEvent(evt);
        }
    }

    /// <summary>
    /// Creates an immutable snapshot of the current terminal state.
    /// Useful for assertions and wait conditions in tests.
    /// Automatically flushes pending output before creating the snapshot.
    /// </summary>
    public Testing.Hex1bTerminalSnapshot CreateSnapshot()
    {
        FlushOutput();
        return new Testing.Hex1bTerminalSnapshot(this);
    }

    /// <summary>
    /// Resizes the terminal, preserving content where possible.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        var newBuffer = new TerminalCell[newHeight, newWidth];
        
        // Initialize with empty cells
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                newBuffer[y, x] = TerminalCell.Empty;
            }
        }

        // Copy existing content that fits in the new size
        var copyHeight = Math.Min(_height, newHeight);
        var copyWidth = Math.Min(_width, newWidth);
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                newBuffer[y, x] = _screenBuffer[y, x];
            }
        }
        
        // Release tracked objects from cells that are being removed
        // (cells outside the new bounds)
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                // Skip cells that were copied to the new buffer
                if (y < copyHeight && x < copyWidth)
                    continue;
                    
                _screenBuffer[y, x].TrackedSixel?.Release();
            }
        }

        _screenBuffer = newBuffer;
        _width = newWidth;
        _height = newHeight;
        _cursorX = Math.Min(_cursorX, newWidth - 1);
        _cursorY = Math.Min(_cursorY, newHeight - 1);
    }

    // === Screen Buffer Parsing ===

    /// <summary>
    /// Sets a cell in the screen buffer, properly managing tracked object references.
    /// Releases the old cell's tracked object (if any) and adds a reference for the new one (if any).
    /// </summary>
    private void SetCell(int y, int x, TerminalCell newCell)
    {
        ref var oldCell = ref _screenBuffer[y, x];
        
        // Release old Sixel data reference
        oldCell.TrackedSixel?.Release();
        
        // Note: new cell's tracked object already has a reference from GetOrCreateSixel
        // No need to AddRef here - the caller is responsible for getting the object
        // with the correct refcount
        
        oldCell = newCell;
    }

    /// <summary>
    /// Gets or creates a tracked Sixel object for the given payload.
    /// The returned object has a reference count that accounts for this request.
    /// </summary>
    internal TrackedObject<SixelData> GetOrCreateSixel(string payload, int widthInCells, int heightInCells)
    {
        return _trackedObjects.GetOrCreateSixel(payload, widthInCells, heightInCells);
    }

    /// <summary>
    /// Gets the number of tracked Sixel objects in the store.
    /// Useful for testing to verify cleanup behavior.
    /// </summary>
    internal int TrackedSixelCount => _trackedObjects.SixelCount;

    /// <summary>
    /// Gets the Sixel data at the specified cell position, if any.
    /// Returns null if the cell doesn't contain Sixel data or is a continuation cell.
    /// </summary>
    internal SixelData? GetSixelDataAt(int x, int y)
    {
        FlushOutput();
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return null;
        return _screenBuffer[y, x].SixelData;
    }

    /// <summary>
    /// Gets the tracked Sixel object at the specified cell position, if any.
    /// Returns null if the cell doesn't contain Sixel data.
    /// Useful for testing reference count behavior.
    /// </summary>
    internal TrackedObject<SixelData>? GetTrackedSixelAt(int x, int y)
    {
        FlushOutput();
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return null;
        return _screenBuffer[y, x].TrackedSixel;
    }

    /// <summary>
    /// Checks if any cell in the screen buffer contains Sixel data.
    /// </summary>
    internal bool ContainsSixelData()
    {
        FlushOutput();
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (_screenBuffer[y, x].TrackedSixel is not null)
                    return true;
            }
        }
        return false;
    }

    private void ClearBuffer()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                SetCell(y, x, TerminalCell.Empty);
            }
        }
        _currentForeground = null;
        _currentBackground = null;
    }

    /// <summary>
    /// Process ANSI output text into the screen buffer.
    /// </summary>
    /// <param name="text">Text containing ANSI escape sequences.</param>
    internal void ProcessOutput(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            // Check for DCS sequence (ESC P or 0x90) - Sixel starts with ESC P q
            if (TryParseSixelSequence(text, i, out var sixelConsumed, out var sixelPayload))
            {
                ProcessSixelData(sixelPayload);
                i += sixelConsumed;
            }
            else if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                i = ProcessAnsiSequence(text, i);
            }
            else if (text[i] == '\n')
            {
                _cursorY++;
                _cursorX = 0;
                if (_cursorY >= _height)
                {
                    ScrollUp();
                    _cursorY = _height - 1;
                }
                i++;
            }
            else if (text[i] == '\r')
            {
                _cursorX = 0;
                i++;
            }
            else
            {
                // Extract the grapheme cluster starting at position i
                var grapheme = GetGraphemeAt(text, i);
                var graphemeWidth = DisplayWidth.GetGraphemeWidth(grapheme);
                
                // Scroll if cursor is past the bottom of the screen BEFORE writing
                // This implements "delayed line wrap" behavior
                if (_cursorY >= _height)
                {
                    ScrollUp();
                    _cursorY = _height - 1;
                }
                
                if (_cursorX < _width && _cursorY < _height)
                {
                    // Assign sequence number and timestamp for this write operation
                    var sequence = ++_writeSequence;
                    var writtenAt = _timeProvider.GetUtcNow();
                    
                    // Write the grapheme to the current cell
                    SetCell(_cursorY, _cursorX, new TerminalCell(
                        grapheme, _currentForeground, _currentBackground, _currentAttributes,
                        sequence, writtenAt));
                    
                    // For wide characters (width > 1), fill subsequent cells with continuation markers
                    // Use the same sequence and timestamp so they're part of the same logical write
                    for (int w = 1; w < graphemeWidth && _cursorX + w < _width; w++)
                    {
                        // Use empty string as continuation marker (the previous cell "owns" this space)
                        SetCell(_cursorY, _cursorX + w, new TerminalCell(
                            "", _currentForeground, _currentBackground, _currentAttributes,
                            sequence, writtenAt));
                    }
                    
                    _cursorX += graphemeWidth;
                    if (_cursorX >= _width)
                    {
                        _cursorX = 0;
                        _cursorY++;
                        // Don't scroll yet - we'll scroll when we try to write the next character
                    }
                }
                i += grapheme.Length;
            }
        }
    }

    /// <summary>
    /// Gets the grapheme cluster starting at the given position in the text.
    /// </summary>
    private static string GetGraphemeAt(string text, int index)
    {
        if (index >= text.Length)
            return "";
            
        var enumerator = StringInfo.GetTextElementEnumerator(text, index);
        if (enumerator.MoveNext())
        {
            return (string)enumerator.Current;
        }
        return text[index].ToString();
    }

    private int ProcessAnsiSequence(string text, int start)
    {
        int end = start + 2;
        while (end < text.Length && !char.IsLetter(text[end]))
        {
            end++;
        }

        if (end >= text.Length)
            return end;

        var command = text[end];
        var parameters = text[(start + 2)..end];

        switch (command)
        {
            case 'm':
                ProcessSgr(parameters);
                break;
            case 'H':
                ProcessCursorPosition(parameters);
                break;
            case 'J':
                ProcessClearScreen(parameters);
                break;
            case 'h':
            case 'l':
                if (parameters.Contains("?1049"))
                {
                    if (command == 'h')
                        DoEnterAlternateScreen();
                    else
                        DoExitAlternateScreen();
                }
                break;
        }

        return end + 1;
    }

    private void DoEnterAlternateScreen()
    {
        _inAlternateScreen = true;
        ClearBuffer();
        _cursorX = 0;
        _cursorY = 0;
    }

    private void DoExitAlternateScreen()
    {
        _inAlternateScreen = false;
    }

    private void ProcessSgr(string parameters)
    {
        if (string.IsNullOrEmpty(parameters) || parameters == "0")
        {
            _currentForeground = null;
            _currentBackground = null;
            _currentAttributes = CellAttributes.None;
            return;
        }

        var parts = parameters.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var code))
                continue;

            switch (code)
            {
                case 0:
                    _currentForeground = null;
                    _currentBackground = null;
                    _currentAttributes = CellAttributes.None;
                    break;

                // Text attributes - set
                case 1:
                    _currentAttributes |= CellAttributes.Bold;
                    break;
                case 2:
                    _currentAttributes |= CellAttributes.Dim;
                    break;
                case 3:
                    _currentAttributes |= CellAttributes.Italic;
                    break;
                case 4:
                    _currentAttributes |= CellAttributes.Underline;
                    break;
                case 5:
                case 6: // Rapid blink treated same as slow blink
                    _currentAttributes |= CellAttributes.Blink;
                    break;
                case 7:
                    _currentAttributes |= CellAttributes.Reverse;
                    break;
                case 8:
                    _currentAttributes |= CellAttributes.Hidden;
                    break;
                case 9:
                    _currentAttributes |= CellAttributes.Strikethrough;
                    break;
                case 53:
                    _currentAttributes |= CellAttributes.Overline;
                    break;

                // Text attributes - reset
                case 21: // Double underline OR bold off (varies by terminal)
                case 22: // Normal intensity (not bold, not dim)
                    _currentAttributes &= ~(CellAttributes.Bold | CellAttributes.Dim);
                    break;
                case 23:
                    _currentAttributes &= ~CellAttributes.Italic;
                    break;
                case 24:
                    _currentAttributes &= ~CellAttributes.Underline;
                    break;
                case 25:
                    _currentAttributes &= ~CellAttributes.Blink;
                    break;
                case 27:
                    _currentAttributes &= ~CellAttributes.Reverse;
                    break;
                case 28:
                    _currentAttributes &= ~CellAttributes.Hidden;
                    break;
                case 29:
                    _currentAttributes &= ~CellAttributes.Strikethrough;
                    break;
                case 55:
                    _currentAttributes &= ~CellAttributes.Overline;
                    break;

                // Foreground colors
                case >= 30 and <= 37:
                    _currentForeground = StandardColorFromCode(code - 30);
                    break;
                case >= 40 and <= 47:
                    _currentBackground = StandardColorFromCode(code - 40);
                    break;
                case >= 90 and <= 97:
                    _currentForeground = BrightColorFromCode(code - 90);
                    break;
                case >= 100 and <= 107:
                    _currentBackground = BrightColorFromCode(code - 100);
                    break;
                case 38:
                    if (i + 2 < parts.Length && parts[i + 1] == "5")
                    {
                        if (int.TryParse(parts[i + 2], out var colorIndex))
                        {
                            _currentForeground = Color256FromIndex(colorIndex);
                        }
                        i += 2;
                    }
                    else if (i + 4 < parts.Length && parts[i + 1] == "2")
                    {
                        if (int.TryParse(parts[i + 2], out var r) &&
                            int.TryParse(parts[i + 3], out var g) &&
                            int.TryParse(parts[i + 4], out var b))
                        {
                            _currentForeground = Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
                        }
                        i += 4;
                    }
                    break;
                case 48:
                    if (i + 2 < parts.Length && parts[i + 1] == "5")
                    {
                        if (int.TryParse(parts[i + 2], out var colorIndex))
                        {
                            _currentBackground = Color256FromIndex(colorIndex);
                        }
                        i += 2;
                    }
                    else if (i + 4 < parts.Length && parts[i + 1] == "2")
                    {
                        if (int.TryParse(parts[i + 2], out var r) &&
                            int.TryParse(parts[i + 3], out var g) &&
                            int.TryParse(parts[i + 4], out var b))
                        {
                            _currentBackground = Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
                        }
                        i += 4;
                    }
                    break;
            }
        }
    }

    private void ProcessCursorPosition(string parameters)
    {
        if (string.IsNullOrEmpty(parameters))
        {
            _cursorX = 0;
            _cursorY = 0;
            return;
        }

        var parts = parameters.Split(';');
        if (parts.Length >= 2)
        {
            if (int.TryParse(parts[0], out var row) && int.TryParse(parts[1], out var col))
            {
                _cursorY = Math.Clamp(row - 1, 0, _height - 1);
                _cursorX = Math.Clamp(col - 1, 0, _width - 1);
            }
        }
        else if (parts.Length == 1)
        {
            if (int.TryParse(parts[0], out var row))
            {
                _cursorY = Math.Clamp(row - 1, 0, _height - 1);
            }
        }
    }

    private void ProcessClearScreen(string parameters)
    {
        var mode = string.IsNullOrEmpty(parameters) ? 0 : int.TryParse(parameters, out var m) ? m : 0;

        switch (mode)
        {
            case 0:
                ClearFromCursor();
                break;
            case 1:
                ClearToCursor();
                break;
            case 2:
            case 3:
                ClearBuffer();
                break;
        }
    }

    private void ClearFromCursor()
    {
        for (int x = _cursorX; x < _width; x++)
        {
            SetCell(_cursorY, x, TerminalCell.Empty);
        }
        for (int y = _cursorY + 1; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                SetCell(y, x, TerminalCell.Empty);
            }
        }
    }

    private void ClearToCursor()
    {
        for (int y = 0; y < _cursorY; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                SetCell(y, x, TerminalCell.Empty);
            }
        }
        for (int x = 0; x <= _cursorX && x < _width; x++)
        {
            SetCell(_cursorY, x, TerminalCell.Empty);
        }
    }

    private void ScrollUp()
    {
        // First, release Sixel data from the top row (being scrolled off)
        for (int x = 0; x < _width; x++)
        {
            _screenBuffer[0, x].TrackedSixel?.Release();
        }
        
        // Shift all rows up (tracked object refs move with them, no AddRef/Release needed)
        for (int y = 0; y < _height - 1; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _screenBuffer[y, x] = _screenBuffer[y + 1, x];
            }
        }
        
        // Clear the bottom row (no tracked objects to release - they moved up)
        for (int x = 0; x < _width; x++)
        {
            _screenBuffer[_height - 1, x] = TerminalCell.Empty;
        }
    }

    // === Sixel Parsing ===

    /// <summary>
    /// Tries to parse a Sixel DCS sequence starting at the given position.
    /// Sixel format: ESC P q [sixel data] ESC \ (or ESC P q [sixel data] 0x9C)
    /// </summary>
    private static bool TryParseSixelSequence(string text, int start, out int consumed, out string payload)
    {
        consumed = 0;
        payload = "";

        // Check for DCS start: ESC P (0x1b 0x50) or 0x90
        bool isDcsStart = false;
        int dataStart = start;

        if (start + 1 < text.Length && text[start] == '\x1b' && text[start + 1] == 'P')
        {
            isDcsStart = true;
            dataStart = start + 2;
        }
        else if (text[start] == '\x90')
        {
            isDcsStart = true;
            dataStart = start + 1;
        }

        if (!isDcsStart)
            return false;

        // Check if this is a Sixel sequence (starts with 'q' after optional params)
        // Skip optional parameters and look for 'q'
        int i = dataStart;
        while (i < text.Length && (char.IsDigit(text[i]) || text[i] == ';'))
        {
            i++;
        }

        if (i >= text.Length || text[i] != 'q')
            return false;

        i++; // Skip 'q'

        // Find the ST (String Terminator): ESC \ (0x1b 0x5c) or 0x9C
        int dataEnd = -1;
        for (int j = i; j < text.Length; j++)
        {
            if (j + 1 < text.Length && text[j] == '\x1b' && text[j + 1] == '\\')
            {
                dataEnd = j;
                consumed = j + 2 - start; // Include ESC \
                break;
            }
            else if (text[j] == '\x9c')
            {
                dataEnd = j;
                consumed = j + 1 - start; // Include 0x9C
                break;
            }
        }

        if (dataEnd < 0)
            return false; // No terminator found

        // Extract the full Sixel sequence (including DCS header and ST)
        payload = text.Substring(start, consumed);
        return true;
    }

    /// <summary>
    /// Processes Sixel data by creating a tracked object and marking cells.
    /// </summary>
    private void ProcessSixelData(string sixelPayload)
    {
        // Estimate the size in cells based on Sixel data
        // This is approximate - Sixel images can specify dimensions in the data
        var (widthInCells, heightInCells) = EstimateSixelDimensions(sixelPayload);

        // Create or reuse a tracked Sixel object
        var sixelData = _trackedObjects.GetOrCreateSixel(sixelPayload, widthInCells, heightInCells);

        // Mark cells covered by this Sixel image
        // The first cell gets the tracked object, others just get the Sixel flag
        var sequence = ++_writeSequence;
        var writtenAt = _timeProvider.GetUtcNow();

        for (int dy = 0; dy < heightInCells && _cursorY + dy < _height; dy++)
        {
            for (int dx = 0; dx < widthInCells && _cursorX + dx < _width; dx++)
            {
                var y = _cursorY + dy;
                var x = _cursorX + dx;

                // First cell (origin) holds the tracked object
                // Other cells just have the Sixel flag set
                if (dx == 0 && dy == 0)
                {
                    SetCell(y, x, new TerminalCell(
                        " ", _currentForeground, _currentBackground,
                        _currentAttributes | CellAttributes.Sixel,
                        sequence, writtenAt, sixelData));
                }
                else
                {
                    // Continuation cells - just mark as Sixel, no tracked object
                    // (The origin cell owns the reference)
                    SetCell(y, x, new TerminalCell(
                        "", _currentForeground, _currentBackground,
                        _currentAttributes | CellAttributes.Sixel,
                        sequence, writtenAt));
                }
            }
        }
    }

    /// <summary>
    /// Estimates Sixel image dimensions in terminal cells.
    /// </summary>
    private (int Width, int Height) EstimateSixelDimensions(string sixelPayload)
    {
        // Default dimensions if we can't parse
        int width = 1;
        int height = 1;
        
        // Get cell pixel dimensions from capabilities
        var cellWidth = Capabilities.CellPixelWidth;
        var cellHeight = Capabilities.CellPixelHeight;

        // Try to find "width;height in the sixel raster attributes
        // Format: "Pan;Pad;Ph;Pv where Ph = pixel height, Pv = pixel width
        // This appears after the 'q' and before the first color definition '#'
        var qIndex = sixelPayload.IndexOf('q');
        var hashIndex = sixelPayload.IndexOf('#');

        if (qIndex >= 0)
        {
            var rasterEnd = hashIndex > qIndex ? hashIndex : sixelPayload.Length;
            var rasterSection = sixelPayload.Substring(qIndex + 1, Math.Min(50, rasterEnd - qIndex - 1));

            // Look for raster attributes: "Pan;Pad;Ph;Pv
            var quoteIndex = rasterSection.IndexOf('"');
            if (quoteIndex >= 0 && quoteIndex + 1 < rasterSection.Length)
            {
                var attrStr = rasterSection.Substring(quoteIndex + 1);
                // Find end of raster attributes - terminated by sixel control chars (not semicolon)
                var endIndex = attrStr.IndexOfAny(['#', '!', '-', '$', '~', '?']);
                if (endIndex < 0) endIndex = attrStr.Length;
                attrStr = attrStr.Substring(0, Math.Min(30, endIndex));

                var parts = attrStr.Split(';');
                if (parts.Length >= 4)
                {
                    // Ph = pixel height, Pv = pixel width
                    if (int.TryParse(parts[2], out var ph) && int.TryParse(parts[3], out var pv))
                    {
                        // Convert pixels to cells using actual cell dimensions
                        // Round up to ensure the image doesn't get cut off
                        width = Math.Max(1, (pv + cellWidth - 1) / cellWidth);
                        height = Math.Max(1, (ph + cellHeight - 1) / cellHeight);
                    }
                }
            }
        }

        // Clamp to reasonable bounds
        return (Math.Clamp(width, 1, 200), Math.Clamp(height, 1, 100));
    }

    // === Static Input Parsing Helpers ===

    private static bool TryParseDA1Response(string message, int start, out int consumed)
    {
        consumed = 0;
        
        if (start + 3 < message.Length && 
            message[start] == '\x1b' && 
            message[start + 1] == '[' && 
            message[start + 2] == '?')
        {
            for (var j = start + 3; j < message.Length; j++)
            {
                if (message[j] == 'c')
                {
                    // DA1 response format: ESC [ ? {params} c
                    // We consume it but capabilities are now provided via TerminalCapabilities
                    // rather than runtime detection
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

        var i = start + 2;
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

        var i = start + 3;

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

    private static ConsoleKey CharToConsoleKey(char c)
    {
        return char.ToUpperInvariant(c) switch
        {
            >= 'A' and <= 'Z' => (ConsoleKey)(c - 'a' + (int)ConsoleKey.A),
            >= '0' and <= '9' => (ConsoleKey)(c - '0' + (int)ConsoleKey.D0),
            ' ' => ConsoleKey.Spacebar,
            '\t' => ConsoleKey.Tab,
            '\n' or '\r' => ConsoleKey.Enter,
            _ => ConsoleKey.NoName
        };
    }

    private static Hex1bColor StandardColorFromCode(int code) => code switch
    {
        0 => Hex1bColor.FromRgb(0, 0, 0),
        1 => Hex1bColor.FromRgb(128, 0, 0),
        2 => Hex1bColor.FromRgb(0, 128, 0),
        3 => Hex1bColor.FromRgb(128, 128, 0),
        4 => Hex1bColor.FromRgb(0, 0, 128),
        5 => Hex1bColor.FromRgb(128, 0, 128),
        6 => Hex1bColor.FromRgb(0, 128, 128),
        7 => Hex1bColor.FromRgb(192, 192, 192),
        _ => Hex1bColor.FromRgb(128, 128, 128)
    };

    private static Hex1bColor BrightColorFromCode(int code) => code switch
    {
        0 => Hex1bColor.FromRgb(128, 128, 128),
        1 => Hex1bColor.FromRgb(255, 0, 0),
        2 => Hex1bColor.FromRgb(0, 255, 0),
        3 => Hex1bColor.FromRgb(255, 255, 0),
        4 => Hex1bColor.FromRgb(0, 0, 255),
        5 => Hex1bColor.FromRgb(255, 0, 255),
        6 => Hex1bColor.FromRgb(0, 255, 255),
        7 => Hex1bColor.FromRgb(255, 255, 255),
        _ => Hex1bColor.FromRgb(192, 192, 192)
    };

    private static Hex1bColor Color256FromIndex(int index)
    {
        if (index < 16)
        {
            return index < 8 ? StandardColorFromCode(index) : BrightColorFromCode(index - 8);
        }
        else if (index < 232)
        {
            index -= 16;
            var r = (index / 36) * 51;
            var g = ((index / 6) % 6) * 51;
            var b = (index % 6) * 51;
            return Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
        }
        else
        {
            var gray = (index - 232) * 10 + 8;
            return Hex1bColor.FromRgb((byte)gray, (byte)gray, (byte)gray);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Notify filters of session end (fire-and-forget from sync Dispose)
        var elapsed = _timeProvider.GetUtcNow() - _sessionStart;
        _ = NotifyWorkloadFiltersSessionEndAsync(elapsed);
        _ = NotifyPresentationFiltersSessionEndAsync(elapsed);

        // Exit TUI mode before disposing
        if (_presentation != null)
        {
            // Fire and forget - ExitTuiModeAsync is typically synchronous for console
            _ = _presentation.ExitTuiModeAsync();
            _presentation.Resized -= OnPresentationResized;
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Notify filters of session end
        var elapsed = _timeProvider.GetUtcNow() - _sessionStart;
        await NotifyWorkloadFiltersSessionEndAsync(elapsed);
        await NotifyPresentationFiltersSessionEndAsync(elapsed);

        if (_presentation != null)
        {
            // Exit TUI mode before disposing
            await _presentation.ExitTuiModeAsync();
            _presentation.Resized -= OnPresentationResized;
            await _presentation.DisposeAsync();
        }

        await _workload.DisposeAsync();

        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    // === Filter Notification Helpers ===

    private TimeSpan GetElapsed() => _timeProvider.GetUtcNow() - _sessionStart;

    private async ValueTask NotifyWorkloadFiltersSessionStartAsync()
    {
        foreach (var filter in _workloadFilters)
        {
            await filter.OnSessionStartAsync(_width, _height, _sessionStart);
        }
    }

    private async ValueTask NotifyWorkloadFiltersSessionEndAsync(TimeSpan elapsed)
    {
        foreach (var filter in _workloadFilters)
        {
            await filter.OnSessionEndAsync(elapsed);
        }
    }

    private async ValueTask NotifyWorkloadFiltersOutputAsync(ReadOnlyMemory<byte> data)
    {
        if (_workloadFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _workloadFilters)
        {
            await filter.OnOutputAsync(data, elapsed);
        }
    }

    private async ValueTask NotifyWorkloadFiltersFrameCompleteAsync()
    {
        if (_workloadFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _workloadFilters)
        {
            await filter.OnFrameCompleteAsync(elapsed);
        }
    }

    private async ValueTask NotifyWorkloadFiltersInputAsync(ReadOnlyMemory<byte> data)
    {
        if (_workloadFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _workloadFilters)
        {
            await filter.OnInputAsync(data, elapsed);
        }
    }

    private async ValueTask NotifyWorkloadFiltersResizeAsync(int width, int height)
    {
        if (_workloadFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _workloadFilters)
        {
            await filter.OnResizeAsync(width, height, elapsed);
        }
    }

    private async ValueTask NotifyPresentationFiltersSessionStartAsync()
    {
        foreach (var filter in _presentationFilters)
        {
            await filter.OnSessionStartAsync(_width, _height, _sessionStart);
        }
    }

    private async ValueTask NotifyPresentationFiltersSessionEndAsync(TimeSpan elapsed)
    {
        foreach (var filter in _presentationFilters)
        {
            await filter.OnSessionEndAsync(elapsed);
        }
    }

    private async ValueTask NotifyPresentationFiltersOutputAsync(ReadOnlyMemory<byte> data)
    {
        if (_presentationFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _presentationFilters)
        {
            await filter.OnOutputAsync(data, elapsed);
        }
    }

    private async ValueTask NotifyPresentationFiltersInputAsync(ReadOnlyMemory<byte> data)
    {
        if (_presentationFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _presentationFilters)
        {
            await filter.OnInputAsync(data, elapsed);
        }
    }

    private async ValueTask NotifyPresentationFiltersResizeAsync(int width, int height)
    {
        if (_presentationFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _presentationFilters)
        {
            await filter.OnResizeAsync(width, height, elapsed);
        }
    }
}
