#pragma warning disable HEX1B_SIXEL // Sixel API is experimental - internal usage is allowed

using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Reflow;
using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b;

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
public sealed class Hex1bTerminal : IDisposable, IAsyncDisposable
{
    private readonly IHex1bTerminalPresentationAdapter _presentation;
    private readonly IHex1bTerminalWorkloadAdapter _workload;
    private readonly Func<CancellationToken, Task<int>>? _runCallback;
    private readonly IReadOnlyList<IHex1bTerminalWorkloadFilter> _workloadFilters;
    private readonly IReadOnlyList<IHex1bTerminalPresentationFilter> _presentationFilters;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly DateTimeOffset _sessionStart;
    private readonly TrackedObjectStore _trackedObjects = new();
    private readonly TimeProvider _timeProvider;
    private readonly KgpImageStore _kgpImageStore = new();
    private readonly List<KgpPlacement> _kgpPlacements = new();
    
    // Lock to protect screen buffer state from concurrent access.
    // The resize event comes from the input thread while the output pump
    // runs on a separate thread, both accessing _screenBuffer, _width, _height.
    private readonly object _bufferLock = new();
    private readonly SemaphoreSlim _workloadInputWriteLock = new(1, 1);
    
    private TerminalCell[,] _screenBuffer;
    private int _cursorX;
    private int _cursorY;
    private Hex1bColor? _currentForeground;
    private Hex1bColor? _currentBackground;
    private Hex1bColor? _currentUnderlineColor;
    private UnderlineStyle _currentUnderlineStyle;
    private CellAttributes _currentAttributes;
    private TrackedObject<HyperlinkData>? _currentHyperlink; // Active hyperlink from OSC 8
    private bool _disposed;
    private bool _inAlternateScreen;
    private TerminalCell[,]? _savedMainScreenBuffer; // Saved main screen when entering alternate screen
    private int _alternateScreenSavedCursorX; // Saved cursor X for alternate screen (mode 1049)
    private int _alternateScreenSavedCursorY; // Saved cursor Y for alternate screen (mode 1049)
    private Task? _inputProcessingTask;
    private Task? _outputProcessingTask;
    private readonly TaskCompletionSource<(string PumpName, Exception Error)> _pumpFaultTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _writeSequence; // Monotonically increasing write order counter
    private int _savedCursorX; // Saved cursor X position for DECSC/DECRC
    private int _savedCursorY; // Saved cursor Y position for DECSC/DECRC
    private bool _cursorSaved; // Whether cursor has been saved (for restore without prior save)
    private bool _savedPendingWrap; // Saved pending wrap state for DECSC/DECRC
    private bool _savedCursorProtected; // Saved protection state for DECSC/DECRC
    private readonly Decoder _utf8Decoder = Encoding.UTF8.GetDecoder(); // Handles incomplete UTF-8 sequences across workload output reads
    private readonly Decoder _inputUtf8Decoder = Encoding.UTF8.GetDecoder(); // Handles incomplete UTF-8 sequences across presentation input reads
    private string _incompleteSequenceBuffer = ""; // Buffers incomplete ANSI escape sequences across workload output reads
    private string _incompleteInputSequenceBuffer = ""; // Buffers incomplete ANSI escape sequences across presentation input reads
    
    // Escape-sequence timeout: channel-based architecture for zero-alloc disambiguation.
    // A background reader writes data events, and a reusable timer writes timeout
    // sentinels into the same channel.  The processing loop reads from one place.
    private readonly TimeSpan _escapeTimeout;
    private readonly Channel<PresentationInputEvent> _presentationInputChannel =
        Channel.CreateUnbounded<PresentationInputEvent>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private ITimer? _escapeFlushTimer;
    
    // Scrollback buffer (opt-in via WithScrollback)
    private readonly ScrollbackBuffer? _scrollbackBuffer;
    private readonly Action<ScrollbackRowEventArgs>? _scrollbackCallback;
    
    // Metrics instrumentation
    private readonly Diagnostics.Hex1bMetrics _metrics;
    
    // Bracketed paste state
    private bool _inBracketedPaste;
    private PasteContext? _activePasteContext;
    
    // Scroll region (DECSTBM) - 0-based indices
    private int _scrollTop; // Top margin (0 = first row)
    private int _scrollBottom; // Bottom margin (height-1 = last row), initialized in constructor
    
    // Left/Right margins (DECSLRM) - 0-based indices
    private int _marginLeft; // Left margin (0 = first column)
    private int _marginRight; // Right margin (width-1 = last column), initialized in constructor
    private bool _declrmm; // DECLRMM mode (mode 69): when true, CSI s sets left/right margins instead of saving cursor
    
    // Tab stops: a boolean array where true means a tab stop is set at that column.
    // Initialized with default tab stops every 8 columns.
    private bool[] _tabStops = Array.Empty<bool>();
    
    // Deferred wrap (standard terminal behavior): when writing to the last column,
    // wrap is deferred until the next printable character. CR/LF clear the pending wrap.
    private bool _pendingWrap;
    
    // Line Feed/New Line Mode (LNM, DEC mode 20): when true, LF also performs CR.
    // Real terminal emulators (xterm, etc.) have this OFF by default.
    // The ONLCR translation (LF→CRLF) happens in the PTY/TTY kernel driver, not the terminal.
    // Full-screen apps like vim/mapscii disable ONLCR and send explicit \r\n.
    private bool _newlineMode = false;
    
    // Origin Mode (DECOM, DEC mode 6): when true, cursor positions are relative to scroll region.
    // When false (default), cursor positions are absolute (relative to full screen).
    private bool _originMode = false;
    
    // Insert/Replace Mode (IRM, ECMA-48 mode 4): when true, printing inserts characters
    // (shifting existing content right) instead of overwriting.
    private bool _insertMode = false;
    
    // Auto-wrap mode (DECAWM, DEC private mode 7): when true, cursor wraps to next line
    // when writing past the right margin. Default is true (on).
    private bool _wraparoundMode = true;
    
    // Reverse wrap mode (DEC private mode 45): when true, cursor can wrap backwards
    // to the end of the previous line when moving left past column 0, but only if
    // the previous line was soft-wrapped. Default is false (off).
    private bool _reverseWrapMode = false;
    
    // Extended reverse wrap mode (xterm private mode 1045): like reverse wrap but
    // also wraps across hard line boundaries, and wraps from top to bottom.
    // Takes priority over mode 45 when both are set. Default is false (off).
    private bool _reverseWrapExtendedMode = false;
    
    // Grapheme clustering mode (mode 2027): when true, multi-codepoint grapheme
    // clusters (like ZWJ emoji sequences, Devanagari ligatures) are combined into
    // single cells. When false, each codepoint is treated individually.
    // Default is true — Hex1b always clusters graphemes like modern terminals.
    // Applications can disable with CSI ? 2027 l for per-codepoint handling.
    private bool _graphemeClusterMode = true;
    
    // Last printed character for CSI b (REP - repeat) command
    private TerminalCell _lastPrintedCell = TerminalCell.Empty;
    private bool _hasLastPrintedCell = false;
    
    // Position and width of last printed character for retroactive VS15/VS16 handling
    private int _lastPrintedCellX = 0;
    private int _lastPrintedCellY = 0;
    private int _lastPrintedCellWidth = 0;
    
    // When true, the last printed cell ends with a ZWJ (U+200D) or combining mark,
    // meaning the next emoji/character should combine with it rather than printing
    // in a new cell. Used for building ZWJ sequences across separate Feed() calls.
    private bool _pendingGraphemeCombine = false;
    
    // DECSCA character protection mode. Tracks whether newly printed characters
    
    // Character set designation (VT100/VT220):
    // G0-G3 are the four character set slots. Each can be designated as ASCII ('B'),
    // DEC special graphics ('0'), or other charsets.
    // GL (Graphics Left) is the active character set for normal printing.
    // SO (0x0E) invokes G1 into GL, SI (0x0F) invokes G0 into GL.
    private char _charsetG0 = 'B'; // G0 = ASCII by default
    private char _charsetG1 = 'B'; // G1 = ASCII by default
    private char _charsetG2 = 'B'; // G2 = ASCII by default
    private char _charsetG3 = 'B'; // G3 = ASCII by default
    private int _activeCharsetSlot = 0; // Which Gn is active in GL (0=G0, 1=G1, etc.)
    // are marked as protected (immune to selective erase DECSED/DECSEL).
    // _protectedMode tracks the MOST RECENT protection mode set (iso or dec) and
    // is never reset to Off — this is intentional: erase operations need to know
    // the most recent mode to decide whether ISO protection applies.
    // _cursorProtected tracks whether the cursor is currently in protected mode.
    private ProtectedMode _protectedMode = ProtectedMode.Off;
    private bool _cursorProtected = false;
    
    /// <summary>Gets whether the cursor is currently in protected mode (for testing).</summary>
    internal bool CursorProtected => _cursorProtected;
    
    // Terminal title (OSC 0/2) and icon name (OSC 0/1)
    // OSC 0 sets both, OSC 1 sets icon only, OSC 2 sets title only
    private string _windowTitle = "";
    private string _iconName = "";
    
    // Title stack for OSC 22/23 (push/pop)
    // When OSC 22 is received, current (title, iconName) is pushed onto stack
    // When OSC 23 is received, (title, iconName) is popped and restored
    // OSC 0/1/2 do NOT affect the stack - they only change current values
    private readonly Stack<(string Title, string IconName)> _titleStack = new();

    // === Static Factory ===

    /// <summary>
    /// Creates a new terminal builder for fluent configuration.
    /// </summary>
    /// <returns>A new <see cref="Hex1bTerminalBuilder"/> instance.</returns>
    /// <example>
    /// <code>
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithHex1bApp(ctx => ctx.Text("Hello"))
    ///     .RunAsync();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder CreateBuilder() => new();

    // === Constructor ===

    /// <summary>
    /// Creates a new terminal with the specified options.
    /// </summary>
    /// <param name="options">Terminal configuration options.</param>
    public Hex1bTerminal(Hex1bTerminalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        
        var presentation = options.PresentationAdapter ?? new HeadlessPresentationAdapter(options.Width, options.Height);
        var workload = options.WorkloadAdapter ?? throw new ArgumentNullException(nameof(options), "WorkloadAdapter is required");
        
        _presentation = presentation;
        _workload = workload;
        _runCallback = options.RunCallback;
        _workloadFilters = options.WorkloadFilters?.ToList() ?? [];
        _presentationFilters = options.PresentationFilters?.ToList() ?? [];
        _timeProvider = options.TimeProvider ?? TimeProvider.System;
        _sessionStart = _timeProvider.GetUtcNow();
        
        // Notify lifecycle-aware presentation adapters that the terminal is created
        if (presentation is ITerminalLifecycleAwarePresentationAdapter lifecycleAdapter)
        {
            lifecycleAdapter.TerminalCreated(this);
        }
        
        // Notify terminal-aware presentation filters
        foreach (var filter in _presentationFilters)
        {
            if (filter is ITerminalAwarePresentationFilter terminalAwareFilter)
            {
                terminalAwareFilter.SetTerminal(this);
            }
        }
        
        // Get dimensions from presentation adapter (it's the source of truth)
        _width = _presentation.Width > 0 ? _presentation.Width : options.Width;
        _height = _presentation.Height > 0 ? _presentation.Height : options.Height;
        
        // Notify workload of initial dimensions (ResizeAsync handles not firing event on init)
        _ = _workload.ResizeAsync(_width, _height);
        
        _screenBuffer = new TerminalCell[_height, _width];
        _scrollBottom = _height - 1; // Default scroll region is full screen
        _marginRight = _width - 1; // Default left/right margins are full screen
        InitializeTabStops();
        
        ClearBuffer();

        // Initialize scrollback buffer if configured
        if (options.ScrollbackCapacity is int scrollbackCapacity)
        {
            _scrollbackBuffer = new ScrollbackBuffer(scrollbackCapacity);
            _scrollbackCallback = options.ScrollbackCallback;
        }
        
        _metrics = options.Metrics ?? Diagnostics.Hex1bMetrics.Default;
        _escapeTimeout = options.EscapeSequenceTimeout ?? TimeSpan.FromMilliseconds(50);

        // Subscribe to presentation events
        _presentation.Resized += OnPresentationResized;

        // Notify filters of session start
        // Note: We fire-and-forget here since the constructor can't be async
        // Filters should handle this gracefully
        _ = NotifyWorkloadFiltersSessionStartAsync();
        _ = NotifyPresentationFiltersSessionStartAsync();

        // Auto-start I/O pumps when no runCallback is set.
        // When a runCallback is provided (builder pattern), the workload may not be ready
        // (e.g., PTY process not started yet), so we defer starting pumps to RunAsync().
        if (_runCallback == null)
        {
            Start();
        }
    }

    private int _width;
    private int _height;

    private void OnPresentationResized(int width, int height)
    {
        ResizeWithWorkload(width, height);
    }

    /// <summary>
    /// Resizes the terminal buffer and propagates the resize to the workload (PTY child process).
    /// Used by diagnostics to ensure child processes receive SIGWINCH.
    /// </summary>
    internal void ResizeWithWorkload(int width, int height)
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
    /// The workload adapter for this terminal.
    /// </summary>
    internal IHex1bTerminalWorkloadAdapter Workload => _workload;
    
    /// <summary>
    /// Terminal capabilities from the presentation adapter, workload adapter, or defaults.
    /// Priority: presentation adapter > workload adapter (if IHex1bAppTerminalWorkloadAdapter) > defaults.
    /// </summary>
    internal TerminalCapabilities Capabilities => 
        _presentation?.Capabilities 
        ?? (_workload as IHex1bAppTerminalWorkloadAdapter)?.Capabilities 
        ?? TerminalCapabilities.Modern;

    // === Title Properties ===

    /// <summary>
    /// Gets the current window title set by OSC 0 or OSC 2 sequences.
    /// </summary>
    /// <remarks>
    /// <para>The window title can be set by the workload (e.g., a shell or TUI application) using:</para>
    /// <list type="bullet">
    ///   <item>OSC 0 - Sets both window title and icon name</item>
    ///   <item>OSC 2 - Sets window title only</item>
    /// </list>
    /// </remarks>
    public string WindowTitle => _windowTitle;

    /// <summary>
    /// Gets the current icon name set by OSC 0 or OSC 1 sequences.
    /// </summary>
    /// <remarks>
    /// <para>The icon name is a historical X11 concept. In modern terminals it may be used for:</para>
    /// <list type="bullet">
    ///   <item>Tab titles (when different from window title)</item>
    ///   <item>Taskbar/dock tooltips</item>
    /// </list>
    /// <para>The icon name can be set by the workload using:</para>
    /// <list type="bullet">
    ///   <item>OSC 0 - Sets both window title and icon name</item>
    ///   <item>OSC 1 - Sets icon name only</item>
    /// </list>
    /// </remarks>
    public string IconName => _iconName;

    /// <summary>
    /// Event raised when the window title changes (OSC 0 or OSC 2).
    /// </summary>
    /// <remarks>
    /// The event provides the new window title as a string.
    /// </remarks>
    public event Action<string>? WindowTitleChanged;

    /// <summary>
    /// Event raised when the icon name changes (OSC 0 or OSC 1).
    /// </summary>
    /// <remarks>
    /// The event provides the new icon name as a string.
    /// </remarks>
    public event Action<string>? IconNameChanged;

    // === Terminal Control ===

    /// <summary>
    /// Starts the terminal's I/O pump loops.
    /// Called automatically when a presentation adapter is provided.
    /// </summary>
    private void Start()
    {
        // Enter raw mode on presentation if present (enables proper input capture)
        if (_presentation != null)
        {
            // Fire and forget - EnterRawModeAsync is typically synchronous for console
            _ = _presentation.EnterRawModeAsync();
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
    /// Runs the terminal until the workload exits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method starts the terminal's I/O pumps, executes the workload's run callback
    /// (if configured), and waits for completion. For terminals created via the builder
    /// pattern with <see cref="Hex1bTerminalBuilder"/>, the run callback handles the 
    /// workload lifecycle.
    /// </para>
    /// <para>
    /// For terminals created with a raw workload adapter and no run callback, this method
    /// waits for the workload to disconnect.
    /// </para>
    /// </remarks>
    /// <param name="ct">Cancellation token to stop the terminal.</param>
    /// <returns>The exit code from the workload (0 = success).</returns>
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Enter raw mode on presentation
            if (_presentation != null)
            {
                await _presentation.EnterRawModeAsync(ct);
            }

            // Start I/O pumps
            Start();
            
            // Notify lifecycle-aware presentation adapters that the terminal has started
            if (_presentation is ITerminalLifecycleAwarePresentationAdapter startedAdapter)
            {
                startedAdapter.TerminalStarted();
            }

            // Execute the run callback or wait for workload disconnect
            var runTask = _runCallback != null
                ? _runCallback(ct)
                : WaitForWorkloadDisconnectWithExitCodeAsync(ct);

            var completedTask = await Task.WhenAny(runTask, _pumpFaultTcs.Task);
            if (completedTask == _pumpFaultTcs.Task)
            {
                var (pumpName, error) = await _pumpFaultTcs.Task;
                throw new InvalidOperationException($"The {pumpName} failed.", error);
            }

            var exitCode = await runTask;
             
            // Notify lifecycle-aware adapters (for example TerminalWidgetHandle)
            // so embedded terminal UIs can surface the final exit state and swap
            // back to their fallback/not-running content.
            if (_presentation is ITerminalLifecycleAwarePresentationAdapter completedAdapter)
            {
                completedAdapter.TerminalCompleted(exitCode);
            }

            return exitCode;
        }
        finally
        {
            // Stop pumps (via dispose cancellation token)
            _disposeCts.Cancel();

            // Exit raw mode
            if (_presentation != null)
            {
                try
                {
                    await _presentation.ExitRawModeAsync(default);
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }
        }
    }

    /// <summary>
    /// Waits for the workload to disconnect.
    /// </summary>
    private async Task WaitForWorkloadDisconnectAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource();
        
        void OnDisconnect() => tcs.TrySetResult();
        
        _workload.Disconnected += OnDisconnect;
        try
        {
            using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
            await tcs.Task;
        }
        finally
        {
            _workload.Disconnected -= OnDisconnect;
        }
    }

    private async Task<int> WaitForWorkloadDisconnectWithExitCodeAsync(CancellationToken ct)
    {
        await WaitForWorkloadDisconnectAsync(ct);
        return 0;
    }

    // === I/O Pump Tasks ===

    /// <summary>
    /// Lightweight value type written into <see cref="_presentationInputChannel"/>.
    /// Either carries raw input data or signals that the escape-sequence timeout fired.
    /// </summary>
    private readonly struct PresentationInputEvent
    {
        public readonly ReadOnlyMemory<byte> Data;
        public readonly bool IsTimeout;

        private PresentationInputEvent(ReadOnlyMemory<byte> data, bool isTimeout)
        {
            Data = data;
            IsTimeout = isTimeout;
        }

        public static PresentationInputEvent FromData(ReadOnlyMemory<byte> data) => new(data, false);
        public static readonly PresentationInputEvent TimeoutSentinel = new(default, true);
    }

    /// <summary>
    /// Tiny background loop that reads raw bytes from the presentation adapter and
    /// posts them into <see cref="_presentationInputChannel"/>.  Runs for the lifetime
    /// of the terminal.  This loop does NO tokenization, no state mutation — it only
    /// moves bytes from the blocking <see cref="IHex1bTerminalPresentationAdapter.ReadInputAsync"/>
    /// call into the channel so that the processing loop can race reads against the
    /// escape-sequence timeout without allocating.
    /// </summary>
    private async Task PumpPresentationReaderAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _presentation != null)
            {
                var data = await _presentation.ReadInputAsync(ct);
                if (data.IsEmpty)
                    break;
                _presentationInputChannel.Writer.TryWrite(PresentationInputEvent.FromData(data));
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            ReportPumpFault("presentation input reader", ex);
            _presentationInputChannel.Writer.TryComplete(ex);
        }
        finally
        {
            _presentationInputChannel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Timer callback that fires when the escape-sequence timeout expires.
    /// Writes a <see cref="PresentationInputEvent.TimeoutSentinel"/> into the
    /// channel so the processing loop can flush the incomplete buffer.
    /// Zero-alloc: only sets a field on the existing channel entry.
    /// </summary>
    private void OnEscapeFlushTimerFired(object? _)
    {
        _presentationInputChannel.Writer.TryWrite(PresentationInputEvent.TimeoutSentinel);
    }

    private async Task PumpPresentationInputAsync(CancellationToken ct)
    {
        try
        {
            // Spin up the background reader that feeds raw data into the channel.
            _ = Task.Run(() => PumpPresentationReaderAsync(ct), ct);

            await foreach (var item in _presentationInputChannel.Reader.ReadAllAsync(ct))
            {
                if (item.IsTimeout)
                {
                    // Timer fired — flush the incomplete buffer as a bare Escape key
                    // (or whatever partial sequence was pending).  Guard against a
                    // spurious timeout that arrives after data already cleared the buffer.
                    if (_incompleteInputSequenceBuffer.Length > 0)
                    {
                        var flushed = _incompleteInputSequenceBuffer;
                        _incompleteInputSequenceBuffer = "";
                        await DispatchCompleteInputTextAsync(flushed, ReadOnlyMemory<byte>.Empty, ct);
                    }
                    continue;
                }

                var data = item.Data;
                _metrics.TerminalInputBytes.Record(data.Length);

                // Cancel any pending escape-sequence timeout — real data arrived.
                _escapeFlushTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

                // Tokenize input the same way we tokenize output, preserving
                // incomplete escape sequences across read boundaries. Kitty KGP
                // responses (APC: ESC _ G ... ST) may be split across reads; if
                // we tokenize each chunk independently, the leading ESC becomes a
                // spurious Escape key event and can close focused windows.
                var charCount = _inputUtf8Decoder.GetCharCount(data.Span, flush: false);
                var chars = new char[charCount];
                _inputUtf8Decoder.GetChars(data.Span, chars, flush: false);
                var decodedText = new string(chars);

                var text = _incompleteInputSequenceBuffer + decodedText;
                _incompleteInputSequenceBuffer = "";

                var extracted = ExtractIncompleteEscapeSequence(text);
                var completeText = extracted.completeText;
                _incompleteInputSequenceBuffer = extracted.incompleteSequence;

                if (!string.IsNullOrEmpty(completeText))
                {
                    await DispatchCompleteInputTextAsync(completeText, data, ct);
                }

                // If there is an incomplete escape sequence and the timeout is enabled,
                // start (or restart) the reusable timer.
                if (_incompleteInputSequenceBuffer.Length > 0 && _escapeTimeout > TimeSpan.Zero)
                {
                    _escapeFlushTimer ??= _timeProvider.CreateTimer(
                        OnEscapeFlushTimerFired, null,
                        Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    _escapeFlushTimer.Change(_escapeTimeout, Timeout.InfiniteTimeSpan);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            ReportPumpFault("presentation input pump", ex);
        }
    }

    /// <summary>
    /// Tokenizes complete input text and dispatches it to the appropriate workload.
    /// </summary>
    private async Task DispatchCompleteInputTextAsync(string completeText, ReadOnlyMemory<byte> rawData, CancellationToken ct)
    {
        var tokens = AnsiTokenizer.Tokenize(completeText);

        _metrics.TerminalInputTokens.Record(tokens.Count);

        // Notify presentation filters of input FROM presentation
        await NotifyPresentationFiltersInputAsync(tokens);

        // Notify workload filters of input going TO workload
        await NotifyWorkloadFiltersInputAsync(tokens);

        // For Hex1bAppWorkloadAdapter, convert tokens to events and dispatch
        if (_workload is Hex1bAppWorkloadAdapter appWorkload)
        {
            await DispatchTokensAsEventsAsync(tokens, appWorkload, ct);
        }
        else
        {
            // For other workloads, forward raw bytes
            if (!rawData.IsEmpty)
            {
                await WriteWorkloadInputAsync(rawData, ct);
            }
            else
            {
                // Flushed from incomplete buffer — re-encode to bytes
                var bytes = Encoding.UTF8.GetBytes(completeText);
                await WriteWorkloadInputAsync(bytes, ct);
            }
        }
    }
    
    /// <summary>
    /// Dispatches tokenized input as high-level events to the Hex1bApp workload.
    /// Handles bracketed paste accumulation: detects ESC[200~ (start) and ESC[201~ (end)
    /// markers, streaming paste data through a PasteContext instead of individual key events.
    /// </summary>
    private async Task DispatchTokensAsEventsAsync(IReadOnlyList<AnsiToken> tokens, Hex1bAppWorkloadAdapter workload, CancellationToken ct)
    {
        foreach (var token in tokens)
        {
            ct.ThrowIfCancellationRequested();
            
            // Check for bracketed paste start/end markers
            if (token is SpecialKeyToken { KeyCode: 200 })
            {
                // Paste start marker — create PasteContext and emit event
                _inBracketedPaste = true;
                _activePasteContext = new PasteContext();
                var pasteEvent = new Hex1bPasteEvent(_activePasteContext);
                await workload.WriteInputEventAsync(pasteEvent, ct);
                _metrics.TerminalInputEvents.Add(1, new KeyValuePair<string, object?>("type", "paste"));
                continue;
            }
            
            if (token is SpecialKeyToken { KeyCode: 201 })
            {
                // Paste end marker — complete the PasteContext stream
                if (_inBracketedPaste && _activePasteContext != null)
                {
                    _activePasteContext.Complete();
                    _activePasteContext = null;
                    _inBracketedPaste = false;
                }
                continue;
            }
            
            // While in bracketed paste, route text data to PasteContext stream
            if (_inBracketedPaste && _activePasteContext != null)
            {
                var pasteText = ExtractPasteText(token);
                if (pasteText != null)
                {
                    if (!_activePasteContext.IsCancelled)
                    {
                        await _activePasteContext.WriteAsync(pasteText, ct);
                    }
                    // If cancelled, we still consume the token (drain) but don't write to context
                    continue;
                }
                // Non-text tokens during paste (mouse, special keys) fall through to normal dispatch
            }
            
            var evt = TokenToEvent(token);
            if (evt != null)
            {
                await workload.WriteInputEventAsync(evt, ct);
                
                var eventType = evt switch
                {
                    Hex1bKeyEvent => "key",
                    Hex1bMouseEvent => "mouse",
                    Hex1bResizeEvent => "resize",
                    _ => "other"
                };
                _metrics.TerminalInputEvents.Add(1, new KeyValuePair<string, object?>("type", eventType));
            }
        }
    }
    
    /// <summary>
    /// Extracts text content from a token for paste accumulation.
    /// Returns null for non-text tokens (which should be dispatched normally during paste).
    /// </summary>
    private static string? ExtractPasteText(AnsiToken token)
    {
        return token switch
        {
            TextToken text => text.Text,
            ControlCharacterToken ctrl => ctrl.Character switch
            {
                '\r' => "\r",
                '\n' => "\n",
                '\t' => "\t",
                _ => ctrl.Character.ToString()
            },
            _ => null
        };
    }
    
    /// <summary>
    /// Converts an ANSI token to a Hex1b input event.
    /// Returns null if the token doesn't represent a user input event.
    /// </summary>
    private static Hex1bEvent? TokenToEvent(AnsiToken token)
    {
        return token switch
        {
            // Mouse events
            SgrMouseToken mouse => new Hex1bMouseEvent(mouse.Button, mouse.Action, mouse.X, mouse.Y, mouse.Modifiers),
            
            // SS3 sequences (F1-F4, arrow keys in application mode)
            Ss3Token ss3 => Ss3TokenToKeyEvent(ss3),
            
            // Special key sequences (Insert, Delete, PgUp/Dn, F5-F12)
            SpecialKeyToken special => SpecialKeyTokenToKeyEvent(special),
            
            // Arrow keys with modifiers (Shift+Arrow, Ctrl+Arrow, etc.)
            ArrowKeyToken arrow => ArrowKeyTokenToKeyEvent(arrow),
            
            // Cursor movement (arrow keys in normal mode, interpreted as input)
            CursorMoveToken move => CursorMoveTokenToKeyEvent(move),
            
            // Cursor position with (1,1) = Home key when used as input
            // Cursor position with (1,N) where N >= 2 = Home with xterm modifier code N
            // (xterm sends CSI 1;{mod}H for modified Home, which tokenizes as CursorPosition(1,mod))
            CursorPositionToken { Row: 1, Column: 1 } => new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None),
            CursorPositionToken { Row: 1 } cp when cp.Column >= 2 => new Hex1bKeyEvent(Hex1bKey.Home, '\0', DecodeXtermModifiers(cp.Column)),
            
            // Backtab (Shift+Tab) - CSI Z
            BackTabToken => new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.Shift),
            
            // Text (printable characters, emoji, etc.)
            TextToken text => TextTokenToEvent(text),
            
            // Control characters (Enter, Tab, etc.)
            ControlCharacterToken ctrl => ControlCharToKeyEvent(ctrl),

            // Kitty graphics responses are terminal protocol traffic, not user input.
            KgpToken => null,
            
            // Unrecognized sequences may contain Alt+key combinations or bare Escape
            UnrecognizedSequenceToken unrec => UnrecognizedToKeyEvent(unrec),
            
            // Other tokens don't represent user input
            _ => null
        };
    }
    
    private static Hex1bKeyEvent? Ss3TokenToKeyEvent(Ss3Token token)
    {
        var key = token.Character switch
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
        
        return key == Hex1bKey.None ? null : new Hex1bKeyEvent(key, '\0', Hex1bModifiers.None);
    }
    
    private static Hex1bKeyEvent? SpecialKeyTokenToKeyEvent(SpecialKeyToken token)
    {
        var key = token.KeyCode switch
        {
            1 => Hex1bKey.Home,
            2 => Hex1bKey.Insert,
            3 => Hex1bKey.Delete,
            4 => Hex1bKey.End,
            5 => Hex1bKey.PageUp,
            6 => Hex1bKey.PageDown,
            7 => Hex1bKey.Home,   // rxvt
            8 => Hex1bKey.End,    // rxvt
            11 => Hex1bKey.F1,    // vt
            12 => Hex1bKey.F2,    // vt
            13 => Hex1bKey.F3,    // vt
            14 => Hex1bKey.F4,    // vt
            15 => Hex1bKey.F5,
            17 => Hex1bKey.F6,
            18 => Hex1bKey.F7,
            19 => Hex1bKey.F8,
            20 => Hex1bKey.F9,
            21 => Hex1bKey.F10,
            23 => Hex1bKey.F11,
            24 => Hex1bKey.F12,
            _ => Hex1bKey.None
        };
        
        if (key == Hex1bKey.None)
            return null;
        
        // Decode modifiers from the modifier code
        var modifiers = Hex1bModifiers.None;
        if (token.Modifiers >= 2)
        {
            var modifierBits = token.Modifiers - 1;
            if ((modifierBits & 1) != 0) modifiers |= Hex1bModifiers.Shift;
            if ((modifierBits & 2) != 0) modifiers |= Hex1bModifiers.Alt;
            if ((modifierBits & 4) != 0) modifiers |= Hex1bModifiers.Control;
        }
        
        return new Hex1bKeyEvent(key, '\0', modifiers);
    }
    
    private static Hex1bKeyEvent? ArrowKeyTokenToKeyEvent(ArrowKeyToken token)
    {
        // Arrow keys with modifiers (Shift+Arrow, Ctrl+Arrow, etc.)
        var key = token.Direction switch
        {
            CursorMoveDirection.Up => Hex1bKey.UpArrow,
            CursorMoveDirection.Down => Hex1bKey.DownArrow,
            CursorMoveDirection.Forward => Hex1bKey.RightArrow,
            CursorMoveDirection.Back => Hex1bKey.LeftArrow,
            _ => Hex1bKey.None
        };
        
        if (key == Hex1bKey.None)
            return null;
        
        // Decode modifier bits: modifier code - 1 gives the bit pattern
        // Bit 0 = Shift, Bit 1 = Alt, Bit 2 = Ctrl
        var modifiers = Hex1bModifiers.None;
        if (token.Modifiers >= 2)
        {
            var modifierBits = token.Modifiers - 1;
            if ((modifierBits & 1) != 0) modifiers |= Hex1bModifiers.Shift;
            if ((modifierBits & 2) != 0) modifiers |= Hex1bModifiers.Alt;
            if ((modifierBits & 4) != 0) modifiers |= Hex1bModifiers.Control;
        }
        
        return new Hex1bKeyEvent(key, '\0', modifiers);
    }
    
    private static Hex1bKeyEvent? CursorMoveTokenToKeyEvent(CursorMoveToken token)
    {
        // When received as input (not output), cursor move tokens represent arrow keys
        // Note: PreviousLine with count 1 = End key (ESC [ F)
        var key = token.Direction switch
        {
            CursorMoveDirection.Up => Hex1bKey.UpArrow,
            CursorMoveDirection.Down => Hex1bKey.DownArrow,
            CursorMoveDirection.Forward => Hex1bKey.RightArrow,
            CursorMoveDirection.Back => Hex1bKey.LeftArrow,
            CursorMoveDirection.PreviousLine when token.Count == 1 => Hex1bKey.End,
            _ => Hex1bKey.None
        };
        
        return key == Hex1bKey.None ? null : new Hex1bKeyEvent(key, '\0', Hex1bModifiers.None);
    }
    
    private static Hex1bEvent? TextTokenToEvent(TextToken token)
    {
        var text = token.Text;
        if (string.IsNullOrEmpty(text))
            return null;
        
        // Single character - try to parse as a key
        if (text.Length == 1)
        {
            return ParseKeyInput(text[0]);
        }
        
        // Multi-character (emoji, surrogate pairs, etc.) - emit as text event
        return Hex1bKeyEvent.FromText(text);
    }
    
    private static Hex1bKeyEvent? ControlCharToKeyEvent(ControlCharacterToken token)
    {
        return token.Character switch
        {
            '\r' or '\n' => new Hex1bKeyEvent(Hex1bKey.Enter, token.Character, Hex1bModifiers.None),
            '\t' => new Hex1bKeyEvent(Hex1bKey.Tab, token.Character, Hex1bModifiers.None),
            '\x7f' or '\b' => new Hex1bKeyEvent(Hex1bKey.Backspace, token.Character, Hex1bModifiers.None),
            _ => null
        };
    }
    
    private static Hex1bKeyEvent? UnrecognizedToKeyEvent(UnrecognizedSequenceToken token)
    {
        var seq = token.Sequence;
        
        // Bare Escape
        if (seq == "\x1b")
        {
            return new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None);
        }
        
        // Alt+key combination: ESC followed by a character
        if (seq.Length == 2 && seq[0] == '\x1b')
        {
            var c = seq[1];
            
            // Alt+letter (lowercase)
            if (c >= 'a' && c <= 'z')
            {
                return new Hex1bKeyEvent(
                    KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'a'))), c, Hex1bModifiers.Alt);
            }
            
            // Alt+letter (uppercase = Alt+Shift)
            if (c >= 'A' && c <= 'Z')
            {
                return new Hex1bKeyEvent(
                    KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'A'))), c, Hex1bModifiers.Alt | Hex1bModifiers.Shift);
            }
            
            // Alt+number
            if (c >= '0' && c <= '9')
            {
                return new Hex1bKeyEvent(
                    KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.D0 + (c - '0'))), c, Hex1bModifiers.Alt);
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Converts a Hex1b input event to ANSI tokens for serialization.
    /// This is the inverse of TokenToEvent.
    /// </summary>
    private static IReadOnlyList<AnsiToken> EventToTokens(Hex1bEvent evt)
    {
        var token = EventToToken(evt);
        return token != null ? [token] : [];
    }
    
    /// <summary>
    /// Converts a Hex1b input event to a single ANSI token.
    /// Returns null if the event cannot be represented as a token.
    /// </summary>
    private static AnsiToken? EventToToken(Hex1bEvent evt)
    {
        return evt switch
        {
            Hex1bMouseEvent mouse => MouseEventToToken(mouse),
            Hex1bKeyEvent key => KeyEventToToken(key),
            _ => null
        };
    }
    
    private static SgrMouseToken MouseEventToToken(Hex1bMouseEvent evt)
    {
        // Encode button and modifiers into raw button code
        int rawButton = evt.Button switch
        {
            MouseButton.Left => 0,
            MouseButton.Middle => 1,
            MouseButton.Right => 2,
            MouseButton.None when evt.Action == MouseAction.Move => 35, // Motion with no button
            MouseButton.ScrollUp => 64,
            MouseButton.ScrollDown => 65,
            _ => 0
        };
        
        // Add modifier bits
        if (evt.Modifiers.HasFlag(Hex1bModifiers.Shift)) rawButton |= 4;
        if (evt.Modifiers.HasFlag(Hex1bModifiers.Alt)) rawButton |= 8;
        if (evt.Modifiers.HasFlag(Hex1bModifiers.Control)) rawButton |= 16;
        
        // Add motion bit for drag
        if (evt.Action == MouseAction.Move || evt.Action == MouseAction.Drag) rawButton |= 32;
        
        return new SgrMouseToken(evt.Button, evt.Action, evt.X, evt.Y, evt.Modifiers, rawButton);
    }
    
    private static AnsiToken? KeyEventToToken(Hex1bKeyEvent evt)
    {
        // Check for Ctrl+letter combinations (emit as control character)
        // Ctrl+A = 0x01, Ctrl+B = 0x02, ..., Ctrl+Z = 0x1A
        if (evt.Modifiers.HasFlag(Hex1bModifiers.Control) && !evt.Modifiers.HasFlag(Hex1bModifiers.Alt))
        {
            var ctrlChar = GetControlCharacter(evt.Key);
            if (ctrlChar != '\0')
            {
                return new ControlCharacterToken(ctrlChar);
            }
        }
        
        // Check if it's an Alt+key combination (emit as unrecognized ESC+char sequence)
        if (evt.Modifiers.HasFlag(Hex1bModifiers.Alt) && !evt.Modifiers.HasFlag(Hex1bModifiers.Control))
        {
            var c = evt.Text;
            if (!string.IsNullOrEmpty(c) && c.Length == 1)
            {
                return new UnrecognizedSequenceToken($"\x1b{c}");
            }
        }
        
        // Check for special keys that use SS3 sequences (F1-F4)
        var ss3Char = evt.Key switch
        {
            Hex1bKey.F1 => 'P',
            Hex1bKey.F2 => 'Q',
            Hex1bKey.F3 => 'R',
            Hex1bKey.F4 => 'S',
            _ => '\0'
        };
        if (ss3Char != '\0' && evt.Modifiers == Hex1bModifiers.None)
        {
            return new Ss3Token(ss3Char);
        }
        
        // Check for special keys that use CSI ~ sequences
        var specialCode = evt.Key switch
        {
            Hex1bKey.Insert => 2,
            Hex1bKey.Delete => 3,
            Hex1bKey.PageUp => 5,
            Hex1bKey.PageDown => 6,
            Hex1bKey.F5 => 15,
            Hex1bKey.F6 => 17,
            Hex1bKey.F7 => 18,
            Hex1bKey.F8 => 19,
            Hex1bKey.F9 => 20,
            Hex1bKey.F10 => 21,
            Hex1bKey.F11 => 23,
            Hex1bKey.F12 => 24,
            _ => 0
        };
        if (specialCode != 0)
        {
            var modCode = EncodeModifiers(evt.Modifiers);
            return new SpecialKeyToken(specialCode, modCode);
        }
        
        // Arrow keys and Home/End use CSI sequences
        var arrowDir = evt.Key switch
        {
            Hex1bKey.UpArrow => CursorMoveDirection.Up,
            Hex1bKey.DownArrow => CursorMoveDirection.Down,
            Hex1bKey.RightArrow => CursorMoveDirection.Forward,
            Hex1bKey.LeftArrow => CursorMoveDirection.Back,
            _ => (CursorMoveDirection?)null
        };
        if (arrowDir.HasValue)
        {
            if (evt.Modifiers == Hex1bModifiers.None)
                return new CursorMoveToken(arrowDir.Value, 1);
            return new ArrowKeyToken(arrowDir.Value, EncodeModifiers(evt.Modifiers));
        }
        
        // Home/End
        if (evt.Key == Hex1bKey.Home)
        {
            if (evt.Modifiers == Hex1bModifiers.None) return new Ss3Token('H');
            return new SpecialKeyToken(1, EncodeModifiers(evt.Modifiers));
        }
        if (evt.Key == Hex1bKey.End)
        {
            if (evt.Modifiers == Hex1bModifiers.None) return new Ss3Token('F');
            return new SpecialKeyToken(4, EncodeModifiers(evt.Modifiers));
        }
        
        // Control characters
        if (evt.Key == Hex1bKey.Enter) return new ControlCharacterToken('\r');
        if (evt.Key == Hex1bKey.Tab) return new ControlCharacterToken('\t');
        if (evt.Key == Hex1bKey.Escape) return new UnrecognizedSequenceToken("\x1b");
        if (evt.Key == Hex1bKey.Backspace)
        {
            // Preserve the original backspace character from the host terminal.
            // Windows sends 0x08 (BS), Unix typically sends 0x7F (DEL).
            // Child processes expect the same encoding their terminal would normally use.
            var c = (!string.IsNullOrEmpty(evt.Text) && (evt.Text[0] == '\b' || evt.Text[0] == '\x7f'))
                ? evt.Text[0]
                : '\x7f'; // Default to DEL for programmatic events without original text
            return new ControlCharacterToken(c);
        }
        
        // Regular text
        if (!string.IsNullOrEmpty(evt.Text))
        {
            return new TextToken(evt.Text);
        }
        
        return null;
    }
    
    /// <summary>
    /// Decodes xterm modifier code to Hex1bModifiers.
    /// Modifier code format: bits + 1, where bit 0=Shift, bit 1=Alt, bit 2=Ctrl.
    /// </summary>
    private static Hex1bModifiers DecodeXtermModifiers(int modifierCode)
    {
        if (modifierCode < 2) return Hex1bModifiers.None;
        var bits = modifierCode - 1;
        var modifiers = Hex1bModifiers.None;
        if ((bits & 1) != 0) modifiers |= Hex1bModifiers.Shift;
        if ((bits & 2) != 0) modifiers |= Hex1bModifiers.Alt;
        if ((bits & 4) != 0) modifiers |= Hex1bModifiers.Control;
        return modifiers;
    }
    
    private static int EncodeModifiers(Hex1bModifiers modifiers)
    {
        if (modifiers == Hex1bModifiers.None)
            return 1; // No modifiers = 1 in xterm encoding
        
        int bits = 0;
        if (modifiers.HasFlag(Hex1bModifiers.Shift)) bits |= 1;
        if (modifiers.HasFlag(Hex1bModifiers.Alt)) bits |= 2;
        if (modifiers.HasFlag(Hex1bModifiers.Control)) bits |= 4;
        
        return bits + 1; // xterm modifier encoding: bits + 1
    }
    
    /// <summary>
    /// Gets the control character for a Ctrl+key combination.
    /// Ctrl+A = 0x01, Ctrl+B = 0x02, ..., Ctrl+Z = 0x1A
    /// </summary>
    private static char GetControlCharacter(Hex1bKey key)
    {
        // Map letter keys to control characters
        return key switch
        {
            Hex1bKey.A => '\x01',
            Hex1bKey.B => '\x02',
            Hex1bKey.C => '\x03',
            Hex1bKey.D => '\x04',
            Hex1bKey.E => '\x05',
            Hex1bKey.F => '\x06',
            Hex1bKey.G => '\x07',
            Hex1bKey.H => '\x08',
            Hex1bKey.I => '\x09', // Tab
            Hex1bKey.J => '\x0A', // LF
            Hex1bKey.K => '\x0B',
            Hex1bKey.L => '\x0C',
            Hex1bKey.M => '\x0D', // CR
            Hex1bKey.N => '\x0E',
            Hex1bKey.O => '\x0F',
            Hex1bKey.P => '\x10',
            Hex1bKey.Q => '\x11',
            Hex1bKey.R => '\x12',
            Hex1bKey.S => '\x13',
            Hex1bKey.T => '\x14',
            Hex1bKey.U => '\x15',
            Hex1bKey.V => '\x16',
            Hex1bKey.W => '\x17',
            Hex1bKey.X => '\x18',
            Hex1bKey.Y => '\x19',
            Hex1bKey.Z => '\x1A',
            _ => '\0'
        };
    }
    
    private async Task PumpWorkloadOutputAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                ReadOnlyMemory<byte> data;
                IReadOnlyList<AnsiToken>? preTokenizedTokens = null;

                if (_workload is IHex1bTerminalTokenWorkloadAdapter tokenWorkload)
                {
                    var item = await tokenWorkload.ReadOutputItemAsync(ct);
                    data = item.Bytes;
                    preTokenizedTokens = item.Tokens;
                }
                else
                {
                    data = await _workload.ReadOutputAsync(ct);
                }
                
                if (data.IsEmpty)
                {
                    // Channel empty - this is a frame boundary
                    await NotifyWorkloadFiltersFrameCompleteAsync();
                    
                    // Small delay to prevent busy-waiting in headless mode
                    await Task.Delay(10, ct);
                    continue;
                }
                
                string? completeText = null;
                IReadOnlyList<AnsiToken> tokens;

                if (preTokenizedTokens != null)
                {
                    tokens = NormalizePreTokenizedTokens(preTokenizedTokens);
                }
                else
                {
                    // Decode UTF-8 using the stateful decoder which handles incomplete sequences
                    // across read boundaries (e.g., braille characters split across reads)
                    var charCount = _utf8Decoder.GetCharCount(data.Span, flush: false);
                    var chars = new char[charCount];
                    _utf8Decoder.GetChars(data.Span, chars, flush: false);
                    var decodedText = new string(chars);
                    
                    // Prepend any buffered incomplete escape sequence from previous read
                    var text = _incompleteSequenceBuffer + decodedText;
                    _incompleteSequenceBuffer = "";
                    
                    // Check if text ends with an incomplete escape sequence and buffer it
                    var extracted = ExtractIncompleteEscapeSequence(text);
                    completeText = extracted.completeText;
                    _incompleteSequenceBuffer = extracted.incompleteSequence;
                    
                    if (string.IsNullOrEmpty(completeText))
                        continue; // All content is incomplete, wait for more data
                    
                    // Tokenize once, use for all processing
                    tokens = AnsiTokenizer.Tokenize(completeText);
                }
                
                _metrics.TerminalOutputTokens.Record(tokens.Count);
                
                // FAST PATH: If no filters are active AND presentation doesn't need cell impacts,
                // apply tokens to buffer and forward bytes directly.
                // This is crucial for programs like tmux that are sensitive to output timing
                if (_workloadFilters.Count == 0 && _presentationFilters.Count == 0 
                    && _presentation is not ICellImpactAwarePresentationAdapter)
                {
                    // Still apply tokens to internal buffer so CreateSnapshot() works
                    ApplyTokens(tokens);
                    
                    // Forward raw bytes to presentation if present
                    if (_presentation != null)
                    {
                        await _presentation.WriteOutputAsync(data, ct);
                        _metrics.TerminalOutputBytes.Record(data.Length);
                    }
                    continue;
                }
                
                // Notify workload filters with tokens
                await NotifyWorkloadFiltersOutputAsync(tokens);
                
                // Apply tokens to our internal buffer and collect cell impacts
                var appliedTokens = ApplyTokensWithImpacts(tokens);
                
                // Forward to presentation if present
                if (_presentation != null)
                {
                    // Check if presentation adapter wants cell impacts directly
                    if (_presentation is ICellImpactAwarePresentationAdapter impactAware)
                    {
                        // Run through presentation filters first if any
                        if (_presentationFilters.Count > 0)
                        {
                            await NotifyPresentationFiltersOutputAsync(appliedTokens);
                        }
                        // Send applied tokens with impacts directly to the adapter
                        await impactAware.WriteOutputWithImpactsAsync(appliedTokens, ct);
                    }
                    // If there are no presentation filters, pass through original bytes directly
                    // to preserve exact escape sequence syntax
                    else if (_presentationFilters.Count == 0)
                    {
                        if (completeText != null)
                        {
                            var originalBytes = Encoding.UTF8.GetBytes(completeText);
                            await _presentation.WriteOutputAsync(originalBytes, ct);
                            _metrics.TerminalOutputBytes.Record(originalBytes.Length);
                        }
                        else
                        {
                            await _presentation.WriteOutputAsync(data, ct);
                            _metrics.TerminalOutputBytes.Record(data.Length);
                        }
                    }
                    else
                    {
                        // Pass through presentation filters, serialize and send
                        var filteredTokens = await NotifyPresentationFiltersOutputAsync(appliedTokens);
                        var filteredBytes = Tokens.AnsiTokenUtf8Serializer.Serialize(filteredTokens);
                        await _presentation.WriteOutputAsync(filteredBytes, ct);
                        _metrics.TerminalOutputBytes.Record(filteredBytes.Length);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            ReportPumpFault("workload output pump", ex);
        }
    }

    private void ReportPumpFault(string pumpName, Exception error)
    {
        if (error is OperationCanceledException && _disposeCts.IsCancellationRequested)
        {
            return;
        }

        TracePump($"{pumpName} faulted: {error}");
        _pumpFaultTcs.TrySetResult((pumpName, error));
    }

    private static void TracePump(string message)
    {
        var tracePath = Environment.GetEnvironmentVariable("HEX1B_TERMINAL_PUMP_TRACE_FILE");
        if (string.IsNullOrWhiteSpace(tracePath))
        {
            return;
        }

        try
        {
            File.AppendAllText(tracePath, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static IReadOnlyList<AnsiToken> NormalizePreTokenizedTokens(IReadOnlyList<AnsiToken> tokens)
    {
        // When Hex1bApp provides pre-tokenized output, DCS sequences (Sixel) are currently
        // represented as raw UnrecognizedSequenceToken payloads (ESC P ... ST). The terminal
        // processing path expects them as DcsToken to track sixels and update internal state.
        List<AnsiToken>? normalized = null;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token is UnrecognizedSequenceToken unrec)
            {
                AnsiToken? replacement = null;

                if (TryExtractDcsPayload(unrec.Sequence, out var dcsPayload))
                {
                    replacement = new DcsToken(dcsPayload);
                }
                else if (TryExtractKgpToken(unrec.Sequence, out var kgpControlData, out var kgpPayload))
                {
                    replacement = new KgpToken(kgpControlData, kgpPayload);
                }

                if (replacement != null)
                {
                    normalized ??= new List<AnsiToken>(tokens.Count);
                    if (normalized.Count == 0)
                    {
                        for (int j = 0; j < i; j++)
                        {
                            normalized.Add(tokens[j]);
                        }
                    }

                    normalized.Add(replacement);
                    continue;
                }
            }

            if (normalized != null)
            {
                normalized.Add(token);
            }
        }

        return normalized ?? tokens;
    }

    private static bool TryExtractDcsPayload(string sequence, out string payload)
    {
        payload = "";

        int dataStart;
        if (sequence.Length >= 2 && sequence[0] == '\x1b' && sequence[1] == 'P')
        {
            dataStart = 2;
        }
        else if (sequence.Length >= 1 && sequence[0] == '\x90')
        {
            dataStart = 1;
        }
        else
        {
            return false;
        }

        // Find ST: ESC \ or 0x9C
        int dataEnd = -1;
        for (int i = dataStart; i < sequence.Length; i++)
        {
            if (i + 1 < sequence.Length && sequence[i] == '\x1b' && sequence[i + 1] == '\\')
            {
                dataEnd = i;
                break;
            }

            if (sequence[i] == '\x9c')
            {
                dataEnd = i;
                break;
            }
        }

        if (dataEnd < 0)
            return false;

        payload = sequence.Substring(dataStart, dataEnd - dataStart);
        return true;
    }

    /// <summary>
    /// Extracts KGP control data and payload from an APC sequence string (ESC _ G ... ST).
    /// </summary>
    private static bool TryExtractKgpToken(string sequence, out string controlData, out string payload)
    {
        controlData = "";
        payload = "";

        // Check for APC start: ESC _ (0x1b 0x5f)
        int dataStart;
        if (sequence.Length >= 2 && sequence[0] == '\x1b' && sequence[1] == '_')
        {
            dataStart = 2;
        }
        else if (sequence.Length >= 1 && sequence[0] == '\x9f')
        {
            dataStart = 1;
        }
        else
        {
            return false;
        }

        // Must start with 'G' to be a KGP sequence
        if (dataStart >= sequence.Length || sequence[dataStart] != 'G')
            return false;

        dataStart++; // Skip 'G'

        // Find ST: ESC \ or 0x9C
        int dataEnd = -1;
        for (int i = dataStart; i < sequence.Length; i++)
        {
            if (i + 1 < sequence.Length && sequence[i] == '\x1b' && sequence[i + 1] == '\\')
            {
                dataEnd = i;
                break;
            }
            if (sequence[i] == '\x9c')
            {
                dataEnd = i;
                break;
            }
        }

        if (dataEnd < 0)
            return false;

        var content = sequence.Substring(dataStart, dataEnd - dataStart);

        // Split on ';' - control data is before, payload is after
        var semicolonIndex = content.IndexOf(';');
        if (semicolonIndex < 0)
        {
            controlData = content;
            payload = "";
        }
        else
        {
            controlData = content.Substring(0, semicolonIndex);
            payload = content.Substring(semicolonIndex + 1);
        }

        return true;
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
                    // ESC followed by a printable character = Alt+key combination
                    // For example: ESC f = Alt+F, ESC 1 = Alt+1
                    var nextChar = message[i + 1];
                    var altKeyEvent = ParseAltKeyInput(nextChar);
                    if (altKeyEvent != null)
                    {
                        await workload.WriteInputEventAsync(altKeyEvent, ct);
                        i += 2; // Consume both ESC and the following character
                    }
                    else
                    {
                        // Unknown escape sequence, just emit Escape
                        await workload.WriteInputEventAsync(
                            new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None), ct);
                        i++;
                    }
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
            return _inAlternateScreen;
        }
    }

    /// <summary>Gets whether the terminal has a pending wrap (for testing).</summary>
    internal bool PendingWrap => _pendingWrap;

    /// <summary>
    /// The scrollback buffer, if one was configured via <see cref="Hex1bTerminalOptions.ScrollbackCapacity"/>.
    /// </summary>
    internal ScrollbackBuffer? Scrollback => _scrollbackBuffer;

    /// <summary>
    /// Enters alternate screen mode (for testing purposes).
    /// In headless mode, this just sets the flag and clears the buffer.
    /// </summary>
    internal void EnterAlternateScreen()
    {
        ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h"));
    }

    /// <summary>
    /// Exits alternate screen mode (for testing purposes).
    /// In headless mode, this just clears the flag.
    /// </summary>
    internal void ExitAlternateScreen()
    {
        ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
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
        lock (_bufferLock)
        {
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
    }

    /// <summary>
    /// Gets the text content of the screen buffer as a string, with lines separated by newlines.
    /// Automatically flushes pending output before returning.
    /// </summary>
    internal string GetScreenText()
    {
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
            // Direct event dispatch for Hex1bApp workloads
            appWorkload.TryWriteInputEvent(evt);
        }
        else
        {
            // Serialize event to ANSI bytes for child process workloads
            var tokens = EventToTokens(evt);
            if (tokens.Count > 0)
            {
                var serialized = AnsiTokenSerializer.Serialize(tokens);
                var bytes = Encoding.UTF8.GetBytes(serialized);
                // Fire and forget - synchronous API can't wait for async write
                _ = WriteWorkloadInputAsync(bytes, CancellationToken.None);
            }
        }
    }
    
    /// <summary>
    /// Sends an input event to the workload asynchronously.
    /// For Hex1bApp workloads, this dispatches the event directly.
    /// For child process workloads, this serializes the event to ANSI bytes.
    /// </summary>
    /// <param name="evt">The event to send.</param>
    /// <param name="ct">Cancellation token.</param>
    internal async Task SendEventAsync(Hex1bEvent evt, CancellationToken ct = default)
    {
        if (_workload is Hex1bAppWorkloadAdapter appWorkload)
        {
            // Direct event dispatch for Hex1bApp workloads
            await appWorkload.WriteInputEventAsync(evt, ct);
        }
        else
        {
            // Serialize event to ANSI bytes for child process workloads
            var tokens = EventToTokens(evt);
            if (tokens.Count > 0)
            {
                var serialized = AnsiTokenSerializer.Serialize(tokens);
                var bytes = Encoding.UTF8.GetBytes(serialized);
                await WriteWorkloadInputAsync(bytes, ct);
                
                // Also notify filters of the input
                await NotifyWorkloadFiltersInputAsync(tokens);
            }
        }
    }

    /// <summary>
    /// Sends raw input bytes to the workload.
    /// </summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SendInputAsync(byte[] bytes, CancellationToken ct = default)
    {
        await WriteWorkloadInputAsync(bytes, ct);
    }

    private async ValueTask WriteWorkloadInputAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct)
    {
        await _workloadInputWriteLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _workload.WriteInputAsync(bytes, ct).ConfigureAwait(false);
        }
        finally
        {
            _workloadInputWriteLock.Release();
        }
    }

    /// <summary>
    /// Creates an immutable snapshot of the current terminal state.
    /// Useful for assertions and wait conditions in tests.
    /// Automatically flushes pending output before creating the snapshot.
    /// </summary>
    public Hex1bTerminalSnapshot CreateSnapshot()
    {
        return new Hex1bTerminalSnapshot(this);
    }

    /// <summary>
    /// Creates an immutable snapshot of the current terminal state, optionally including
    /// lines from the scrollback buffer.
    /// </summary>
    /// <param name="scrollbackLines">Number of scrollback lines to include above the visible area. Zero means no scrollback.</param>
    /// <param name="scrollbackWidth">Controls how scrollback line widths are adapted in the snapshot.</param>
    /// <param name="voidCell">The cell used to fill void regions where the snapshot is wider than a row's original content. Defaults to <see cref="TerminalCell.Empty"/> (a space with no attributes).</param>
    /// <returns>A snapshot with scrollback lines prepended above the visible content.</returns>
    public Hex1bTerminalSnapshot CreateSnapshot(int scrollbackLines, ScrollbackWidth scrollbackWidth = ScrollbackWidth.CurrentTerminal, TerminalCell? voidCell = null)
    {
        return new Hex1bTerminalSnapshot(this, scrollbackLines, scrollbackWidth, voidCell ?? TerminalCell.Empty);
    }

    /// <summary>
    /// Resizes the terminal, preserving content where possible.
    /// If the presentation adapter implements <see cref="ITerminalReflowProvider"/>,
    /// soft-wrapped lines are re-wrapped to the new width.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        lock (_bufferLock)
        {
            // Check if the presentation adapter supports reflow and has it enabled
            if (_presentation is ITerminalReflowProvider { ReflowEnabled: true } reflowProvider)
            {
                ResizeWithReflow(newWidth, newHeight, reflowProvider);
            }
            else
            {
                ResizeWithCrop(newWidth, newHeight);
            }
            
            // Reset margins on resize - this matches xterm behavior
            _marginRight = newWidth - 1;
            if (_marginLeft > _marginRight)
                _marginLeft = 0;
            
            // Reset scroll region on resize - this matches xterm behavior
            // The scroll region should cover the full new screen height
            _scrollTop = 0;
            _scrollBottom = newHeight - 1;
            
            _pendingWrap = false;
        }
    }

    private void ResizeWithReflow(int newWidth, int newHeight, ITerminalReflowProvider reflowProvider)
    {
        // Build the ReflowContext from current state
        var screenRows = new TerminalCell[_height][];
        for (int y = 0; y < _height; y++)
        {
            var row = new TerminalCell[_width];
            for (int x = 0; x < _width; x++)
                row[x] = _screenBuffer[y, x];
            screenRows[y] = row;
        }

        // Get scrollback rows
        var scrollbackData = _scrollbackBuffer?.GetLines(_scrollbackBuffer.Count) ?? [];
        var scrollbackRows = new ReflowScrollbackRow[scrollbackData.Length];
        for (int i = 0; i < scrollbackData.Length; i++)
        {
            scrollbackRows[i] = new ReflowScrollbackRow(scrollbackData[i].Cells, scrollbackData[i].OriginalWidth);
        }

        var context = new ReflowContext(
            screenRows, scrollbackRows,
            _width, _height, newWidth, newHeight,
            _cursorX, _cursorY, _inAlternateScreen,
            _cursorSaved ? _savedCursorX : null,
            _cursorSaved ? _savedCursorY : null);

        // Call the adapter's reflow implementation
        var result = reflowProvider.Reflow(context);

        // Release tracked objects from old screen buffer (all cells)
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _screenBuffer[y, x].TrackedSixel?.Release();
                _screenBuffer[y, x].TrackedHyperlink?.Release();
            }
        }

        // Apply the reflowed screen buffer
        var newBuffer = new TerminalCell[newHeight, newWidth];
        for (int y = 0; y < newHeight; y++)
        {
            if (y < result.ScreenRows.Length)
            {
                for (int x = 0; x < newWidth && x < result.ScreenRows[y].Length; x++)
                {
                    newBuffer[y, x] = result.ScreenRows[y][x];
                    // AddRef tracked objects in the new buffer
                    newBuffer[y, x].TrackedSixel?.AddRef();
                    newBuffer[y, x].TrackedHyperlink?.AddRef();
                }
                for (int x = result.ScreenRows[y].Length; x < newWidth; x++)
                    newBuffer[y, x] = TerminalCell.Empty;
            }
            else
            {
                for (int x = 0; x < newWidth; x++)
                    newBuffer[y, x] = TerminalCell.Empty;
            }
        }

        // Apply reflowed scrollback if scrollback is enabled
        if (_scrollbackBuffer is not null)
        {
            _scrollbackBuffer.Clear();
            foreach (var sbRow in result.ScrollbackRows)
            {
                _scrollbackBuffer.Push(sbRow.Cells, sbRow.OriginalWidth, _timeProvider.GetUtcNow());
            }
        }

        _screenBuffer = newBuffer;
        _width = newWidth;
        _height = newHeight;
        _cursorX = Math.Clamp(result.CursorX, 0, newWidth - 1);
        _cursorY = Math.Clamp(result.CursorY, 0, newHeight - 1);

        // Update saved cursor if the reflow strategy reflowed it
        if (result.NewSavedCursorX.HasValue && result.NewSavedCursorY.HasValue)
        {
            _savedCursorX = Math.Clamp(result.NewSavedCursorX.Value, 0, newWidth - 1);
            _savedCursorY = Math.Clamp(result.NewSavedCursorY.Value, 0, newHeight - 1);
        }
    }

    private void ResizeWithCrop(int newWidth, int newHeight)
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
    /// <param name="y">The row position (0-based).</param>
    /// <param name="x">The column position (0-based).</param>
    /// <param name="newCell">The new cell value.</param>
    /// <param name="impacts">Optional list to record the cell impact for delta tracking.</param>
    private void SetCell(int y, int x, TerminalCell newCell, List<CellImpact>? impacts = null)
    {
        ref var oldCell = ref _screenBuffer[y, x];
        
        // Release old Sixel data reference
        oldCell.TrackedSixel?.Release();
        
        // Release old hyperlink data reference
        oldCell.TrackedHyperlink?.Release();
        
        // Note: new cell's tracked object already has a reference from GetOrCreateSixel/GetOrCreateHyperlink
        // or was explicitly AddRef'd by the caller before creating the cell
        // No need to AddRef here - the caller is responsible for getting the object
        // with the correct refcount
        
        oldCell = newCell;
        
        // Record the impact if tracking is enabled
        impacts?.Add(new CellImpact(x, y, newCell));
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
    /// Gets the number of tracked hyperlink objects in the store.
    /// Useful for testing to verify cleanup behavior.
    /// </summary>
    internal int TrackedHyperlinkCount => _trackedObjects.HyperlinkCount;

    /// <summary>
    /// Gets the Sixel data at the specified cell position, if any.
    /// Returns null if the cell doesn't contain Sixel data or is a continuation cell.
    /// </summary>
    internal SixelData? GetSixelDataAt(int x, int y)
    {
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
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return null;
        return _screenBuffer[y, x].TrackedSixel;
    }

    /// <summary>
    /// Checks if any cell in the screen buffer contains Sixel data.
    /// </summary>
    internal bool ContainsSixelData()
    {
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

    /// <summary>
    /// Gets the hyperlink data at the specified cell position, if any.
    /// Returns null if the cell doesn't contain hyperlink data.
    /// </summary>
    internal HyperlinkData? GetHyperlinkDataAt(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return null;
        return _screenBuffer[y, x].HyperlinkData;
    }

    /// <summary>
    /// Gets the tracked hyperlink object at the specified cell position, if any.
    /// Returns null if the cell doesn't contain hyperlink data.
    /// Useful for testing reference count behavior.
    /// </summary>
    internal TrackedObject<HyperlinkData>? GetTrackedHyperlinkAt(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return null;
        return _screenBuffer[y, x].TrackedHyperlink;
    }

    /// <summary>
    /// Checks if any cell in the screen buffer contains hyperlink data.
    /// </summary>
    internal bool ContainsHyperlinkData()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (_screenBuffer[y, x].TrackedHyperlink is not null)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Creates a blank cell for erase operations, preserving the current background color per ECMA-48.
    /// </summary>
    private TerminalCell CreateEraseCell()
    {
        if (_currentBackground is null)
            return TerminalCell.Empty;
        return new TerminalCell(" ", null, _currentBackground, CellAttributes.None, 0, default);
    }

    private void ClearBuffer(bool respectProtection = false, List<CellImpact>? impacts = null)
    {
        var eraseCell = CreateEraseCell();
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (respectProtection && IsProtectedCell(y, x))
                    continue;
                SetCell(y, x, eraseCell, impacts);
            }
        }
        if (!respectProtection)
        {
            _currentForeground = null;
            _currentBackground = null;
            _currentUnderlineColor = null;
            _currentUnderlineStyle = UnderlineStyle.None;
        }
    }
    
    /// <summary>
    /// Clears the internal buffer without generating cell impacts.
    /// Used when the presentation adapter handles alternate screen natively.
    /// </summary>
    private void ClearBufferInternal()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                // Release any tracked objects
                _screenBuffer[y, x].TrackedSixel?.Release();
                _screenBuffer[y, x].TrackedHyperlink?.Release();
                _screenBuffer[y, x] = TerminalCell.Empty;
            }
        }
        _currentForeground = null;
        _currentBackground = null;
        _currentUnderlineColor = null;
        _currentUnderlineStyle = UnderlineStyle.None;
    }

    /// <summary>
    /// Applies a list of ANSI tokens to the screen buffer.
    /// </summary>
    /// <remarks>
    /// This method works with pre-tokenized input, which is useful when tokens
    /// have been processed through workload filters.
    /// </remarks>
    /// <param name="tokens">The tokens to apply.</param>
    internal void ApplyTokens(IReadOnlyList<AnsiToken> tokens)
    {
        lock (_bufferLock)
        {
            foreach (var token in tokens)
            {
                ApplyToken(token, null);
            }
        }
    }

    /// <summary>
    /// Applies a list of ANSI tokens to the screen buffer and captures the impact of each token.
    /// </summary>
    /// <remarks>
    /// This method tracks which cells were modified by each token and captures cursor movement.
    /// The returned <see cref="AppliedToken"/> list contains metadata useful for delta encoding
    /// and other presentation filters.
    /// </remarks>
    /// <param name="tokens">The tokens to apply.</param>
    /// <returns>A list of applied tokens with their cell impacts and cursor state changes.</returns>
    internal IReadOnlyList<AppliedToken> ApplyTokensWithImpacts(IReadOnlyList<AnsiToken> tokens)
    {
        lock (_bufferLock)
        {
            var result = new List<AppliedToken>(tokens.Count);
            
            foreach (var token in tokens)
            {
                int cursorXBefore = _cursorX;
                int cursorYBefore = _cursorY;
                
                var impacts = new List<CellImpact>();
                ApplyToken(token, impacts);
                
                result.Add(new AppliedToken(
                    token,
                    impacts,
                    cursorXBefore, cursorYBefore,
                    _cursorX, _cursorY));
            }
            
            return result;
        }
    }

    /// <summary>
    /// Applies a single ANSI token to the screen buffer.
    /// </summary>
    /// <param name="token">The token to apply.</param>
    /// <param name="impacts">Optional list to record cell impacts for delta tracking.</param>
    private void ApplyToken(AnsiToken token, List<CellImpact>? impacts)
    {
        switch (token)
        {
            case TextToken textToken:
                ApplyTextToken(textToken, impacts);
                break;
                
            case ControlCharacterToken controlToken:
                ApplyControlCharacter(controlToken, impacts);
                break;
                
            case SgrToken sgrToken:
                ProcessSgr(sgrToken.Parameters);
                break;
                
            case CursorPositionToken cursorToken:
                _pendingWrap = false; // Explicit cursor movement clears pending wrap
                
                // Clear SoftWrap on the current row's last cell if the adapter says to.
                // This breaks the reflow chain — absolute positioning indicates the app
                // is managing screen layout directly.
                if (_presentation is ITerminalReflowProvider { ShouldClearSoftWrapOnAbsolutePosition: true }
                    && _cursorY >= 0 && _cursorY < _height)
                {
                    ref var lastCell = ref _screenBuffer[_cursorY, _width - 1];
                    if ((lastCell.Attributes & CellAttributes.SoftWrap) != 0)
                        lastCell = lastCell with { Attributes = lastCell.Attributes & ~CellAttributes.SoftWrap };
                }
                
                if (_originMode)
                {
                    // Origin mode: positions are relative to scroll region
                    _cursorY = Math.Clamp(_scrollTop + cursorToken.Row - 1, _scrollTop, _scrollBottom);
                    // When DECLRMM is enabled, column is relative to left margin
                    if (_declrmm)
                        _cursorX = Math.Clamp(_marginLeft + cursorToken.Column - 1, _marginLeft, _marginRight);
                    else
                        _cursorX = Math.Clamp(cursorToken.Column - 1, 0, _width - 1);
                }
                else
                {
                    // Normal mode: positions are absolute
                    var requestedRow = cursorToken.Row - 1;
                    var clampedRow = Math.Clamp(requestedRow, 0, _height - 1);
                    _cursorY = clampedRow;
                    _cursorX = Math.Clamp(cursorToken.Column - 1, 0, _width - 1);
                }
                break;
                
            case ClearScreenToken clearToken:
                ApplyClearScreen(clearToken.Mode, clearToken.Selective, impacts);
                break;
                
            case ClearLineToken clearLineToken:
                ApplyClearLine(clearLineToken.Mode, clearLineToken.Selective, impacts);
                break;
                
            case PrivateModeToken privateModeToken:
                if (privateModeToken.Mode == 1049 || privateModeToken.Mode == 47 || privateModeToken.Mode == 1047)
                {
                    if (privateModeToken.Enable)
                        DoEnterAlternateScreen(impacts);
                    else
                        DoExitAlternateScreen(impacts);
                }
                else if (privateModeToken.Mode == 6)
                {
                    // DECOM - Origin Mode
                    _originMode = privateModeToken.Enable;
                    // Setting origin mode also moves cursor to home position (within scroll region if enabled)
                    _pendingWrap = false;
                    _cursorX = _marginLeft;
                    _cursorY = _originMode ? _scrollTop : 0;
                }
                else if (privateModeToken.Mode == 69)
                {
                    // DECLRMM - Left Right Margin Mode
                    _declrmm = privateModeToken.Enable;
                    if (!_declrmm)
                    {
                        // When disabled, reset margins to full screen
                        _marginLeft = 0;
                        _marginRight = _width - 1;
                    }
                }
                else if (privateModeToken.Mode == 7)
                {
                    // DECAWM - Auto-wrap Mode
                    _wraparoundMode = privateModeToken.Enable;
                }
                else if (privateModeToken.Mode == 45)
                {
                    // Reverse Wraparound Mode — cursor can wrap backwards past column 0
                    // to the end of the previous line (only if soft-wrapped)
                    _reverseWrapMode = privateModeToken.Enable;
                }
                else if (privateModeToken.Mode == 1045)
                {
                    // Extended Reverse Wraparound Mode (xterm XTREVWRAP2) — like mode 45
                    // but wraps across hard line boundaries and wraps from top to bottom
                    _reverseWrapExtendedMode = privateModeToken.Enable;
                }
                else if (privateModeToken.Mode == 2027)
                {
                    // Grapheme cluster mode — when enabled, multi-codepoint graphemes
                    // (ZWJ sequences, Devanagari ligatures) are combined into single cells.
                    // When disabled, each codepoint is treated individually.
                    _graphemeClusterMode = privateModeToken.Enable;
                }
                break;
            
            case StandardModeToken stdModeToken:
                if (stdModeToken.Mode == 4)
                {
                    // IRM - Insert/Replace Mode
                    _insertMode = stdModeToken.Enable;
                }
                else if (stdModeToken.Mode == 20)
                {
                    // LNM - Linefeed/New Line Mode
                    _newlineMode = stdModeToken.Enable;
                }
                break;
            
            case LeftRightMarginToken lrmToken:
                // DECSLRM - Set Left Right Margins
                // Only effective when DECLRMM (mode 69) is enabled
                if (_declrmm)
                {
                    // 0 means "use default" — left=1, right=cols
                    var left = lrmToken.Left <= 0 ? 1 : lrmToken.Left;
                    var right = lrmToken.Right <= 0 ? _width : lrmToken.Right;
                    
                    var newLeft = Math.Clamp(left - 1, 0, _width - 1);
                    var newRight = Math.Clamp(right - 1, newLeft, _width - 1);
                    
                    // If left >= right (after clamping), reset to full width
                    if (newLeft >= newRight)
                    {
                        _marginLeft = 0;
                        _marginRight = _width - 1;
                    }
                    else
                    {
                        _marginLeft = newLeft;
                        _marginRight = newRight;
                    }
                    
                    // DECSLRM also moves cursor to home position
                    _pendingWrap = false;
                    _cursorX = _marginLeft;
                    _cursorY = _originMode ? _scrollTop : 0;
                }
                break;
                
            case OscToken oscToken:
                ProcessOscSequence(oscToken.Command, oscToken.Parameters, oscToken.Payload);
                break;
                
            case DcsToken dcsToken:
                ProcessSixelData(dcsToken.Payload, impacts);
                break;
                
            case ScrollRegionToken scrollRegionToken:
                // Store scroll region for future scroll operations (DECSTBM)
                // Top and Bottom are 1-based; we store as 0-based
                // When bottom is 0 or omitted, default to last row
                var sTop = scrollRegionToken.Top <= 0 ? 1 : scrollRegionToken.Top;
                var sBottom = scrollRegionToken.Bottom <= 0 ? _height : scrollRegionToken.Bottom;
                
                if (sTop >= sBottom)
                {
                    // Equal or inverted margins = reset to full screen
                    _scrollTop = 0;
                    _scrollBottom = _height - 1;
                }
                else
                {
                    _scrollTop = Math.Clamp(sTop - 1, 0, _height - 1);
                    _scrollBottom = Math.Clamp(sBottom - 1, _scrollTop, _height - 1);
                }
                // DECSTBM also moves cursor to home position (1,1)
                _pendingWrap = false; // Cursor movement clears pending wrap
                _cursorX = 0;
                _cursorY = 0;
                break;
                
            case SaveCursorToken:
                _savedCursorX = _cursorX;
                _savedCursorY = _cursorY;
                _savedPendingWrap = _pendingWrap;
                _savedCursorProtected = _cursorProtected;
                _cursorSaved = true;
                break;
                
            case RestoreCursorToken:
                // Only restore if cursor was previously saved (matches GNOME Terminal behavior)
                if (_cursorSaved)
                {
                    _pendingWrap = _savedPendingWrap;
                    _cursorX = _savedCursorX;
                    _cursorY = _savedCursorY;
                    _cursorProtected = _savedCursorProtected;
                }
                break;
                
            case CursorShapeToken:
                // Cursor shape is presentation-only, no buffer state to update
                break;
                
            case CursorMoveToken moveToken:
                ApplyCursorMove(moveToken);
                break;
                
            case CursorColumnToken columnToken:
                _pendingWrap = false; // Explicit column movement clears pending wrap
                _cursorX = Math.Clamp(columnToken.Column - 1, 0, _width - 1);
                break;
            
            case CursorRowToken rowToken:
                _pendingWrap = false; // Explicit row movement clears pending wrap
                // VPA respects origin mode like CUP
                if (_originMode)
                {
                    _cursorY = Math.Clamp(_scrollTop + rowToken.Row - 1, _scrollTop, _scrollBottom);
                }
                else
                {
                    _cursorY = Math.Clamp(rowToken.Row - 1, 0, _height - 1);
                }
                break;
                
            case ScrollUpToken scrollUpToken:
                for (int i = 0; i < scrollUpToken.Count; i++)
                    ScrollUp(impacts);
                break;
                
            case ScrollDownToken scrollDownToken:
                for (int i = 0; i < scrollDownToken.Count; i++)
                    ScrollDown(impacts);
                break;
                
            case InsertLinesToken insertLinesToken:
                InsertLines(insertLinesToken.Count, impacts);
                break;
                
            case DeleteLinesToken deleteLinesToken:
                DeleteLines(deleteLinesToken.Count, impacts);
                break;
                
            case RisToken:
                // RIS (ESC c): Full terminal reset — clear screen, reset all state
                _scrollTop = 0;
                _scrollBottom = _height - 1;
                _marginLeft = 0;
                _marginRight = _width - 1;
                _declrmm = false;
                _cursorX = 0;
                _cursorY = 0;
                _pendingWrap = false;
                _originMode = false;
                _insertMode = false;
                _newlineMode = false;
                _savedCursorX = 0;
                _savedCursorY = 0;
                _savedPendingWrap = false;
                _savedCursorProtected = false;
                _cursorSaved = false;
                _cursorProtected = false;
                _protectedMode = ProtectedMode.Off;
                _currentForeground = null;
                _currentBackground = null;
                _currentAttributes = CellAttributes.None;
                _currentUnderlineColor = null;
                _currentUnderlineStyle = UnderlineStyle.None;
                _lastPrintedCell = TerminalCell.Empty;
                _hasLastPrintedCell = false;
                _lastPrintedCellX = 0;
                _lastPrintedCellY = 0;
                _lastPrintedCellWidth = 0;
                _pendingGraphemeCombine = false;
                _wraparoundMode = true;
                _reverseWrapMode = false;
                _reverseWrapExtendedMode = false;
                _graphemeClusterMode = true;
                _currentHyperlink?.Release();
                _currentHyperlink = null;
                _charsetG0 = 'B';
                _charsetG1 = 'B';
                _charsetG2 = 'B';
                _charsetG3 = 'B';
                _activeCharsetSlot = 0;
                InitializeTabStops();
                // Clear screen
                for (int row = 0; row < _height; row++)
                {
                    for (int col = 0; col < _width; col++)
                    {
                        SetCell(row, col, TerminalCell.Empty, impacts);
                    }
                }
                // Clear KGP state
                _kgpPlacements.Clear();
                _kgpImageStore.Clear();
                break;
                
            case DecalnToken:
                // DECALN: Fill entire screen with 'E', reset margins and cursor
                _scrollTop = 0;
                _scrollBottom = _height - 1;
                _marginLeft = 0;
                _marginRight = _width - 1;
                _cursorX = 0;
                _cursorY = 0;
                _pendingWrap = false;
                for (int row = 0; row < _height; row++)
                {
                    for (int col = 0; col < _width; col++)
                    {
                        var cell = TerminalCell.Empty with { Character = "E" };
                        SetCell(row, col, cell, impacts);
                    }
                }
                break;
                
            case DeleteCharacterToken deleteCharToken:
                DeleteCharacters(deleteCharToken.Count, impacts);
                break;
                
            case InsertCharacterToken insertCharToken:
                InsertCharacters(insertCharToken.Count, impacts);
                break;
                
            case EraseCharacterToken eraseCharToken:
                EraseCharacters(eraseCharToken.Count, impacts);
                break;
                
            case DecscaToken decscaToken:
                ApplyDecsca(decscaToken.Mode);
                break;
                
            case RepeatCharacterToken repeatToken:
                RepeatLastCharacter(repeatToken.Count, impacts);
                break;
                
            case BackTabToken:
                // CBT (CSI Z): Back tab — move cursor to previous tab stop.
                // When DECLRMM is enabled and cursor is at or right of the left margin,
                // CBT clamps to the left margin instead of column 0.
                _pendingWrap = false;
                if (_cursorX > 0)
                {
                    int cbtLeftEdge = (_declrmm && _cursorX >= _marginLeft) ? _marginLeft : 0;
                    _cursorX = PrevTabStop(_cursorX, cbtLeftEdge);
                }
                break;
                
            case IndexToken:
                // Move cursor down one line, scroll if at bottom of scroll region
                if (_cursorY == _scrollBottom)
                {
                    // At bottom of scroll region — scroll if within L/R margins (or no L/R margins)
                    if (!_declrmm || (_cursorX >= _marginLeft && _cursorX <= _marginRight))
                        ScrollUp(impacts);
                    // else: cursor stays put (stuck at scroll bottom, outside L/R margin)
                }
                else if (_cursorY < _height - 1)
                {
                    _cursorY++;
                }
                break;
                
            case ReverseIndexToken:
                // Move cursor up one line, scroll if at top of scroll region
                if (_cursorY == _scrollTop)
                {
                    // At top of scroll region — scroll down if within L/R margins (or no L/R margins)
                    if (!_declrmm || (_cursorX >= _marginLeft && _cursorX <= _marginRight))
                        ScrollDown(impacts);
                    // else: cursor stays put (stuck at scroll top, outside L/R margin)
                }
                else if (_cursorY > 0)
                {
                    _cursorY--;
                }
                break;
            
            case CharacterSetToken csToken:
                // Designate a character set to one of the G0-G3 slots.
                // ESC ( X → G0, ESC ) X → G1, ESC * X → G2, ESC + X → G3
                // X = 'B' for ASCII/UTF-8, '0' for DEC special graphics
                switch (csToken.Target)
                {
                    case 0: _charsetG0 = csToken.Charset; break;
                    case 1: _charsetG1 = csToken.Charset; break;
                    case 2: _charsetG2 = csToken.Charset; break;
                    case 3: _charsetG3 = csToken.Charset; break;
                }
                break;
            
            case KeypadModeToken:
                // Keypad mode - presentation-only, no buffer state
                break;
                
            case UnrecognizedSequenceToken:
                // Ignore unrecognized sequences
                break;
                
            case KgpToken kgpToken:
                ProcessKgpCommand(kgpToken);
                break;
                
            case TabClearToken tabClear:
                // TBC (CSI Ps g): Tab Clear
                if (tabClear.Mode == 0)
                {
                    // Clear tab stop at current cursor position
                    if (_cursorX < _tabStops.Length)
                        _tabStops[_cursorX] = false;
                }
                else if (tabClear.Mode == 3)
                {
                    // Clear all tab stops
                    Array.Clear(_tabStops);
                }
                break;
                
            case DeviceStatusReportToken dsr:
                HandleDeviceStatusReport(dsr);
                break;
        }
    }
    
    private void HandleDeviceStatusReport(DeviceStatusReportToken dsr)
    {
        if (_workload == null) return;
        
        string response = dsr.Type switch
        {
            DeviceStatusReportToken.StatusReport => "\x1b[0n", // Terminal is ready
            DeviceStatusReportToken.CursorPositionReport => $"\x1b[{_cursorY + 1};{_cursorX + 1}R",
            _ => "" // Unknown DSR type
        };
        
        if (!string.IsNullOrEmpty(response))
        {
            var bytes = Encoding.UTF8.GetBytes(response);
            // Keep protocol responses ordered with user keystrokes. PowerShell/PSReadLine
            // is sensitive to delayed CPR replies; fire-and-forget thread-pool writes can
            // leak literal `1;1R` fragments into the prompt when the response arrives late.
            _ = SendProtocolResponseAsync(bytes);
        }
    }

    private async Task SendProtocolResponseAsync(byte[] bytes)
    {
        try
        {
            await WriteWorkloadInputAsync(bytes, _disposeCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ApplyTextToken(TextToken token, List<CellImpact>? impacts)
    {
        var text = token.Text;
        int i = 0;
        
        while (i < text.Length)
        {
            string grapheme;
            if (_graphemeClusterMode)
            {
                grapheme = GetGraphemeAt(text, i);
            }
            else
            {
                // Mode 2027 disabled: process one rune at a time instead of
                // clustering graphemes. Each codepoint is treated individually.
                var rune = Rune.GetRuneAt(text, i);
                grapheme = rune.ToString();
            }
            
            // Apply character set mapping (DEC special graphics, etc.)
            // Only applies to single ASCII characters; multi-byte/emoji pass through.
            var activeCharset = GetActiveCharset();
            if (activeCharset == '0' && grapheme.Length == 1 && grapheme[0] >= 0x60 && grapheme[0] <= 0x7E)
            {
                grapheme = MapDecSpecialGraphics(grapheme[0]).ToString();
            }
            
            var graphemeWidth = DisplayWidth.GetGraphemeWidth(grapheme);
            
            // Apply mode 2027 multi-codepoint width adjustment
            graphemeWidth = AdjustGraphemeWidth(grapheme, graphemeWidth);
            
            // Retroactive variation selector handling:
            // When VS15 (U+FE0E) or VS16 (U+FE0F) arrives as a standalone character
            // (not part of the same grapheme cluster as the base), modern terminals
            // retroactively modify the width of the previously printed character.
            // VS15 forces text presentation (narrow/1-cell), VS16 forces emoji
            // presentation (wide/2-cell). This matches Ghostty, Kitty, WezTerm, iTerm2.
            // Legacy terminals (xterm, Alacritty) only change the glyph, not the width.
            if (graphemeWidth == 0 && grapheme.Length <= 2 && _hasLastPrintedCell
                && Capabilities.SupportsRetroactiveVariationSelectors)
            {
                var cp = grapheme.EnumerateRunes().First().Value;
                if (cp == 0xFE0E || cp == 0xFE0F)
                {
                    ApplyRetroactiveVariationSelector(cp, impacts);
                    i += grapheme.Length;
                    continue;
                }
            }
            
            // Zero-width characters (combining marks, ZWJ) that aren't VS:
            // Retroactively combine with the previously printed cell by appending
            // to its grapheme content. This handles ZWJ sequences sent as separate
            // Feed() calls and combining marks arriving after the base character.
            if (graphemeWidth == 0 && _hasLastPrintedCell && grapheme.Length >= 1)
            {
                var cp = grapheme.EnumerateRunes().First().Value;
                // Don't re-handle VS (already handled above)
                if (cp != 0xFE0E && cp != 0xFE0F)
                {
                    int cellX = _lastPrintedCellX;
                    int cellY = _lastPrintedCellY;
                    if (cellY >= 0 && cellY < _height && cellX >= 0 && cellX < _width)
                    {
                        ref var cell = ref _screenBuffer[cellY, cellX];
                        if (!string.IsNullOrEmpty(cell.Character))
                        {
                            var newContent = cell.Character + grapheme;
                            int newWidth = DisplayWidth.GetGraphemeWidth(newContent);
                            
                            if (newWidth > _lastPrintedCellWidth)
                            {
                                cell = cell with { Character = newContent };
                                for (int w = _lastPrintedCellWidth; w < newWidth && cellX + w < _width; w++)
                                {
                                    ref var contCell = ref _screenBuffer[cellY, cellX + w];
                                    contCell = contCell with { Character = "" };
                                }
                                _cursorX = Math.Min(cellX + newWidth, _width - 1);
                                if (cellX + newWidth > _width)
                                    _pendingWrap = true;
                                _lastPrintedCellWidth = newWidth;
                                _lastPrintedCell = cell;
                            }
                            else
                            {
                                cell = cell with { Character = newContent };
                                _lastPrintedCell = cell;
                            }
                            
                            // If a ZWJ was appended, the next printable character should
                            // also combine with this cell (building a ZWJ sequence).
                            _pendingGraphemeCombine = (cp == 0x200D);
                        }
                    }
                    i += grapheme.Length;
                    continue;
                }
            }
            
            // If the previous cell ended with a ZWJ and mode 2027 is enabled,
            // combine the next printable character with it instead of printing new.
            // This builds ZWJ sequences like 👩‍👦 across separate Feed() calls.
            if (_pendingGraphemeCombine && _graphemeClusterMode && _hasLastPrintedCell)
            {
                _pendingGraphemeCombine = false;
                int cellX = _lastPrintedCellX;
                int cellY = _lastPrintedCellY;
                if (cellY >= 0 && cellY < _height && cellX >= 0 && cellX < _width)
                {
                    ref var cell = ref _screenBuffer[cellY, cellX];
                    if (!string.IsNullOrEmpty(cell.Character))
                    {
                        var newContent = cell.Character + grapheme;
                        int newWidth = DisplayWidth.GetGraphemeWidth(newContent);
                        
                        // Apply mode 2027 multi-codepoint width adjustment
                        newWidth = AdjustGraphemeWidth(newContent, newWidth);
                        
                        int oldWidth = _lastPrintedCellWidth;
                        
                        // Handle width changes
                        if (newWidth > oldWidth && cellX + newWidth > _width)
                        {
                            // Wide char doesn't fit at current position → wrap to next line
                            // Clear the original cell first
                            cell = cell with { Character = " " };
                            
                            if (_wraparoundMode)
                            {
                                _pendingWrap = false; // Clear pending wrap from original print
                                // Mark soft wrap
                                int wrapCol = _declrmm ? _marginRight : _width - 1;
                                ref var wrapCell2 = ref _screenBuffer[cellY, wrapCol];
                                wrapCell2 = wrapCell2 with { Attributes = wrapCell2.Attributes | CellAttributes.SoftWrap };
                                
                                int newX = _declrmm ? _marginLeft : 0;
                                int newY = cellY + 1;
                                
                                if (newY > _scrollBottom)
                                {
                                    ScrollUp(null);
                                    newY = _scrollBottom;
                                }
                                
                                // Print the combined wide char on the new line
                                ref var newCell = ref _screenBuffer[newY, newX];
                                newCell = newCell with { Character = newContent };
                                
                                // Add continuation cell
                                if (newX + 1 < _width)
                                {
                                    ref var contCell2 = ref _screenBuffer[newY, newX + 1];
                                    contCell2 = contCell2 with { Character = "" };
                                }
                                
                                _cursorX = Math.Min(newX + newWidth, _width - 1);
                                _cursorY = newY;
                                if (newX + newWidth >= _width)
                                    _pendingWrap = true;
                                _lastPrintedCellX = newX;
                                _lastPrintedCellY = newY;
                                _lastPrintedCellWidth = newWidth;
                                _lastPrintedCell = newCell;
                            }
                            // else: no wraparound → discard (don't print)
                        }
                        else
                        {
                            // Update the cell with the combined grapheme
                            cell = cell with { Character = newContent };
                            
                            if (newWidth > oldWidth)
                            {
                                for (int w = oldWidth; w < newWidth && cellX + w < _width; w++)
                                {
                                    ref var contCell = ref _screenBuffer[cellY, cellX + w];
                                    contCell = contCell with { Character = "" };
                                }
                            }
                            
                            _cursorX = Math.Min(cellX + newWidth, _width - 1);
                            if (cellX + newWidth > _width)
                                _pendingWrap = true;
                            _lastPrintedCellWidth = newWidth;
                            _lastPrintedCell = cell;
                        }
                        
                        // Check if the combined content still ends with ZWJ
                        var lastCombinedRune = newContent.EnumerateRunes().Last();
                        _pendingGraphemeCombine = (lastCombinedRune.Value == 0x200D);
                        
                        i += grapheme.Length;
                        continue;
                    }
                }
            }
            _pendingGraphemeCombine = false;
            
            // Deferred wrap: If a wrap was pending from a previous character, perform it now
            // This is standard VT100/xterm behavior - wrap only happens when the NEXT
            // printable character is written, not when cursor reaches the margin.
            // Only wrap if wraparound mode is enabled (DECAWM, mode 7)
            if (_pendingWrap)
            {
                if (_wraparoundMode)
                {
                    _pendingWrap = false;
                
                    // Mark the last cell of the row being left as a soft-wrap point.
                    int wrapCol = _declrmm ? _marginRight : _width - 1;
                    ref var wrapCell = ref _screenBuffer[_cursorY, wrapCol];
                    wrapCell = wrapCell with { Attributes = wrapCell.Attributes | CellAttributes.SoftWrap };
                
                    // When DECLRMM is enabled, wrap to left margin, not column 0
                    _cursorX = _declrmm ? _marginLeft : 0;
                    _cursorY++;
                }
                else
                {
                    // Wraparound disabled: stay at the last column, overwrite it
                    _pendingWrap = false;
                }
            }
            
            // Scroll if cursor is past the bottom of the screen BEFORE writing
            if (_cursorY >= _height)
            {
                ScrollUp(impacts);
                _cursorY = _height - 1;
            }
            
            // Determine effective right margin for wrapping
            // Only use margin boundary if cursor is within the L/R margin region
            bool insideLRMargin = !_declrmm || (_cursorX >= _marginLeft && _cursorX <= _marginRight);
            int effectiveRightMargin = (_declrmm && insideLRMargin) ? _marginRight : _width - 1;
            int availableWidth = effectiveRightMargin - (_declrmm ? _marginLeft : 0) + 1;
            
            // Wide char that can never fit (terminal too narrow): per Ghostty behavior,
            // the character is silently dropped and pending wrap is set so that the next
            // printable character triggers a line wrap.
            if (graphemeWidth > 1 && availableWidth < graphemeWidth)
            {
                _pendingWrap = true;
                i += grapheme.Length;
                continue;
            }
            
            // Wide char at edge: if the character won't fit (needs 2+ cells but only 1 remains)
            if (graphemeWidth > 1 && _cursorX + graphemeWidth - 1 > effectiveRightMargin && !_pendingWrap)
            {
                if (_wraparoundMode)
                {
                    if (_declrmm)
                    {
                        // DECLRMM margin wrap: no spacer head, no soft wrap flag.
                        // Just move cursor to left margin on the next row.
                        _cursorX = _marginLeft;
                        _cursorY++;
                        if (_cursorY >= _height)
                        {
                            ScrollUp(impacts);
                            _cursorY = _height - 1;
                        }
                    }
                    else
                    {
                        // Screen-edge wrap: mark right edge as spacer head with soft wrap.
                        ref var spacerCell = ref _screenBuffer[_cursorY, effectiveRightMargin];
                        spacerCell = spacerCell with
                        {
                            Character = " ",
                            TrackedHyperlink = _currentHyperlink,
                            Attributes = spacerCell.Attributes | CellAttributes.SoftWrap
                        };
                        _currentHyperlink?.AddRef();
                        
                        _cursorX = 0;
                        _cursorY++;
                        if (_cursorY >= _height)
                        {
                            ScrollUp(impacts);
                            _cursorY = _height - 1;
                        }
                    }
                }
                else
                {
                    // Wraparound disabled and wide char doesn't fit — skip it
                    i += grapheme.Length;
                    continue;
                }
            }
            
            if (_cursorX < _width && _cursorY < _height)
            {
                // IRM (Insert Mode): shift existing characters right before placing new one
                if (_insertMode)
                {
                    InsertCharacters(graphemeWidth, impacts);
                }
                
                var sequence = ++_writeSequence;
                var writtenAt = _timeProvider.GetUtcNow();
                
                // If overwriting a continuation cell (tail of a wide char), clear the leading cell
                if (_cursorX > 0 && _screenBuffer[_cursorY, _cursorX].Character == "")
                {
                    ref var leadingCell = ref _screenBuffer[_cursorY, _cursorX - 1];
                    if (leadingCell.Character.Length > 0 && leadingCell.Character != " ")
                    {
                        leadingCell.TrackedSixel?.Release();
                        leadingCell.TrackedHyperlink?.Release();
                        leadingCell = TerminalCell.Empty;
                    }
                }
                
                // If overwriting the leading cell of a wide char, clear the continuation cell
                if (_cursorX + 1 < _width && graphemeWidth == 1)
                {
                    ref var nextCell = ref _screenBuffer[_cursorY, _cursorX + 1];
                    if (nextCell.Character == "" && _screenBuffer[_cursorY, _cursorX].Character.Length > 0
                        && DisplayWidth.GetGraphemeWidth(_screenBuffer[_cursorY, _cursorX].Character) > 1)
                    {
                        nextCell.TrackedSixel?.Release();
                        nextCell.TrackedHyperlink?.Release();
                        nextCell = TerminalCell.Empty;
                    }
                }
                
                _currentHyperlink?.AddRef();
                
                var effectiveAttributes = _cursorProtected
                    ? _currentAttributes | CellAttributes.Protected
                    : _currentAttributes;
                var cell = new TerminalCell(
                    grapheme, _currentForeground, _currentBackground, effectiveAttributes,
                    sequence, writtenAt, TrackedSixel: null, _currentHyperlink,
                    _currentUnderlineColor, _currentUnderlineStyle);
                SetCell(_cursorY, _cursorX, cell, impacts);
                
                // Save last printed cell for CSI b (REP) command and VS15/VS16 handling
                _lastPrintedCell = cell;
                _hasLastPrintedCell = true;
                _lastPrintedCellX = _cursorX;
                _lastPrintedCellY = _cursorY;
                _lastPrintedCellWidth = graphemeWidth;
                
                for (int w = 1; w < graphemeWidth && _cursorX + w < _width; w++)
                {
                    _currentHyperlink?.AddRef();
                    SetCell(_cursorY, _cursorX + w, new TerminalCell(
                        "", _currentForeground, _currentBackground, _currentAttributes,
                        sequence, writtenAt, TrackedSixel: null, _currentHyperlink,
                        _currentUnderlineColor, _currentUnderlineStyle), impacts);
                }
                
                _cursorX += graphemeWidth;
                
                // When cursor reaches or exceeds the right margin, set pending wrap
                // instead of immediately wrapping. The wrap will happen when the next
                // printable character is written (or never, if CR/LF comes first).
                if (_cursorX > effectiveRightMargin)
                {
                    _cursorX = effectiveRightMargin; // Cursor stays at last column within margin
                    _pendingWrap = true;
                }
                
                // If grapheme ends with ZWJ and mode 2027 is on, the next character
                // should combine with this cell (building ZWJ/Devanagari sequences).
                if (_graphemeClusterMode && grapheme.Length > 0)
                {
                    var lastRune = grapheme.EnumerateRunes().Last();
                    _pendingGraphemeCombine = (lastRune.Value == 0x200D);
                }
            }
            i += grapheme.Length;
        }
    }

    /// <summary>
    /// Adjusts grapheme width for mode 2027 multi-codepoint clusters.
    /// When grapheme clustering is enabled and a grapheme has multiple visible
    /// codepoints with a total width > 1 (e.g., Devanagari ligatures like क्‍ष),
    /// the grapheme is treated as wide (2 cells). This matches Ghostty behavior
    /// for Indic scripts and other complex scripts.
    /// </summary>
    private int AdjustGraphemeWidth(string grapheme, int baseWidth)
    {
        if (!_graphemeClusterMode || baseWidth != 1)
            return baseWidth;
        
        int runeCount = 0;
        int totalVisibleWidth = 0;
        foreach (var rune in grapheme.EnumerateRunes())
        {
            runeCount++;
            int runeWidth = DisplayWidth.GetRuneWidth(rune);
            if (runeWidth > 0)
                totalVisibleWidth += runeWidth;
        }
        
        if (runeCount > 1 && totalVisibleWidth > 1)
            return 2;
        
        return baseWidth;
    }
    
    /// <summary>
    /// Retroactively applies VS15 (U+FE0E) or VS16 (U+FE0F) to the last printed cell.
    /// VS15 forces text presentation (1 cell wide), VS16 forces emoji presentation (2 cells wide).
    /// When the width changes, the cursor position and screen buffer are adjusted.
    /// </summary>
    private void ApplyRetroactiveVariationSelector(int selectorCodepoint, List<CellImpact>? impacts)
    {
        int cellX = _lastPrintedCellX;
        int cellY = _lastPrintedCellY;
        int oldWidth = _lastPrintedCellWidth;
        
        // Validate the cell is still on screen
        if (cellY < 0 || cellY >= _height || cellX < 0 || cellX >= _width)
            return;
        
        ref var cell = ref _screenBuffer[cellY, cellX];
        if (string.IsNullOrEmpty(cell.Character))
            return;
        
        // Check if the base character is a valid target for this variation selector.
        // VS16 (emoji presentation) should only be applied to characters with the
        // Unicode Emoji property. VS15 (text presentation) should only be applied to
        // characters that have a text/emoji presentation variant.
        // Non-emoji characters like 'x', 'n', etc. should completely ignore VS16.
        // This matches Ghostty, kitty, and WezTerm behavior.
        var baseRune = cell.Character.EnumerateRunes().FirstOrDefault();
        if (selectorCodepoint == 0xFE0F) // VS16
        {
            if (!DisplayWidth.HasEmojiProperty(baseRune.Value) && !DisplayWidth.IsSmpEmoji(baseRune.Value))
                return; // Not an emoji base — ignore VS16
        }
        
        // Compute the new grapheme by appending the variation selector
        var vs = char.ConvertFromUtf32(selectorCodepoint);
        var newGrapheme = cell.Character + vs;
        int newWidth = DisplayWidth.GetGraphemeWidth(newGrapheme);
        
        if (newWidth == oldWidth)
        {
            // Width unchanged — the variation selector has no visible effect.
            // Don't modify the cell content. For example, VS15 on always-wide
            // SMP emoji (like 🧠) or VS16 on already-wide emoji should be
            // silently discarded. This matches Ghostty behavior.
            return;
        }
        
        if (newWidth < oldWidth)
        {
            // Shrinking (VS15: wide → narrow). Clear the continuation cell(s) and
            // move the cursor back.
            cell = cell with { Character = newGrapheme };
            
            // Clear continuation cells
            for (int w = newWidth; w < oldWidth && cellX + w < _width; w++)
            {
                _screenBuffer[cellY, cellX + w] = TerminalCell.Empty;
            }
            
            // Adjust cursor: move back by the difference in width
            _cursorX = cellX + newWidth;
            
            // If pending wrap was set because the wide char hit the margin,
            // it should be cleared since the narrow char no longer fills it
            if (_pendingWrap)
                _pendingWrap = false;
        }
        else
        {
            // Widening (VS16: narrow → wide). Need to check if there's room.
            // If the cell is at the right edge, wrap to the next line — the wide
            // char can't fit in the remaining space. Replace the current position
            // with a spacer and print the wide char at the start of the next line.
            // This matches Ghostty behavior for VS16 widening at margins.
            if (cellX + newWidth > _width)
            {
                if (!_wraparoundMode)
                {
                    // Wraparound disabled — can't widen beyond the right margin.
                    // Discard the VS and keep the cell unchanged.
                    return;
                }
                
                // Widening (VS16: narrow → wide) with wrap. The wide char can't fit
                // in the remaining space. Replace the current position with a spacer
                // and print the wide char at the start of the next line.
                // This matches Ghostty behavior for VS16 widening at margins.
                
                // Clear the cell at the current position (spacer/blank)
                cell = TerminalCell.Empty;
                
                // Move to start of next line (scroll if needed)
                int newRow = cellY + 1;
                int scrollBottom = _scrollBottom;
                if (newRow > scrollBottom)
                {
                    ScrollUp(null);
                    newRow = scrollBottom;
                }
                _cursorY = newRow;
                _cursorX = 0;
                
                // Write the wide character at the new position
                ref var newCell = ref _screenBuffer[_cursorY, 0];
                newCell = newCell with { Character = newGrapheme };
                
                // Add continuation cell (empty string marks it as wide char tail)
                if (_width > 1)
                {
                    ref var contCell = ref _screenBuffer[_cursorY, 1];
                    contCell = contCell with { Character = "" };
                }
                
                // Position cursor after the wide char
                _cursorX = newWidth;
                if (_cursorX > _width - 1)
                {
                    _cursorX = _width - 1;
                    _pendingWrap = true;
                }
                
                // Update tracking
                _lastPrintedCellX = 0;
                _lastPrintedCellY = _cursorY;
                _lastPrintedCell = newCell;
                _lastPrintedCellWidth = newWidth;
                return;
            }
            
            cell = cell with { Character = newGrapheme };
            
            // Add continuation cell(s) (empty string marks as wide char tail)
            for (int w = oldWidth; w < newWidth && cellX + w < _width; w++)
            {
                ref var contCell = ref _screenBuffer[cellY, cellX + w];
                contCell = contCell with { Character = "" };
            }
            
            // Adjust cursor forward
            _cursorX = cellX + newWidth;
            if (_cursorX > _width - 1)
            {
                _cursorX = _width - 1;
                _pendingWrap = true;
            }
        }
        
        // Update tracking
        _lastPrintedCell = cell;
        _lastPrintedCellWidth = newWidth;
    }

    /// <summary>
    /// Applies DECSCA (Select Character Protection Attribute).
    /// Mode 1 = protected, mode 0/2 = unprotected.
    /// </summary>
    private void ApplyDecsca(int mode)
    {
        switch (mode)
        {
            case 0:
            case 2:
                // Turn off protection. Note: _protectedMode is intentionally NOT
                // reset to Off — erase operations need to know the most recent mode.
                _cursorProtected = false;
                break;
            case 1:
                _cursorProtected = true;
                // DECSCA 1 sets DEC protection mode
                _protectedMode = ProtectedMode.Dec;
                break;
        }
    }
    
    /// <summary>
    /// Sets the protected mode directly (used by conformance tests via escape sequences).
    /// ISO mode is set via SPA (ESC V), DEC mode via DECSCA.
    /// </summary>
    internal void SetProtectedMode(ProtectedMode mode)
    {
        switch (mode)
        {
            case ProtectedMode.Off:
                _cursorProtected = false;
                // _protectedMode is never reset — this matches Ghostty behavior
                break;
            case ProtectedMode.Iso:
                _cursorProtected = true;
                _protectedMode = ProtectedMode.Iso;
                break;
            case ProtectedMode.Dec:
                _cursorProtected = true;
                _protectedMode = ProtectedMode.Dec;
                break;
        }
    }
    
    /// <summary>
    /// Checks if a cell at the given position has the Protected attribute.
    /// </summary>
    private bool IsProtectedCell(int row, int col)
    {
        return (_screenBuffer[row, col].Attributes & CellAttributes.Protected) != 0;
    }

    /// <summary>
    /// Initializes tab stops at default positions (every 8 columns).
    /// </summary>
    private void InitializeTabStops()
    {
        _tabStops = new bool[_width];
        for (int i = 0; i < _width; i++)
        {
            _tabStops[i] = (i % 8 == 0) && i > 0;
        }
    }

    /// <summary>
    /// Finds the next tab stop column after the current position.
    /// Returns rightEdge if no tab stop is found.
    /// </summary>
    private int NextTabStop(int fromCol, int rightEdge)
    {
        for (int i = fromCol + 1; i <= rightEdge; i++)
        {
            if (i < _tabStops.Length && _tabStops[i])
                return i;
        }
        return rightEdge;
    }

    /// <summary>
    /// Finds the previous tab stop column before the current position.
    /// Returns leftEdge if no tab stop is found.
    /// </summary>
    private int PrevTabStop(int fromCol, int leftEdge)
    {
        for (int i = fromCol - 1; i >= leftEdge; i--)
        {
            if (i < _tabStops.Length && _tabStops[i])
                return i;
        }
        return leftEdge;
    }

    private void ApplyControlCharacter(ControlCharacterToken token, List<CellImpact>? impacts)
    {
        switch (token.Character)
        {
            case '\n':
                // LF clears pending wrap - the wrap is "consumed" by the line feed
                _pendingWrap = false;
                
                // LNM (DEC mode 20): when enabled, LF also performs CR.
                // This is typically OFF in terminal emulators - the ONLCR translation
                // happens in the PTY/TTY kernel driver for cooked mode apps.
                if (_newlineMode)
                {
                    // When DECLRMM is enabled, CR goes to left margin
                    _cursorX = _declrmm ? _marginLeft : 0;
                }
                
                // LF moves cursor down. If at bottom of scroll region, scroll up.
                if (_cursorY >= _scrollBottom)
                {
                    ScrollUp(impacts);
                    // Cursor stays at _scrollBottom
                }
                else if (_cursorY < _height - 1)
                {
                    _cursorY++;
                }
                break;
                
            case '\r':
                // CR clears pending wrap - moving to left margin cancels any pending wrap
                _pendingWrap = false;
                // When DECLRMM is enabled and cursor is at or right of left margin,
                // CR moves to left margin. If cursor is left of left margin, CR moves to col 0.
                if (_declrmm && _cursorX >= _marginLeft)
                    _cursorX = _marginLeft;
                else
                    _cursorX = 0;
                break;
                
            case '\t':
                // HT: Move to next tab stop
                // When DECLRMM is enabled and cursor is within margins, clamp to right margin
                int tabRight = (_declrmm && _cursorX <= _marginRight) ? _marginRight : _width - 1;
                _cursorX = NextTabStop(_cursorX, tabRight);
                break;
                
            case '\b':
                // Backspace - move cursor left (non-destructive)
                // Applies reverse wrap logic (same as CUB 1)
                ApplyCursorLeftWithReverseWrap(1);
                break;
                
            case '\x0E': // SO (Shift Out) — invoke G1 into GL
                _activeCharsetSlot = 1;
                break;
                
            case '\x0F': // SI (Shift In) — invoke G0 into GL
                _activeCharsetSlot = 0;
                break;
        }
    }

    private void ApplyCursorMove(CursorMoveToken token)
    {
        // CUB (Back) has its own pending wrap handling for reverse wrap
        if (token.Direction == CursorMoveDirection.Back)
        {
            ApplyCursorLeftWithReverseWrap(token.Count);
            return;
        }
        
        // Any other explicit cursor movement clears pending wrap
        _pendingWrap = false;
        
        switch (token.Direction)
        {
            case CursorMoveDirection.Up:
                // If cursor is within or at the top scroll margin, clamp to top margin
                // If cursor is above the scroll region, clamp to row 0
                if (_cursorY >= _scrollTop)
                    _cursorY = Math.Max(_scrollTop, _cursorY - token.Count);
                else
                    _cursorY = Math.Max(0, _cursorY - token.Count);
                break;
                
            case CursorMoveDirection.Down:
                // If cursor is within or at the bottom scroll margin, clamp to bottom margin
                // If cursor is below the scroll region, clamp to last row
                if (_cursorY <= _scrollBottom)
                    _cursorY = Math.Min(_scrollBottom, _cursorY + token.Count);
                else
                    _cursorY = Math.Min(_height - 1, _cursorY + token.Count);
                break;
                
            case CursorMoveDirection.Forward:
                // If DECLRMM is enabled and cursor is within margin bounds, clamp to right margin
                if (_declrmm && _cursorX <= _marginRight)
                    _cursorX = Math.Min(_marginRight, _cursorX + token.Count);
                else
                    _cursorX = Math.Min(_width - 1, _cursorX + token.Count);
                break;
                
            case CursorMoveDirection.NextLine:
                _cursorY = Math.Min(_height - 1, _cursorY + token.Count);
                _cursorX = 0;
                break;
                
            case CursorMoveDirection.PreviousLine:
                _cursorY = Math.Max(0, _cursorY - token.Count);
                _cursorX = 0;
                break;
        }
    }
    
    /// <summary>
    /// Moves cursor left with reverse wraparound support (modes 45 and 1045).
    /// When reverse wrap is enabled and the cursor is at the left margin, the cursor
    /// wraps to the right margin of the previous line. Mode 45 only wraps across
    /// soft-wrapped lines; mode 1045 (extended) also wraps across hard breaks and
    /// from the top row to the bottom row.
    /// </summary>
    private void ApplyCursorLeftWithReverseWrap(int count)
    {
        // Determine wrap mode — both require DECAWM to be enabled
        bool isExtended = _wraparoundMode && _reverseWrapExtendedMode;
        bool isReverse = _wraparoundMode && _reverseWrapMode;
        
        if (!isExtended && !isReverse)
        {
            // No reverse wrap — simple left movement, clear pending wrap
            _pendingWrap = false;
            _cursorX = Math.Max(0, _cursorX - count);
            return;
        }
        
        // When pending wrap is set, decrement count by one to match xterm behavior.
        // CUB 1 with pending wrap just clears the wrap flag without moving.
        if (_pendingWrap)
        {
            count--;
            _pendingWrap = false;
        }
        
        if (count <= 0)
            return;
        
        int leftMargin = (_cursorX < (_declrmm ? _marginLeft : 0)) ? 0 : (_declrmm ? _marginLeft : 0);
        int rightMargin = _declrmm ? _marginRight : _width - 1;
        int top = _scrollTop;
        int bottom = _scrollBottom;
        
        // Pre-loop edge case: cursor is already at left margin
        if (_cursorX == leftMargin)
        {
            if (!isExtended && _cursorY <= top)
            {
                // Mode 45: at/above scroll top → move to (leftMargin, top) and stop
                _cursorX = leftMargin;
                _cursorY = top;
                return;
            }
        }
        
        while (count > 0)
        {
            int maxLeft = _cursorX - leftMargin;
            int move = Math.Min(maxLeft, count);
            _cursorX -= move;
            count -= move;
            
            if (count == 0)
                break;
            
            // At left margin with more to move — need to wrap up
            if (_cursorY == top)
            {
                if (!isExtended)
                {
                    // Mode 45: stop at top of scroll region
                    _cursorX = leftMargin;
                    break;
                }
                
                // Extended: wrap from top to bottom
                _cursorX = rightMargin;
                _cursorY = bottom;
                count--;
                continue;
            }
            
            // Stop at absolute row 0 regardless of mode
            if (_cursorY == 0)
                break;
            
            if (!isExtended)
            {
                // Mode 45: only wrap across soft-wrapped lines
                ref var lastCellPrevRow = ref _screenBuffer[_cursorY - 1, rightMargin];
                if ((lastCellPrevRow.Attributes & CellAttributes.SoftWrap) == 0)
                    break;
            }
            
            _cursorX = rightMargin;
            _cursorY--;
            count--;
        }
    }

    private void ApplyClearScreen(ClearMode mode, bool selective, List<CellImpact>? impacts)
    {
        // ED resets pending wrap per ECMA-48
        _pendingWrap = false;
        
        // Determine if we should respect protection:
        // - If selective (DECSED / CSI ? J): always respect protection
        // - If not selective (normal ED / CSI J): respect only if ISO mode was last set
        bool respectProtection = selective || _protectedMode == ProtectedMode.Iso;
        
        switch (mode)
        {
            case ClearMode.ToEnd:
                ClearFromCursor(respectProtection, impacts);
                break;
            case ClearMode.ToStart:
                ClearToCursor(respectProtection, impacts);
                break;
            case ClearMode.All:
            case ClearMode.AllAndScrollback:
                ClearBuffer(respectProtection, impacts);
                if (mode == ClearMode.AllAndScrollback)
                {
                    _scrollbackBuffer?.Clear();
                }
                break;
        }
    }

    private void ApplyClearLine(ClearMode mode, bool selective, List<CellImpact>? impacts)
    {
        // EL resets pending wrap per ECMA-48
        _pendingWrap = false;
        
        // Determine if we should respect protection:
        // - If selective (DECSEL / CSI ? K): always respect protection
        // - If not selective (normal EL / CSI K): respect only if ISO mode was last set
        bool respectProtection = selective || _protectedMode == ProtectedMode.Iso;
        
        // When DECLRMM is enabled, clear operations respect left/right margins
        int effectiveLeft = _declrmm ? _marginLeft : 0;
        int effectiveRight = _declrmm ? _marginRight : _width - 1;
        
        var eraseCell = CreateEraseCell();
        int clearedCount = 0;
        switch (mode)
        {
            case ClearMode.ToEnd:
                // If cursor is on a wide char continuation cell, also clear the leading cell
                if (_cursorX > effectiveLeft && _screenBuffer[_cursorY, _cursorX].Character == "")
                {
                    if (!respectProtection || !IsProtectedCell(_cursorY, _cursorX - 1))
                    {
                        SetCell(_cursorY, _cursorX - 1, eraseCell, impacts);
                        clearedCount++;
                    }
                }
                for (int x = _cursorX; x <= effectiveRight && x < _width; x++)
                {
                    if (respectProtection && IsProtectedCell(_cursorY, x))
                        continue;
                    SetCell(_cursorY, x, eraseCell, impacts);
                    clearedCount++;
                }
                break;
            case ClearMode.ToStart:
                for (int x = effectiveLeft; x <= _cursorX && x < _width; x++)
                {
                    if (respectProtection && IsProtectedCell(_cursorY, x))
                        continue;
                    SetCell(_cursorY, x, eraseCell, impacts);
                    clearedCount++;
                }
                // If cursor stopped on the leading half of a wide char, also clear continuation
                if (_cursorX + 1 <= effectiveRight && _cursorX + 1 < _width &&
                    _screenBuffer[_cursorY, _cursorX + 1].Character == "")
                {
                    if (!respectProtection || !IsProtectedCell(_cursorY, _cursorX + 1))
                    {
                        SetCell(_cursorY, _cursorX + 1, eraseCell, impacts);
                        clearedCount++;
                    }
                }
                break;
            case ClearMode.All:
                for (int x = effectiveLeft; x <= effectiveRight && x < _width; x++)
                {
                    if (respectProtection && IsProtectedCell(_cursorY, x))
                        continue;
                    SetCell(_cursorY, x, eraseCell, impacts);
                    clearedCount++;
                }
                break;
        }
    }

    /// <summary>
    /// Gets the active character set designation character for the currently invoked GL charset.
    /// </summary>
    private char GetActiveCharset()
    {
        return _activeCharsetSlot switch
        {
            0 => _charsetG0,
            1 => _charsetG1,
            2 => _charsetG2,
            3 => _charsetG3,
            _ => 'B'
        };
    }
    
    /// <summary>
    /// Maps a character through the DEC special graphics charset (charset '0').
    /// Only maps characters in the ASCII range 0x60-0x7E. Characters outside
    /// this range are passed through unchanged.
    /// </summary>
    private static char MapDecSpecialGraphics(char c)
    {
        return c switch
        {
            '`' => '\u25C6', // ◆ Diamond
            'a' => '\u2592', // ▒ Checkerboard
            'b' => '\u2409', // ␉ HT
            'c' => '\u240C', // ␌ FF
            'd' => '\u240D', // ␍ CR
            'e' => '\u240A', // ␊ LF
            'f' => '\u00B0', // ° Degree
            'g' => '\u00B1', // ± Plus/Minus
            'h' => '\u2424', // ␤ NL
            'i' => '\u240B', // ␋ VT
            'j' => '\u2518', // ┘ Lower Right
            'k' => '\u2510', // ┐ Upper Right
            'l' => '\u250C', // ┌ Upper Left
            'm' => '\u2514', // └ Lower Left
            'n' => '\u253C', // ┼ Crossing
            'o' => '\u23BA', // ⎺ Horizontal 1
            'p' => '\u23BB', // ⎻ Horizontal 2
            'q' => '\u2500', // ─ Horizontal 3
            'r' => '\u23BC', // ⎼ Horizontal 4
            's' => '\u23BD', // ⎽ Horizontal 5
            't' => '\u251C', // ├ Left T
            'u' => '\u2524', // ┤ Right T
            'v' => '\u2534', // ┴ Bottom T
            'w' => '\u252C', // ┬ Top T
            'x' => '\u2502', // │ Vertical
            'y' => '\u2264', // ≤ Less/Equal
            'z' => '\u2265', // ≥ Greater/Equal
            '{' => '\u03C0', // π Pi
            '|' => '\u2260', // ≠ Not Equal
            '}' => '\u00A3', // £ Pound
            '~' => '\u00B7', // · Middle Dot
            _ => c
        };
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
            var grapheme = (string)enumerator.Current;
            
            // Terminal-specific grapheme splitting: .NET's Unicode grapheme clustering
            // gives Fitzpatrick skin tone modifiers (U+1F3FB–1F3FF) the property
            // Grapheme_Cluster_Break=Extend, which causes them to combine with ANY
            // preceding character. However, terminal emulators must only combine them
            // when the base character is a valid Emoji_Modifier_Base. When the base is
            // not a modifier base (e.g., a quote mark, letter, or non-person emoji),
            // the skin tone modifier should be treated as a standalone character.
            // This matches Ghostty, kitty, and other conformant terminals.
            if (grapheme.EnumerateRunes().Count() > 1)
            {
                var runes = grapheme.EnumerateRunes().ToArray();
                var baseRune = runes[0];
                
                // Check if any subsequent rune is a Fitzpatrick modifier
                // following a non-Emoji_Modifier_Base
                for (int r = 1; r < runes.Length; r++)
                {
                    if (DisplayWidth.IsFitzpatrickModifier(runes[r].Value) &&
                        !DisplayWidth.IsEmojiModifierBase(baseRune.Value))
                    {
                        // Split: return only the base character(s) before the modifier.
                        // The modifier will be picked up as its own grapheme on the
                        // next iteration.
                        int splitLen = 0;
                        for (int s = 0; s < r; s++)
                            splitLen += runes[s].Utf16SequenceLength;
                        return grapheme[..splitLen];
                    }
                }
                
                // Terminal-specific variation selector splitting: .NET clusters
                // VS15 (U+FE0E) and VS16 (U+FE0F) with any preceding character,
                // but terminal emulators process variation selectors retroactively —
                // first print the base character, then modify it when the VS arrives.
                //
                // Splitting rules for VS at position 1 (right after base):
                // - VS16 on non-emoji base (e.g., 'x'+VS16, 'n'+VS16+combining):
                //   Split so handler discards invalid VS16. Any subsequent runes
                //   (like combining marks) will combine with the base naturally.
                // - VS16 on emoji base (e.g., '#'+VS16+keycap, '☔'+VS16):
                //   Keep intact — VS16 is part of a valid emoji sequence.
                // - VS15 after any base: Split so handler can narrow emoji.
                //   Non-emoji bases won't be affected (handler is a no-op).
                if (runes.Length >= 2 && (runes[1].Value == 0xFE0E || runes[1].Value == 0xFE0F))
                {
                    bool isVS16 = runes[1].Value == 0xFE0F;
                    
                    if (isVS16)
                    {
                        // Only split VS16 from non-emoji bases
                        if (!DisplayWidth.HasEmojiProperty(baseRune.Value) &&
                            !DisplayWidth.IsSmpEmoji(baseRune.Value))
                        {
                            return grapheme[..baseRune.Utf16SequenceLength];
                        }
                    }
                    else
                    {
                        // Always split VS15 for retroactive handling
                        return grapheme[..baseRune.Utf16SequenceLength];
                    }
                }
            }
            
            return grapheme;
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

    private void DoEnterAlternateScreen(List<CellImpact>? impacts = null)
    {
        // Save cursor position before switching to alternate screen
        // This uses separate fields from DECSC/DECRC to avoid conflicts
        _alternateScreenSavedCursorX = _cursorX;
        _alternateScreenSavedCursorY = _cursorY;
        
        // Always save the main screen buffer for internal state (needed for snapshots)
        // and for presentation adapters that don't handle alternate screen natively
        _savedMainScreenBuffer = new TerminalCell[_height, _width];
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _savedMainScreenBuffer[y, x] = _screenBuffer[y, x];
            }
        }
        
        _inAlternateScreen = true;
        
        // If the presentation handles alternate screen natively, the real terminal
        // will clear on entry. We just need to update our internal buffer.
        // If not, we need to generate impacts so the presentation layer sees the clear.
        if (Capabilities.HandlesAlternateScreenNatively)
        {
            // Just clear internal buffer without generating impacts
            ClearBufferInternal();
        }
        else
        {
            // Clear with impacts for presentation adapters that need them
            ClearBuffer(respectProtection: false, impacts);
        }
        
        // Mode 1049 saves cursor position and copies it to the alt screen
        // (cursor position is preserved, not reset to 0,0)
    }

    private void DoExitAlternateScreen(List<CellImpact>? impacts = null)
    {
        _inAlternateScreen = false;
        
        // Only restore if we actually saved state when entering alternate screen
        // If _savedMainScreenBuffer is null, this is an unbalanced exit - do nothing
        if (_savedMainScreenBuffer != null)
        {
            // If the presentation handles alternate screen natively, the real terminal
            // will restore its buffer when it receives the escape sequence. We just need
            // to update our internal buffer to match (for snapshot purposes).
            // If not, we need to generate impacts so the presentation layer gets restored.
            bool generateImpacts = !Capabilities.HandlesAlternateScreenNatively;
            
            // Restore the saved buffer (or as much as fits in current dimensions)
            int restoreHeight = Math.Min(_height, _savedMainScreenBuffer.GetLength(0));
            int restoreWidth = Math.Min(_width, _savedMainScreenBuffer.GetLength(1));
            
            for (int y = 0; y < restoreHeight; y++)
            {
                for (int x = 0; x < restoreWidth; x++)
                {
                    if (generateImpacts)
                    {
                        SetCell(y, x, _savedMainScreenBuffer[y, x], impacts);
                    }
                    else
                    {
                        // Direct assignment for internal buffer only
                        _screenBuffer[y, x] = _savedMainScreenBuffer[y, x];
                    }
                }
            }
            
            // Clear any remaining area if current dimensions are larger
            for (int y = 0; y < _height; y++)
            {
                for (int x = (y < restoreHeight ? restoreWidth : 0); x < _width; x++)
                {
                    if (generateImpacts)
                    {
                        SetCell(y, x, TerminalCell.Empty, impacts);
                    }
                    else
                    {
                        _screenBuffer[y, x] = TerminalCell.Empty;
                    }
                }
            }
            
            _savedMainScreenBuffer = null;
            
            // Restore cursor position from alternate screen save
            _cursorX = _alternateScreenSavedCursorX;
            _cursorY = _alternateScreenSavedCursorY;
        }
        // If no saved buffer, this is an unbalanced exit - leave everything as-is
    }

    private void ProcessSgr(string parameters)
    {
        if (string.IsNullOrEmpty(parameters) || parameters == "0")
        {
            _currentForeground = null;
            _currentBackground = null;
            _currentUnderlineColor = null;
            _currentUnderlineStyle = UnderlineStyle.None;
            _currentAttributes = CellAttributes.None;
            return;
        }

        var parts = parameters.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            
            // Check for colon-separated sub-parameters (e.g., "4:3", "38:2:0:1:2:3")
            if (part.Contains(':'))
            {
                ProcessSgrWithSubParams(part);
                continue;
            }
            
            if (!int.TryParse(part, out var code))
                continue;

            switch (code)
            {
                case 0:
                    _currentForeground = null;
                    _currentBackground = null;
                    _currentUnderlineColor = null;
                    _currentUnderlineStyle = UnderlineStyle.None;
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
                    _currentUnderlineStyle = UnderlineStyle.Single;
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
                case 21: // Double underline (ECMA-48)
                    _currentAttributes |= CellAttributes.Underline;
                    _currentUnderlineStyle = UnderlineStyle.Double;
                    break;
                case 22: // Normal intensity (not bold, not dim)
                    _currentAttributes &= ~(CellAttributes.Bold | CellAttributes.Dim);
                    break;
                case 23:
                    _currentAttributes &= ~CellAttributes.Italic;
                    break;
                case 24:
                    _currentAttributes &= ~CellAttributes.Underline;
                    _currentUnderlineStyle = UnderlineStyle.None;
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
                case 39: // Default foreground color
                    _currentForeground = null;
                    break;
                case >= 40 and <= 47:
                    _currentBackground = StandardColorFromCode(code - 40);
                    break;
                case 49: // Default background color
                    _currentBackground = null;
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
                case 58:
                    // Underline color (semicolon syntax)
                    if (i + 2 < parts.Length && parts[i + 1] == "5")
                    {
                        if (int.TryParse(parts[i + 2], out var colorIndex))
                        {
                            _currentUnderlineColor = Color256FromIndex(colorIndex);
                        }
                        i += 2; // 58;5;N
                    }
                    else if (i + 4 < parts.Length && parts[i + 1] == "2")
                    {
                        if (int.TryParse(parts[i + 2], out var r) &&
                            int.TryParse(parts[i + 3], out var g) &&
                            int.TryParse(parts[i + 4], out var b))
                        {
                            _currentUnderlineColor = Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
                        }
                        i += 4; // 58;2;R;G;B
                    }
                    break;
                case 59: // Default underline color
                    _currentUnderlineColor = null;
                    break;
            }
        }
    }
    
    /// <summary>
    /// Processes an SGR parameter that contains colon-separated sub-parameters.
    /// Examples: "4:3" (curly underline), "38:2:0:1:2:3" (RGB fg with colorspace),
    /// "48:2::1:2:3" (RGB bg, empty colorspace), "58:2:1:2:3" (underline color).
    /// </summary>
    private void ProcessSgrWithSubParams(string part)
    {
        var subs = part.Split(':');
        if (subs.Length < 1 || !int.TryParse(subs[0], out var code))
            return;
        
        switch (code)
        {
            case 4: // Underline with style sub-parameter
                if (subs.Length >= 2 && int.TryParse(subs[1], out var underlineStyle))
                {
                    if (underlineStyle == 0)
                    {
                        _currentAttributes &= ~CellAttributes.Underline;
                        _currentUnderlineStyle = UnderlineStyle.None;
                    }
                    else
                    {
                        _currentAttributes |= CellAttributes.Underline;
                        _currentUnderlineStyle = underlineStyle switch
                        {
                            1 => UnderlineStyle.Single,
                            2 => UnderlineStyle.Double,
                            3 => UnderlineStyle.Curly,
                            4 => UnderlineStyle.Dotted,
                            5 => UnderlineStyle.Dashed,
                            _ => UnderlineStyle.Single, // Unknown style defaults to single
                        };
                    }
                }
                break;
                
            case 38: // Foreground extended color (colon syntax)
            case 48: // Background extended color (colon syntax)
            case 58: // Underline color (colon syntax)
                ProcessExtendedColorColon(code, subs);
                break;
        }
    }
    
    /// <summary>
    /// Processes extended color with colon sub-parameters.
    /// Formats: code:2:colorspace:R:G:B or code:2::R:G:B or code:2:R:G:B or code:5:N
    /// </summary>
    private void ProcessExtendedColorColon(int code, string[] subs)
    {
        if (subs.Length < 2)
            return;
            
        if (!int.TryParse(subs[1], out var colorType))
            return;
            
        if (colorType == 5 && subs.Length >= 3)
        {
            // 256-color: code:5:N
            if (int.TryParse(subs[2], out var idx))
            {
                var color = Color256FromIndex(idx);
                ApplyExtendedColor(code, color);
            }
        }
        else if (colorType == 2)
        {
            // RGB: code:2:R:G:B or code:2:colorspace:R:G:B or code:2::R:G:B
            // Try to find R,G,B — they're the last 3 numeric values
            int r, g, b;
            if (subs.Length >= 6 && 
                int.TryParse(subs[3], out r) && 
                int.TryParse(subs[4], out g) && 
                int.TryParse(subs[5], out b))
            {
                // code:2:colorspace:R:G:B (6 sub-params)
                ApplyExtendedColor(code, Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b));
            }
            else if (subs.Length >= 5 && 
                     int.TryParse(subs[2], out r) && 
                     int.TryParse(subs[3], out g) && 
                     int.TryParse(subs[4], out b))
            {
                // code:2:R:G:B (5 sub-params, no colorspace)
                ApplyExtendedColor(code, Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b));
            }
        }
    }
    
    private void ApplyExtendedColor(int code, Hex1bColor color)
    {
        switch (code)
        {
            case 38:
                _currentForeground = color;
                break;
            case 48:
                _currentBackground = color;
                break;
            case 58:
                _currentUnderlineColor = color;
                break;
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

    private void ClearFromCursor(bool respectProtection = false, List<CellImpact>? impacts = null)
    {
        var eraseCell = CreateEraseCell();
        // If cursor is on a wide char continuation cell, also clear the leading cell
        if (_cursorX > 0 && _screenBuffer[_cursorY, _cursorX].Character == "")
        {
            if (!respectProtection || !IsProtectedCell(_cursorY, _cursorX - 1))
                SetCell(_cursorY, _cursorX - 1, eraseCell, impacts);
        }
        for (int x = _cursorX; x < _width; x++)
        {
            if (respectProtection && IsProtectedCell(_cursorY, x))
                continue;
            SetCell(_cursorY, x, eraseCell, impacts);
        }
        for (int y = _cursorY + 1; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (respectProtection && IsProtectedCell(y, x))
                    continue;
                SetCell(y, x, eraseCell, impacts);
            }
        }
    }

    private void ClearToCursor(bool respectProtection = false, List<CellImpact>? impacts = null)
    {
        var eraseCell = CreateEraseCell();
        for (int y = 0; y < _cursorY; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (respectProtection && IsProtectedCell(y, x))
                    continue;
                SetCell(y, x, eraseCell, impacts);
            }
        }
        for (int x = 0; x <= _cursorX && x < _width; x++)
        {
            if (respectProtection && IsProtectedCell(_cursorY, x))
                continue;
            SetCell(_cursorY, x, eraseCell, impacts);
        }
        // If the cell just past cursor is a continuation cell, its leading cell was erased —
        // clear the orphaned continuation too.
        if (_cursorX + 1 < _width && _screenBuffer[_cursorY, _cursorX + 1].Character == "")
        {
            if (!respectProtection || !IsProtectedCell(_cursorY, _cursorX + 1))
                SetCell(_cursorY, _cursorX + 1, eraseCell, impacts);
        }
    }

    private void ScrollUp(List<CellImpact>? impacts = null)
    {
        // Scroll up within the scroll region
        // When DECLRMM is enabled, only scroll within left/right margins
        int leftCol = _declrmm ? _marginLeft : 0;
        int rightCol = _declrmm ? _marginRight : _width - 1;
        
        // Capture the top row into the scrollback buffer before it's overwritten.
        // Only capture when: scrollback is enabled, not in alternate screen, scroll region
        // starts at row 0, and we're scrolling full-width rows (not partial DECLRMM margins).
        if (_scrollbackBuffer is not null
            && !_inAlternateScreen
            && _scrollTop == 0
            && leftCol == 0 && rightCol == _width - 1)
        {
            CaptureRowToScrollback(_scrollTop);
        }
        
        // First, release Sixel data from the top row of the region (being scrolled off)
        for (int x = leftCol; x <= rightCol; x++)
        {
            _screenBuffer[_scrollTop, x].TrackedSixel?.Release();
        }
        
        // Shift rows up within the scroll region
        // All affected cells need to be recorded as impacts
        for (int y = _scrollTop; y < _scrollBottom; y++)
        {
            for (int x = leftCol; x <= rightCol; x++)
            {
                var cellFromBelow = _screenBuffer[y + 1, x];
                SetCell(y, x, cellFromBelow, impacts);
            }
        }
        
        // Clear the bottom row of the scroll region (within margins)
        var eraseCell = CreateEraseCell();
        for (int x = leftCol; x <= rightCol; x++)
        {
            SetCell(_scrollBottom, x, eraseCell, impacts);
        }

        // Scroll KGP placements up within the scroll region
        if (_kgpPlacements.Count > 0)
        {
            for (int i = _kgpPlacements.Count - 1; i >= 0; i--)
            {
                var p = _kgpPlacements[i];
                if (p.Row >= _scrollTop && p.Row <= _scrollBottom)
                {
                    p.Row--;
                    if (p.Row < _scrollTop)
                    {
                        _kgpPlacements.RemoveAt(i);
                    }
                }
            }
        }
    }
    
    private void ScrollDown(List<CellImpact>? impacts = null)
    {
        // Scroll down within the scroll region
        // When DECLRMM is enabled, only scroll within left/right margins
        int leftCol = _declrmm ? _marginLeft : 0;
        int rightCol = _declrmm ? _marginRight : _width - 1;
        
        // First, release Sixel data from the bottom row of the region (being scrolled off)
        for (int x = leftCol; x <= rightCol; x++)
        {
            _screenBuffer[_scrollBottom, x].TrackedSixel?.Release();
        }
        
        // Shift rows down within the scroll region
        // All affected cells need to be recorded as impacts
        for (int y = _scrollBottom; y > _scrollTop; y--)
        {
            for (int x = leftCol; x <= rightCol; x++)
            {
                var cellFromAbove = _screenBuffer[y - 1, x];
                SetCell(y, x, cellFromAbove, impacts);
            }
        }
        
        // Clear the top row of the scroll region (within margins)
        var eraseCell = CreateEraseCell();
        for (int x = leftCol; x <= rightCol; x++)
        {
            SetCell(_scrollTop, x, eraseCell, impacts);
        }

        // Scroll KGP placements down within the scroll region
        if (_kgpPlacements.Count > 0)
        {
            for (int i = _kgpPlacements.Count - 1; i >= 0; i--)
            {
                var p = _kgpPlacements[i];
                if (p.Row >= _scrollTop && p.Row <= _scrollBottom)
                {
                    p.Row++;
                    if (p.Row > _scrollBottom)
                    {
                        _kgpPlacements.RemoveAt(i);
                    }
                }
            }
        }
    }
    
    private void CaptureRowToScrollback(int row)
    {
        var cells = new TerminalCell[_width];
        for (int x = 0; x < _width; x++)
        {
            cells[x] = _screenBuffer[row, x];
        }

        var timestamp = _timeProvider.GetUtcNow();
        _scrollbackBuffer!.Push(cells, _width, timestamp);
        _scrollbackCallback?.Invoke(new ScrollbackRowEventArgs(this, cells, _width, timestamp));
    }
    
    private void InsertLines(int count, List<CellImpact>? impacts = null)
    {
        // Insert blank lines at cursor position within scroll region
        // Lines pushed off the bottom of the scroll region are lost
        // When DECLRMM is enabled, only affect columns within left/right margins
        
        // IL resets pending wrap and moves cursor to left margin
        _pendingWrap = false;
        _cursorX = _declrmm ? _marginLeft : 0;
        
        // No-op if cursor is outside the scroll region
        if (_cursorY < _scrollTop || _cursorY > _scrollBottom)
            return;
        
        var bottom = _scrollBottom;
        count = Math.Min(count, bottom - _cursorY + 1);
        int leftCol = _declrmm ? _marginLeft : 0;
        int rightCol = _declrmm ? _marginRight : _width - 1;
        
        for (int i = 0; i < count; i++)
        {
            // Release Sixel data from the bottom row of scroll region (being pushed off)
            for (int x = leftCol; x <= rightCol; x++)
            {
                _screenBuffer[bottom, x].TrackedSixel?.Release();
            }
            
            // Shift lines down from cursor position to bottom of scroll region
            for (int y = bottom; y > _cursorY; y--)
            {
                for (int x = leftCol; x <= rightCol; x++)
                {
                    var cellFromAbove = _screenBuffer[y - 1, x];
                    SetCell(y, x, cellFromAbove, impacts);
                }
            }
            
            // Clear the line at cursor position (within margins)
            var eraseCell = CreateEraseCell();
            for (int x = leftCol; x <= rightCol; x++)
            {
                SetCell(_cursorY, x, eraseCell, impacts);
            }
        }
    }
    
    private void DeleteLines(int count, List<CellImpact>? impacts = null)
    {
        // Delete lines at cursor position within scroll region
        // Blank lines are inserted at the bottom of the scroll region
        // When DECLRMM is enabled, only affect columns within left/right margins
        
        // DL resets pending wrap and moves cursor to left margin
        _pendingWrap = false;
        _cursorX = _declrmm ? _marginLeft : 0;
        
        // No-op if cursor is outside the scroll region
        if (_cursorY < _scrollTop || _cursorY > _scrollBottom)
            return;
        
        var bottom = _scrollBottom;
        count = Math.Min(count, bottom - _cursorY + 1);
        int leftCol = _declrmm ? _marginLeft : 0;
        int rightCol = _declrmm ? _marginRight : _width - 1;
        
        for (int i = 0; i < count; i++)
        {
            // Release Sixel data from the line being deleted
            for (int x = leftCol; x <= rightCol; x++)
            {
                _screenBuffer[_cursorY, x].TrackedSixel?.Release();
            }
            
            // Shift lines up from cursor position to bottom of scroll region
            for (int y = _cursorY; y < bottom; y++)
            {
                for (int x = leftCol; x <= rightCol; x++)
                {
                    var cellFromBelow = _screenBuffer[y + 1, x];
                    SetCell(y, x, cellFromBelow, impacts);
                }
            }
            
            // Clear the bottom line of the scroll region (within margins)
            var eraseCell = CreateEraseCell();
            for (int x = leftCol; x <= rightCol; x++)
            {
                SetCell(bottom, x, eraseCell, impacts);
            }
        }
        
        // Clean up wide char orphans at margin boundaries after line operations.
        // When DECLRMM is enabled, lines are shifted within [leftCol..rightCol] only.
        // Wide chars straddling these boundaries get split and must be replaced with blanks.
        if (_declrmm)
        {
            var eraseCleanup = CreateEraseCell();
            for (int y = _cursorY; y <= bottom; y++)
            {
                // Left boundary: if leftCol has a continuation cell (orphaned from
                // a leading cell outside the margin), blank it.
                if (leftCol > 0 && _screenBuffer[y, leftCol].Character == "")
                {
                    SetCell(y, leftCol, eraseCleanup, impacts);
                }
                
                // Right boundary: if rightCol has a wide char whose continuation
                // at rightCol+1 is NOT a continuation (was not shifted), the wide
                // char is broken → blank the leading cell.
                if (rightCol < _width - 1)
                {
                    var ch = _screenBuffer[y, rightCol].Character;
                    if (ch.Length > 0 && ch != " " && ch != ""
                        && DisplayWidth.GetGraphemeWidth(ch) > 1)
                    {
                        // Check if continuation cell is intact
                        if (_screenBuffer[y, rightCol + 1].Character != "")
                        {
                            // Continuation was NOT preserved → broken wide char, blank it
                            SetCell(y, rightCol, eraseCleanup, impacts);
                        }
                    }
                    // Also clean orphaned continuation just past right margin
                    if (_screenBuffer[y, rightCol + 1].Character == "")
                    {
                        SetCell(y, rightCol + 1, eraseCleanup, impacts);
                    }
                }
                
                // If cell just before leftCol is a wide char whose continuation
                // (at leftCol) was overwritten by the shift, blank the orphaned leading cell.
                if (leftCol > 0)
                {
                    var prevCh = _screenBuffer[y, leftCol - 1].Character;
                    if (prevCh.Length > 0 && prevCh != " " && prevCh != ""
                        && DisplayWidth.GetGraphemeWidth(prevCh) > 1
                        && _screenBuffer[y, leftCol].Character != "")
                    {
                        // Leading cell's continuation was overwritten → blank it
                        SetCell(y, leftCol - 1, eraseCleanup, impacts);
                    }
                }
            }
        }
    }
    
    private void DeleteCharacters(int count, List<CellImpact>? impacts = null)
    {
        // DCH resets pending wrap per ECMA-48
        _pendingWrap = false;
        
        // Per ECMA-48, DCH parameter defaults to 1 when 0 or omitted
        if (count <= 0)
            count = 1;
        
        // Delete n characters at cursor, shifting remaining characters left
        // Blank characters are inserted at the right margin
        // When DECLRMM is enabled, operations are bounded by left/right margins
        int rightEdge = _declrmm ? _marginRight + 1 : _width;
        count = Math.Min(count, rightEdge - _cursorX);
        
        // Handle wide char splitting at cursor position:
        // If cursor is on a continuation cell, clear the leading cell
        if (_cursorX > 0 && _screenBuffer[_cursorY, _cursorX].Character == "")
        {
            var erase = CreateEraseCell();
            SetCell(_cursorY, _cursorX - 1, erase, impacts);
            SetCell(_cursorY, _cursorX, erase, impacts);
        }
        
        // Handle wide char splitting at the boundary after deletion:
        // If the cell at _cursorX + count is a continuation cell, clear its leading cell
        int shiftStart = _cursorX + count;
        if (shiftStart < rightEdge && _screenBuffer[_cursorY, shiftStart].Character == ""
            && shiftStart > 0)
        {
            var erase = CreateEraseCell();
            SetCell(_cursorY, shiftStart - 1, erase, impacts);
            SetCell(_cursorY, shiftStart, erase, impacts);
        }
        
        for (int x = _cursorX; x < rightEdge - count; x++)
        {
            var cellFromRight = _screenBuffer[_cursorY, x + count];
            SetCell(_cursorY, x, cellFromRight, impacts);
        }
        
        // Fill the right edge with blanks
        var eraseCell = CreateEraseCell();
        for (int x = rightEdge - count; x < rightEdge; x++)
        {
            SetCell(_cursorY, x, eraseCell, impacts);
        }
        
        // Clean up wide char orphans after shift and erasure:
        // 1. If a wide char's leading cell was shifted into the last position before
        //    the erased zone, its continuation was erased → blank the leading cell too.
        int lastShifted = rightEdge - count - 1;
        if (lastShifted >= _cursorX && lastShifted < _width)
        {
            var ch = _screenBuffer[_cursorY, lastShifted].Character;
            if (ch.Length > 0 && ch != " " && ch != "" && DisplayWidth.GetGraphemeWidth(ch) > 1)
            {
                SetCell(_cursorY, lastShifted, eraseCell, impacts);
            }
        }
        
        // 2. If a wide char's continuation cell is just past the right edge boundary
        //    (outside margin), it was orphaned by the leading cell being erased → clear it.
        if (_declrmm && rightEdge < _width)
        {
            if (_screenBuffer[_cursorY, rightEdge].Character == "")
            {
                SetCell(_cursorY, rightEdge, eraseCell, impacts);
            }
        }
    }
    
    private void InsertCharacters(int count, List<CellImpact>? impacts = null)
    {
        // ICH resets pending wrap per ECMA-48
        _pendingWrap = false;
        
        if (count == 0)
            return;
        
        // Insert n blank characters at cursor, shifting existing characters right
        // Characters pushed off the right margin are lost
        // When DECLRMM is enabled, operations are bounded by left/right margins
        int rightEdge = _declrmm ? _marginRight + 1 : _width;
        count = Math.Min(count, rightEdge - _cursorX);
        
        if (count <= 0)
            return;
        
        // Handle wide char splitting at cursor position:
        // If cursor is on a continuation cell, clear both the leading cell and the continuation
        if (_cursorX > 0 && _screenBuffer[_cursorY, _cursorX].Character == "")
        {
            var eraseCell = CreateEraseCell();
            SetCell(_cursorY, _cursorX - 1, eraseCell, impacts);
            SetCell(_cursorY, _cursorX, eraseCell, impacts);
        }
        
        // Handle wide char splitting at the right edge:
        // If the cell being pushed off is the leading half of a wide char,
        // its continuation cell (now orphaned) should be cleared
        int shiftBoundary = rightEdge - count;
        if (shiftBoundary >= 0 && shiftBoundary < rightEdge)
        {
            var cellAtBoundary = _screenBuffer[_cursorY, shiftBoundary];
            if (cellAtBoundary.Character.Length > 0 && cellAtBoundary.Character != " " 
                && DisplayWidth.GetGraphemeWidth(cellAtBoundary.Character) > 1
                && shiftBoundary + 1 < rightEdge)
            {
                // Wide char at boundary — continuation will be orphaned, clear the wide char
                var eraseCell = CreateEraseCell();
                SetCell(_cursorY, shiftBoundary, eraseCell, impacts);
                SetCell(_cursorY, shiftBoundary + 1, eraseCell, impacts);
            }
            // Check if boundary is on a continuation cell — its leading half stays, clear continuation
            if (shiftBoundary > 0 && _screenBuffer[_cursorY, shiftBoundary].Character == ""
                && _screenBuffer[_cursorY, shiftBoundary - 1].Character.Length > 0
                && _screenBuffer[_cursorY, shiftBoundary - 1].Character != " ")
            {
                var eraseCell = CreateEraseCell();
                SetCell(_cursorY, shiftBoundary - 1, eraseCell, impacts);
                SetCell(_cursorY, shiftBoundary, eraseCell, impacts);
            }
        }
        
        // Shift characters right
        for (int x = rightEdge - 1; x >= _cursorX + count; x--)
        {
            var cellFromLeft = _screenBuffer[_cursorY, x - count];
            SetCell(_cursorY, x, cellFromLeft, impacts);
        }
        
        // Insert blanks at cursor position
        var eraseCell2 = CreateEraseCell();
        for (int x = _cursorX; x < _cursorX + count && x < rightEdge; x++)
        {
            SetCell(_cursorY, x, eraseCell2, impacts);
        }
    }
    
    private void EraseCharacters(int count, List<CellImpact>? impacts = null)
    {
        // ECH resets pending wrap per ECMA-48 / Ghostty conformance
        _pendingWrap = false;
        
        // ECH respects ISO protection mode (same as ED/EL)
        bool respectProtection = _protectedMode == ProtectedMode.Iso;
        
        // Erase n characters from cursor without moving cursor or shifting
        // When DECLRMM is enabled, operations are bounded by right margin
        int rightEdge = _declrmm ? _marginRight + 1 : _width;
        count = Math.Min(count, rightEdge - _cursorX);
        
        // Handle wide char splitting: if cursor is on a continuation cell, clear leading too
        if (_cursorX > 0 && _screenBuffer[_cursorY, _cursorX].Character == "")
        {
            if (!respectProtection || !IsProtectedCell(_cursorY, _cursorX - 1))
            {
                var erase = CreateEraseCell();
                SetCell(_cursorY, _cursorX - 1, erase, impacts);
            }
        }
        
        // Handle wide char splitting at the end of erase range:
        // If the cell just past the erase range is a continuation cell, its leading cell
        // was erased, so clear the orphaned continuation cell too.
        int eraseEnd = _cursorX + count;
        if (eraseEnd < rightEdge && _screenBuffer[_cursorY, eraseEnd].Character == ""
            && eraseEnd > 0)
        {
            if (!respectProtection || !IsProtectedCell(_cursorY, eraseEnd))
            {
                var orphanErase = CreateEraseCell();
                SetCell(_cursorY, eraseEnd, orphanErase, impacts);
            }
        }
        
        var eraseCell = CreateEraseCell();
        for (int x = _cursorX; x < _cursorX + count; x++)
        {
            if (respectProtection && IsProtectedCell(_cursorY, x))
                continue;
            SetCell(_cursorY, x, eraseCell, impacts);
        }
    }
    
    private void RepeatLastCharacter(int count, List<CellImpact>? impacts)
    {
        // Repeat the last printed graphic character n times
        if (!_hasLastPrintedCell || string.IsNullOrEmpty(_lastPrintedCell.Character))
            return;
            
        var graphemeWidth = DisplayWidth.GetGraphemeWidth(_lastPrintedCell.Character);
        
        // Determine effective right margin for wrapping
        int effectiveRightMargin = _declrmm ? _marginRight : _width - 1;
        
        for (int i = 0; i < count; i++)
        {
            // Handle deferred wrap
            if (_pendingWrap)
            {
                _pendingWrap = false;
                
                // Mark the last cell of the row being left as a soft-wrap point.
                int wrapCol = _declrmm ? _marginRight : _width - 1;
                ref var wrapCell = ref _screenBuffer[_cursorY, wrapCol];
                wrapCell = wrapCell with { Attributes = wrapCell.Attributes | CellAttributes.SoftWrap };
                
                // When DECLRMM is enabled, wrap to left margin, not column 0
                _cursorX = _declrmm ? _marginLeft : 0;
                _cursorY++;
            }
            
            // Scroll if needed
            if (_cursorY >= _height)
            {
                ScrollUp(impacts);
                _cursorY = _height - 1;
            }
            
            if (_cursorX < _width && _cursorY < _height)
            {
                var sequence = ++_writeSequence;
                var writtenAt = _timeProvider.GetUtcNow();
                
                // Create a new cell with the same visual properties but new timing
                var cell = new TerminalCell(
                    _lastPrintedCell.Character, 
                    _lastPrintedCell.Foreground, 
                    _lastPrintedCell.Background, 
                    _lastPrintedCell.Attributes,
                    sequence, 
                    writtenAt, 
                    TrackedSixel: null, 
                    TrackedHyperlink: null,
                    _lastPrintedCell.UnderlineColor,
                    _lastPrintedCell.UnderlineStyle);
                SetCell(_cursorY, _cursorX, cell, impacts);
                
                // Handle wide characters
                for (int w = 1; w < graphemeWidth && _cursorX + w < _width; w++)
                {
                    SetCell(_cursorY, _cursorX + w, new TerminalCell(
                        "", _lastPrintedCell.Foreground, _lastPrintedCell.Background, _lastPrintedCell.Attributes,
                        sequence, writtenAt, TrackedSixel: null, TrackedHyperlink: null,
                        _lastPrintedCell.UnderlineColor, _lastPrintedCell.UnderlineStyle), impacts);
                }
                
                _cursorX += graphemeWidth;
                
                // Handle pending wrap at right margin
                if (_cursorX > effectiveRightMargin)
                {
                    _cursorX = effectiveRightMargin;
                    _pendingWrap = true;
                }
            }
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
    private void ProcessSixelData(string sixelPayload, List<CellImpact>? impacts)
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
                        sequence, writtenAt, sixelData), impacts);
                }
                else
                {
                    // Continuation cells - just mark as Sixel, no tracked object
                    // (The origin cell owns the reference)
                    SetCell(y, x, new TerminalCell(
                        "", _currentForeground, _currentBackground,
                        _currentAttributes | CellAttributes.Sixel,
                        sequence, writtenAt), impacts);
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

    /// <summary>
    /// Tries to parse an OSC (Operating System Command) sequence.
    /// Format: ESC ] command ; params ; payload ST
    /// where ST is either ESC \ or BEL (\x07)
    /// </summary>
    private static bool TryParseOscSequence(string text, int start, out int consumed, out string command, out string parameters, out string payload)
    {
        consumed = 0;
        command = "";
        parameters = "";
        payload = "";

        // Check for OSC start: ESC ] (0x1b 0x5d) or 0x9D
        bool isOscStart = false;
        int dataStart = start;

        if (start + 1 < text.Length && text[start] == '\x1b' && text[start + 1] == ']')
        {
            isOscStart = true;
            dataStart = start + 2;
        }
        else if (text[start] == '\x9d')
        {
            isOscStart = true;
            dataStart = start + 1;
        }

        if (!isOscStart)
            return false;

        // Find the ST (String Terminator): ESC \ (0x1b 0x5c), BEL (\x07), or 0x9C
        int dataEnd = -1;
        int stLength = 0;
        for (int j = dataStart; j < text.Length; j++)
        {
            if (j + 1 < text.Length && text[j] == '\x1b' && text[j + 1] == '\\')
            {
                dataEnd = j;
                stLength = 2; // ESC \
                break;
            }
            else if (text[j] == '\x07')
            {
                dataEnd = j;
                stLength = 1; // BEL
                break;
            }
            else if (text[j] == '\x9c')
            {
                dataEnd = j;
                stLength = 1; // 0x9C
                break;
            }
        }

        if (dataEnd < 0)
            return false; // No terminator found

        consumed = dataEnd + stLength - start;

        // Extract the OSC data (between ESC ] and ST)
        var oscData = text.Substring(dataStart, dataEnd - dataStart);

        // Parse OSC data: command ; params ; payload
        // For OSC 8: 8 ; params ; URI
        var firstSemicolon = oscData.IndexOf(';');
        if (firstSemicolon < 0)
        {
            // No semicolons - entire thing is command
            command = oscData;
            return true;
        }

        command = oscData.Substring(0, firstSemicolon);
        
        var secondSemicolon = oscData.IndexOf(';', firstSemicolon + 1);
        if (secondSemicolon < 0)
        {
            // Only one semicolon - rest is payload
            payload = oscData.Substring(firstSemicolon + 1);
            return true;
        }

        parameters = oscData.Substring(firstSemicolon + 1, secondSemicolon - firstSemicolon - 1);
        payload = oscData.Substring(secondSemicolon + 1);

        return true;
    }

    /// <summary>
    /// Processes an OSC sequence, handling title sequences (0/1/2/22/23) and hyperlinks (8).
    /// </summary>
    /// <remarks>
    /// <para>Supported OSC sequences:</para>
    /// <list type="bullet">
    ///   <item>OSC 0 - Set icon name AND window title</item>
    ///   <item>OSC 1 - Set icon name only</item>
    ///   <item>OSC 2 - Set window title only</item>
    ///   <item>OSC 8 - Hyperlinks</item>
    ///   <item>OSC 22 - Push current title/icon onto stack, optionally set new values</item>
    ///   <item>OSC 23 - Pop title/icon from stack and restore</item>
    /// </list>
    /// <para>
    /// OSC 0/1/2 do NOT affect the title stack - they only modify current values.
    /// The stack is independent: push saves current state, pop restores regardless of
    /// what changes were made with 0/1/2 in between.
    /// </para>
    /// </remarks>
    private void ProcessOscSequence(string command, string parameters, string payload)
    {
        switch (command)
        {
            case "0":
                // OSC 0: Set both icon name and window title
                SetIconName(payload);
                SetWindowTitle(payload);
                break;
                
            case "1":
                // OSC 1: Set icon name only
                SetIconName(payload);
                break;
                
            case "2":
                // OSC 2: Set window title only
                SetWindowTitle(payload);
                break;
                
            case "8":
                // OSC 8: Hyperlinks
                ProcessOsc8Hyperlink(parameters, payload);
                break;
                
            case "22":
                // OSC 22: Push title onto stack
                // Format: OSC 22 ; Pt ST or OSC 22 ; 0 ST or OSC 22 ; 1 ST or OSC 22 ; 2 ST
                // If Pt is "0", "1", or "2", it indicates which to push (icon, title, or both)
                // For simplicity, we push both (XTerm behavior)
                PushTitleStack(payload);
                break;
                
            case "23":
                // OSC 23: Pop title from stack
                // Format: OSC 23 ; Pt ST
                // Similar to 22, Pt can specify which to pop
                PopTitleStack(payload);
                break;
        }
    }
    
    /// <summary>
    /// Processes OSC 8 hyperlink sequences.
    /// </summary>
    private void ProcessOsc8Hyperlink(string parameters, string payload)
    {
        // Empty payload means end of hyperlink
        if (string.IsNullOrEmpty(payload))
        {
            // Release current hyperlink if any
            if (_currentHyperlink is not null)
            {
                _currentHyperlink.Release();
                _currentHyperlink = null;
            }
        }
        else
        {
            // Start a new hyperlink
            // Release any previous hyperlink first
            if (_currentHyperlink is not null)
            {
                _currentHyperlink.Release();
            }
            
            // Create or get existing hyperlink
            _currentHyperlink = _trackedObjects.GetOrCreateHyperlink(payload, parameters);
        }
    }
    
    /// <summary>
    /// Sets the window title and fires the WindowTitleChanged event if changed.
    /// </summary>
    private void SetWindowTitle(string title)
    {
        if (_windowTitle != title)
        {
            _windowTitle = title;
            WindowTitleChanged?.Invoke(title);
        }
    }
    
    /// <summary>
    /// Sets the icon name and fires the IconNameChanged event if changed.
    /// </summary>
    private void SetIconName(string name)
    {
        if (_iconName != name)
        {
            _iconName = name;
            IconNameChanged?.Invoke(name);
        }
    }
    
    /// <summary>
    /// Pushes the current title and icon name onto the stack (OSC 22).
    /// </summary>
    /// <param name="payload">The payload from the OSC sequence. Can specify what to push:
    /// "0" = icon name, "1" = window title, "2" or empty = both.
    /// Can also contain a new title to set after pushing.</param>
    private void PushTitleStack(string payload)
    {
        // XTerm behavior: push current state, then optionally set new values
        // The payload can be:
        // - Empty: push both
        // - "0": push icon name only (but we still push both for simplicity)
        // - "1": push title only (but we still push both for simplicity)  
        // - "2": push both (explicit)
        // - A string: push both, then set title to this string
        
        // Always push current state
        _titleStack.Push((_windowTitle, _iconName));
        
        // If payload is not empty and not just a mode specifier, treat it as a new title
        if (!string.IsNullOrEmpty(payload) && payload != "0" && payload != "1" && payload != "2")
        {
            // Set new title (some terminals support OSC 22 ; text ST to push and set)
            SetWindowTitle(payload);
            SetIconName(payload);
        }
    }
    
    /// <summary>
    /// Pops the title and icon name from the stack (OSC 23).
    /// </summary>
    /// <param name="payload">The payload from the OSC sequence. Can specify what to pop:
    /// "0" = icon name, "1" = window title, "2" or empty = both.</param>
    private void PopTitleStack(string payload)
    {
        if (_titleStack.Count == 0)
        {
            // Nothing to pop - some terminals reset to default, we just ignore
            return;
        }
        
        var (savedTitle, savedIconName) = _titleStack.Pop();
        
        // Restore based on payload (for simplicity, we always restore both)
        // payload "0" = restore icon only, "1" = restore title only, "2" or empty = both
        SetWindowTitle(savedTitle);
        SetIconName(savedIconName);
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
            '\0' => new Hex1bKeyEvent(Hex1bKey.Spacebar, c, Hex1bModifiers.Control), // Ctrl+Space sends NUL
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
            '+' or '=' => new Hex1bKeyEvent(Hex1bKey.OemPlus, c, Hex1bModifiers.None),
            '-' => new Hex1bKeyEvent(Hex1bKey.OemMinus, c, Hex1bModifiers.None),
            ',' => new Hex1bKeyEvent(Hex1bKey.OemComma, c, Hex1bModifiers.None),
            '.' => new Hex1bKeyEvent(Hex1bKey.OemPeriod, c, Hex1bModifiers.None),
            '/' or '?' => new Hex1bKeyEvent(Hex1bKey.OemQuestion, c, Hex1bModifiers.None),
            _ when !char.IsControl(c) => new Hex1bKeyEvent(Hex1bKey.None, c, Hex1bModifiers.None),
            _ => null
        };
    }

    /// <summary>
    /// Parses an Alt+key combination (ESC followed by a character).
    /// Returns null if the character cannot be an Alt+key combination.
    /// </summary>
    private static Hex1bKeyEvent? ParseAltKeyInput(char c)
    {
        // Alt+letter (lowercase: Alt+F sends ESC f)
        if (c >= 'a' && c <= 'z')
        {
            return new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'a'))), c, Hex1bModifiers.Alt);
        }
        
        // Alt+letter (uppercase: Alt+Shift+F sends ESC F)
        if (c >= 'A' && c <= 'Z')
        {
            return new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'A'))), c, Hex1bModifiers.Alt | Hex1bModifiers.Shift);
        }
        
        // Alt+number (Alt+1 sends ESC 1)
        if (c >= '0' && c <= '9')
        {
            return new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.D0 + (c - '0'))), c, Hex1bModifiers.Alt);
        }
        
        // Unknown - don't treat as Alt+key
        return null;
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
            'u' => ParseFixtermKeycode(param1), // CSI u (fixterm/Kitty protocol)
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

    /// <summary>
    /// Maps a CSI u (fixterm/Kitty protocol) Unicode codepoint to a Hex1bKey.
    /// Format: ESC [ codepoint ; modifiers u
    /// </summary>
    private static Hex1bKey ParseFixtermKeycode(int codepoint)
    {
        return codepoint switch
        {
            9 => Hex1bKey.Tab,
            13 => Hex1bKey.Enter,
            27 => Hex1bKey.Escape,
            32 => Hex1bKey.Spacebar,
            127 => Hex1bKey.Backspace,
            >= 'a' and <= 'z' => KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (codepoint - 'a'))),
            >= 'A' and <= 'Z' => KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (codepoint - 'A'))),
            >= '0' and <= '9' => KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.D0 + (codepoint - '0'))),
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

        // Complete any active paste context
        if (_activePasteContext != null)
        {
            _activePasteContext.Complete();
            _activePasteContext = null;
            _inBracketedPaste = false;
        }

        // Release all scrollback buffer tracked object references
        _scrollbackBuffer?.Clear();

        // Notify filters of session end (fire-and-forget from sync Dispose)
        var elapsed = _timeProvider.GetUtcNow() - _sessionStart;
        _ = NotifyWorkloadFiltersSessionEndAsync(elapsed);
        _ = NotifyPresentationFiltersSessionEndAsync(elapsed);

        if (_presentation != null)
        {
            // Write mouse disable sequences and screen restore DIRECTLY to presentation
            // This ensures they're written before raw mode is exited, avoiding race conditions
            var exitSequences = Input.MouseParser.DisableMouseTracking + 
                "\x1b[?2004l" + // Disable bracketed paste mode
                "\x1b[?25h" +   // Show cursor
                "\x1b[?1049l";  // Exit alternate screen
            _presentation.WriteOutputAsync(System.Text.Encoding.UTF8.GetBytes(exitSequences), default).AsTask().GetAwaiter().GetResult();
            _presentation.FlushAsync().AsTask().GetAwaiter().GetResult();
            
            _presentation.ExitRawModeAsync().AsTask().GetAwaiter().GetResult();
            _presentation.Resized -= OnPresentationResized;
            _presentation.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        _workload.DisposeAsync().AsTask().GetAwaiter().GetResult();

        _escapeFlushTimer?.Dispose();

        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Complete any active paste context
        if (_activePasteContext != null)
        {
            _activePasteContext.Complete();
            _activePasteContext = null;
            _inBracketedPaste = false;
        }

        // Release all scrollback buffer tracked object references
        _scrollbackBuffer?.Clear();

        // Notify filters of session end
        var elapsed = _timeProvider.GetUtcNow() - _sessionStart;
        await NotifyWorkloadFiltersSessionEndAsync(elapsed);
        await NotifyPresentationFiltersSessionEndAsync(elapsed);

        if (_presentation != null)
        {
            // Write mouse disable sequences and screen restore DIRECTLY to presentation
            // This ensures they're written before raw mode is exited, avoiding race conditions
            // where mouse events could leak to the shell after app exit
            var exitSequences = Input.MouseParser.DisableMouseTracking + 
                "\x1b[?2004l" + // Disable bracketed paste mode
                "\x1b[0m" +     // Reset text attributes (prevents inverted text from leaking)
                "\x1b[?25h" +   // Show cursor
                "\x1b[?1049l";  // Exit alternate screen
            await _presentation.WriteOutputAsync(System.Text.Encoding.UTF8.GetBytes(exitSequences), default);
            await _presentation.FlushAsync();
            
            // Exit raw mode before disposing
            await _presentation.ExitRawModeAsync();
            _presentation.Resized -= OnPresentationResized;
            await _presentation.DisposeAsync();
        }

        await _workload.DisposeAsync();

        _escapeFlushTimer?.Dispose();

        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    // === Filter Notification Helpers ===

    private TimeSpan GetElapsed() => _timeProvider.GetUtcNow() - _sessionStart;

    private async ValueTask NotifyWorkloadFiltersSessionStartAsync(CancellationToken ct = default)
    {
        foreach (var filter in _workloadFilters)
        {
            ct.ThrowIfCancellationRequested();
            await filter.OnSessionStartAsync(_width, _height, _sessionStart, ct);
        }
    }

    private async ValueTask NotifyWorkloadFiltersSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
    {
        foreach (var filter in _workloadFilters)
        {
            ct.ThrowIfCancellationRequested();
            await filter.OnSessionEndAsync(elapsed, ct);
        }
    }

    private async ValueTask NotifyWorkloadFiltersOutputAsync(IReadOnlyList<AnsiToken> tokens, CancellationToken ct = default)
    {
        if (_workloadFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _workloadFilters)
        {
            ct.ThrowIfCancellationRequested();
            await filter.OnOutputAsync(tokens, elapsed, ct);
        }
    }

    private async ValueTask NotifyWorkloadFiltersFrameCompleteAsync(CancellationToken ct = default)
    {
        if (_workloadFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _workloadFilters)
        {
            ct.ThrowIfCancellationRequested();
            await filter.OnFrameCompleteAsync(elapsed, ct);
        }
    }

    private async ValueTask NotifyWorkloadFiltersInputAsync(IReadOnlyList<AnsiToken> tokens, CancellationToken ct = default)
    {
        if (_workloadFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _workloadFilters)
        {
            ct.ThrowIfCancellationRequested();
            await filter.OnInputAsync(tokens, elapsed, ct);
        }
    }

    private async ValueTask NotifyWorkloadFiltersResizeAsync(int width, int height, CancellationToken ct = default)
    {
        if (_workloadFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _workloadFilters)
        {
            ct.ThrowIfCancellationRequested();
            await filter.OnResizeAsync(width, height, elapsed, ct);
        }
    }

    private async ValueTask NotifyPresentationFiltersSessionStartAsync(CancellationToken ct = default)
    {
        foreach (var filter in _presentationFilters)
        {
            ct.ThrowIfCancellationRequested();
            await filter.OnSessionStartAsync(_width, _height, _sessionStart, ct);
        }
    }

    private async ValueTask NotifyPresentationFiltersSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
    {
        foreach (var filter in _presentationFilters)
        {
            ct.ThrowIfCancellationRequested();
            await filter.OnSessionEndAsync(elapsed, ct);
        }
    }

    private async ValueTask<IReadOnlyList<AnsiToken>> NotifyPresentationFiltersOutputAsync(IReadOnlyList<AppliedToken> appliedTokens, CancellationToken ct = default)
    {
        if (_presentationFilters.Count == 0)
        {
            return appliedTokens.Select(at => at.Token).ToList();
        }
        var elapsed = GetElapsed();
        var currentAppliedTokens = appliedTokens;
        IReadOnlyList<AnsiToken> resultTokens = appliedTokens.Select(at => at.Token).ToList();
        foreach (var filter in _presentationFilters)
        {
            ct.ThrowIfCancellationRequested();
            resultTokens = await filter.OnOutputAsync(currentAppliedTokens, elapsed, ct);
            // For subsequent filters, wrap the result tokens as AppliedTokens with no cell impacts
            // (since they may have been transformed by the previous filter)
            currentAppliedTokens = resultTokens.Select(t => AppliedToken.WithNoCellImpacts(t, 0, 0, 0, 0)).ToList();
        }
        return resultTokens;
    }

    private async ValueTask NotifyPresentationFiltersInputAsync(IReadOnlyList<AnsiToken> tokens, CancellationToken ct = default)
    {
        if (_presentationFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _presentationFilters)
        {
            ct.ThrowIfCancellationRequested();
            await filter.OnInputAsync(tokens, elapsed, ct);
        }
    }

    private async ValueTask NotifyPresentationFiltersResizeAsync(int width, int height, CancellationToken ct = default)
    {
        if (_presentationFilters.Count == 0) return;
        var elapsed = GetElapsed();
        foreach (var filter in _presentationFilters)
        {
            ct.ThrowIfCancellationRequested();
            await filter.OnResizeAsync(width, height, elapsed, ct);
        }
    }
    
    /// <summary>
    /// Extracts any incomplete escape sequence from the end of the text.
    /// Returns (completeText, incompleteSequence) where incompleteSequence
    /// should be prepended to the next chunk of data.
    /// </summary>
    private static (string completeText, string incompleteSequence) ExtractIncompleteEscapeSequence(string text)
    {
        if (string.IsNullOrEmpty(text))
            return (text, "");

        var incompleteStart = FindTrailingIncompleteEscapeSequenceStart(text);
        return incompleteStart < 0
            ? (text, "")
            : (text[..incompleteStart], text[incompleteStart..]);
    }

    private static int FindTrailingIncompleteEscapeSequenceStart(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (!IsEscapeSequenceIntroducer(text[i]))
                continue;

            if (TryFindEscapeSequenceEnd(text, i, out var endExclusive))
            {
                i = endExclusive - 1;
                continue;
            }

            return i;
        }

        return -1;
    }

    private static bool IsEscapeSequenceIntroducer(char c)
        => c is '\x1b' or '\x90' or '\x9d' or '\x9f';

    private static bool TryFindEscapeSequenceEnd(string text, int start, out int endExclusive)
    {
        endExclusive = start + 1;

        return text[start] switch
        {
            '\x90' => TryFindStringTerminator(text, start + 1, allowBellTerminator: false, out endExclusive),
            '\x9d' => TryFindStringTerminator(text, start + 1, allowBellTerminator: true, out endExclusive),
            '\x9f' => TryFindStringTerminator(text, start + 1, allowBellTerminator: false, out endExclusive),
            '\x1b' => TryFindEscapeSequenceEndAfterEsc(text, start, out endExclusive),
            _ => true
        };
    }

    private static bool TryFindEscapeSequenceEndAfterEsc(string text, int start, out int endExclusive)
    {
        endExclusive = start + 1;

        if (start + 1 >= text.Length)
            return false;

        var second = text[start + 1];
        switch (second)
        {
            case '[':
                return TryFindCsiTerminator(text, start + 2, out endExclusive);

            case ']':
                return TryFindStringTerminator(text, start + 2, allowBellTerminator: true, out endExclusive);

            case 'P':
            case '_':
                return TryFindStringTerminator(text, start + 2, allowBellTerminator: false, out endExclusive);

            case 'O':
            case 'N':
            case '(':
            case ')':
            case '*':
            case '+':
            case '#':
                if (start + 2 >= text.Length)
                    return false;

                endExclusive = start + 3;
                return true;

            default:
                endExclusive = start + 2;
                return true;
        }
    }

    private static bool TryFindCsiTerminator(string text, int start, out int endExclusive)
    {
        for (int i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c >= '@' && c <= '~')
            {
                endExclusive = i + 1;
                return true;
            }
        }

        endExclusive = text.Length;
        return false;
    }

    private static bool TryFindStringTerminator(string text, int start, bool allowBellTerminator, out int endExclusive)
    {
        for (int i = start; i < text.Length; i++)
        {
            if (allowBellTerminator && text[i] == '\x07')
            {
                endExclusive = i + 1;
                return true;
            }

            if (text[i] == '\x9c')
            {
                endExclusive = i + 1;
                return true;
            }

            if (text[i] == '\x1b')
            {
                if (i + 1 >= text.Length)
                {
                    endExclusive = i;
                    return false;
                }

                if (text[i + 1] == '\\')
                {
                    endExclusive = i + 2;
                    return true;
                }
            }
        }

        endExclusive = text.Length;
        return false;
    }

    // ---- KGP (Kitty Graphics Protocol) ----

    /// <summary>
    /// Gets the KGP image store for testing and inspection.
    /// </summary>
    internal KgpImageStore KgpImageStore => _kgpImageStore;

    /// <summary>
    /// Gets the active KGP placements for testing and inspection.
    /// </summary>
    internal IReadOnlyList<KgpPlacement> KgpPlacements
    {
        get { lock (_bufferLock) return _kgpPlacements.ToList(); }
    }

    /// <summary>
    /// Processes a KGP (Kitty Graphics Protocol) command.
    /// </summary>
    private void ProcessKgpCommand(KgpToken token)
    {
        if (!Capabilities.SupportsKgp)
            return;

        var command = KgpCommand.Parse(token.ControlData);

        switch (command.Action)
        {
            case KgpAction.Transmit:
                ProcessKgpTransmit(command, token.Payload);
                break;
            case KgpAction.TransmitAndDisplay:
                ProcessKgpTransmitAndDisplay(command, token.Payload);
                break;
            case KgpAction.Query:
                ProcessKgpQuery(command, token.Payload);
                break;
            case KgpAction.Put:
                ProcessKgpPut(command);
                break;
            case KgpAction.Delete:
                ProcessKgpDelete(command);
                break;
        }
    }

    private void ProcessKgpTransmit(KgpCommand command, string base64Payload)
    {
        var decodedData = DecodeKgpPayload(base64Payload);

        if (command.MoreData == 1 || _kgpImageStore.IsChunkedTransferInProgress)
        {
            var image = _kgpImageStore.ProcessChunk(command, decodedData);
            if (image is not null)
            {
                _kgpImageStore.StoreImage(image);
                SendKgpResponse(image.ImageId, image.ImageNumber, "OK", command.Quiet);
            }
            return;
        }

        // Single-chunk transmit
        var imageId = command.ImageId;
        var imageNumber = command.ImageNumber;

        if (imageId == 0 && imageNumber > 0)
        {
            imageId = _kgpImageStore.AllocateId();
        }
        else if (imageId == 0)
        {
            imageId = _kgpImageStore.AllocateId();
        }

        var expectedSize = GetExpectedKgpDataSize(command);
        if (expectedSize > 0 && decodedData.Length < expectedSize)
        {
            SendKgpResponse(imageId, imageNumber,
                $"ENODATA:Insufficient image data: {decodedData.Length} < {expectedSize}",
                command.Quiet);
            return;
        }

        var newImage = new KgpImageData(imageId, imageNumber, decodedData,
            command.Width, command.Height, command.Format);
        _kgpImageStore.StoreImage(newImage);
        SendKgpResponse(imageId, imageNumber, "OK", command.Quiet);
    }

    private void ProcessKgpTransmitAndDisplay(KgpCommand command, string base64Payload)
    {
        // First transmit (create a modified command with Transmit action)
        var transmitCmd = KgpCommand.Parse(
            $"a=t,f={(int)command.Format},s={command.Width},v={command.Height}," +
            $"i={command.ImageId},I={command.ImageNumber},m={command.MoreData}," +
            $"q={command.Quiet}" +
            (command.Compression.HasValue ? $",o={command.Compression}" : ""));
        ProcessKgpTransmit(transmitCmd, base64Payload);

        // Then place the image
        var imageId = command.ImageId;
        if (imageId == 0 && command.ImageNumber > 0)
        {
            var img = _kgpImageStore.GetImageByNumber(command.ImageNumber);
            if (img is not null)
                imageId = img.ImageId;
        }

        if (imageId > 0)
        {
            var cols = command.DisplayColumns > 0 ? (int)command.DisplayColumns : 1;
            var rows = command.DisplayRows > 0 ? (int)command.DisplayRows : 1;

            CreateKgpPlacement(imageId, command.PlacementId,
                (uint)cols, (uint)rows, command);

            if (command.CursorMovement == 0)
            {
                _cursorX = Math.Min(_cursorX + cols, _width - 1);
                for (int r = 1; r < rows; r++)
                {
                    if (_cursorY < _height - 1)
                        _cursorY++;
                }
            }
        }
    }

    private void ProcessKgpQuery(KgpCommand command, string base64Payload)
    {
        var decodedData = DecodeKgpPayload(base64Payload);
        var expectedSize = GetExpectedKgpDataSize(command);

        if (expectedSize > 0 && decodedData.Length < expectedSize)
        {
            SendKgpResponse(command.ImageId, command.ImageNumber,
                $"ENODATA:Insufficient image data: {decodedData.Length} < {expectedSize}",
                command.Quiet);
            return;
        }

        // Query succeeds but does NOT store the image
        SendKgpResponse(command.ImageId, command.ImageNumber, "OK", command.Quiet);
    }

    private void ProcessKgpPut(KgpCommand command)
    {
        var imageId = command.ImageId;
        KgpImageData? image = null;

        if (imageId > 0)
            image = _kgpImageStore.GetImageById(imageId);
        else if (command.ImageNumber > 0)
            image = _kgpImageStore.GetImageByNumber(command.ImageNumber);

        if (image is null)
        {
            SendKgpResponse(imageId, command.ImageNumber, "ENOENT:Image not found", command.Quiet);
            return;
        }

        // Create placement and move cursor
        var cols = command.DisplayColumns > 0 ? (int)command.DisplayColumns : 1;
        var rows = command.DisplayRows > 0 ? (int)command.DisplayRows : 1;

        CreateKgpPlacement(image.ImageId, command.PlacementId,
            (uint)cols, (uint)rows, command);

        if (command.CursorMovement == 0)
        {
            _cursorX = Math.Min(_cursorX + cols, _width - 1);
            for (int r = 1; r < rows; r++)
            {
                if (_cursorY < _height - 1)
                    _cursorY++;
            }
        }

        SendKgpResponse(image.ImageId, image.ImageNumber, "OK", command.Quiet);
    }

    private void ProcessKgpDelete(KgpCommand command)
    {
        switch (command.DeleteTarget)
        {
            case KgpDeleteTarget.All:
                _kgpPlacements.Clear();
                break;
            case KgpDeleteTarget.AllFreeData:
                _kgpPlacements.Clear();
                _kgpImageStore.Clear();
                break;
            case KgpDeleteTarget.ById:
                if (command.ImageId > 0)
                {
                    if (command.PlacementId > 0)
                        _kgpPlacements.RemoveAll(p => p.ImageId == command.ImageId && p.PlacementId == command.PlacementId);
                    else
                        _kgpPlacements.RemoveAll(p => p.ImageId == command.ImageId);
                }
                break;
            case KgpDeleteTarget.ByIdFreeData:
                if (command.ImageId > 0)
                {
                    if (command.PlacementId > 0)
                        _kgpPlacements.RemoveAll(p => p.ImageId == command.ImageId && p.PlacementId == command.PlacementId);
                    else
                        _kgpPlacements.RemoveAll(p => p.ImageId == command.ImageId);
                    _kgpImageStore.RemoveImage(command.ImageId);
                }
                break;
            case KgpDeleteTarget.ByNumber:
            case KgpDeleteTarget.ByNumberFreeData:
                if (command.ImageNumber > 0)
                {
                    var img = _kgpImageStore.GetImageByNumber(command.ImageNumber);
                    if (img is not null)
                        _kgpPlacements.RemoveAll(p => p.ImageId == img.ImageId);
                    _kgpImageStore.RemoveImageByNumber(command.ImageNumber);
                }
                break;
            case KgpDeleteTarget.AtCursor:
            case KgpDeleteTarget.AtCursorFreeData:
                _kgpPlacements.RemoveAll(p => p.IntersectsCell(_cursorY, _cursorX));
                break;
            case KgpDeleteTarget.AtCell:
            case KgpDeleteTarget.AtCellFreeData:
                _kgpPlacements.RemoveAll(p => p.IntersectsCell((int)command.SourceY - 1, (int)command.SourceX - 1));
                break;
            case KgpDeleteTarget.ByColumn:
            case KgpDeleteTarget.ByColumnFreeData:
                _kgpPlacements.RemoveAll(p => p.IntersectsColumn((int)command.SourceX - 1));
                break;
            case KgpDeleteTarget.ByRow:
            case KgpDeleteTarget.ByRowFreeData:
                _kgpPlacements.RemoveAll(p => p.IntersectsRow((int)command.SourceY - 1));
                break;
            case KgpDeleteTarget.ByZIndex:
            case KgpDeleteTarget.ByZIndexFreeData:
                _kgpPlacements.RemoveAll(p => p.ZIndex == command.ZIndex);
                break;
            case KgpDeleteTarget.ByRange:
            case KgpDeleteTarget.ByRangeFreeData:
            {
                var lo = command.SourceX;
                var hi = command.SourceY;
                if (lo > 0 && hi > 0 && lo <= hi)
                {
                    _kgpPlacements.RemoveAll(p => p.ImageId >= lo && p.ImageId <= hi);
                    if (command.DeleteTarget == KgpDeleteTarget.ByRangeFreeData)
                    {
                        for (uint id = lo; id <= hi; id++)
                            _kgpImageStore.RemoveImage(id);
                    }
                }
                break;
            }
            case KgpDeleteTarget.AtCellWithZIndex:
            case KgpDeleteTarget.AtCellWithZIndexFreeData:
            {
                var cellRow = (int)command.SourceY - 1;
                var cellCol = (int)command.SourceX - 1;
                var targetZ = command.ZIndex;
                _kgpPlacements.RemoveAll(p => p.IntersectsCell(cellRow, cellCol) && p.ZIndex == targetZ);
                break;
            }
        }

        // Delete aborts any in-progress chunked transfer
        _kgpImageStore.AbortChunkedTransfer();
    }

    private void CreateKgpPlacement(uint imageId, uint placementId, uint cols, uint rows, KgpCommand command)
    {
        // If same image+placement combo exists, replace it
        if (placementId > 0)
        {
            _kgpPlacements.RemoveAll(p => p.ImageId == imageId && p.PlacementId == placementId);
        }

        var placement = new KgpPlacement(
            imageId, placementId,
            _cursorY, _cursorX,
            cols, rows,
            command.SourceX, command.SourceY,
            command.SourceWidth, command.SourceHeight,
            command.ZIndex,
            command.CellOffsetX, command.CellOffsetY);

        _kgpPlacements.Add(placement);
    }

    private void SendKgpResponse(uint imageId, uint imageNumber, string message, int quiet)
    {
        // q=1 suppresses OK, q=2 suppresses all responses
        if (quiet >= 2) return;
        if (quiet >= 1 && message == "OK") return;

        // App-style workloads consume high-level input events, not raw terminal
        // protocol replies. Feeding APC responses back through WriteInputAsync()
        // causes them to be misparsed as key presses.
        if (_workload is IHex1bAppTerminalWorkloadAdapter)
            return;

        var response = new StringBuilder();
        response.Append("\x1b_G");

        if (imageId > 0)
            response.Append($"i={imageId}");
        if (imageNumber > 0)
        {
            if (imageId > 0)
                response.Append(',');
            response.Append($"I={imageNumber}");
        }

        response.Append(';');
        response.Append(message);
        response.Append("\x1b\\");

        var responseStr = response.ToString();
        var bytes = Encoding.UTF8.GetBytes(responseStr);
        _ = SendProtocolResponseAsync(bytes);
    }

    private static byte[] DecodeKgpPayload(string base64Payload)
    {
        if (string.IsNullOrEmpty(base64Payload))
            return Array.Empty<byte>();

        try
        {
            return Convert.FromBase64String(base64Payload);
        }
        catch (FormatException)
        {
            return Array.Empty<byte>();
        }
    }

    private static long GetExpectedKgpDataSize(KgpCommand command)
    {
        if (command.Format == KgpFormat.Png)
            return 0; // PNG size is variable

        if (command.Width == 0 || command.Height == 0)
            return 0;

        var bytesPerPixel = command.Format == KgpFormat.Rgb24 ? 3 : 4;
        return (long)command.Width * command.Height * bytesPerPixel;
    }
}
