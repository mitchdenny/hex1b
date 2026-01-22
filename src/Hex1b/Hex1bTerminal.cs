#pragma warning disable HEX1B_SIXEL // Sixel API is experimental - internal usage is allowed

using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Input;
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
    
    // Lock to protect screen buffer state from concurrent access.
    // The resize event comes from the input thread while the output pump
    // runs on a separate thread, both accessing _screenBuffer, _width, _height.
    private readonly object _bufferLock = new();
    
    private TerminalCell[,] _screenBuffer;
    private int _cursorX;
    private int _cursorY;
    private Hex1bColor? _currentForeground;
    private Hex1bColor? _currentBackground;
    private CellAttributes _currentAttributes;
    private TrackedObject<HyperlinkData>? _currentHyperlink; // Active hyperlink from OSC 8
    private bool _disposed;
    private bool _inAlternateScreen;
    private TerminalCell[,]? _savedMainScreenBuffer; // Saved main screen when entering alternate screen
    private int _alternateScreenSavedCursorX; // Saved cursor X for alternate screen (mode 1049)
    private int _alternateScreenSavedCursorY; // Saved cursor Y for alternate screen (mode 1049)
    private Task? _inputProcessingTask;
    private Task? _outputProcessingTask;
    private long _writeSequence; // Monotonically increasing write order counter
    private int _savedCursorX; // Saved cursor X position for DECSC/DECRC
    private int _savedCursorY; // Saved cursor Y position for DECSC/DECRC
    private bool _cursorSaved; // Whether cursor has been saved (for restore without prior save)
    private readonly Decoder _utf8Decoder = Encoding.UTF8.GetDecoder(); // Handles incomplete UTF-8 sequences across reads
    private string _incompleteSequenceBuffer = ""; // Buffers incomplete ANSI escape sequences across reads
    
    // Scroll region (DECSTBM) - 0-based indices
    private int _scrollTop; // Top margin (0 = first row)
    private int _scrollBottom; // Bottom margin (height-1 = last row), initialized in constructor
    
    // Left/Right margins (DECSLRM) - 0-based indices
    private int _marginLeft; // Left margin (0 = first column)
    private int _marginRight; // Right margin (width-1 = last column), initialized in constructor
    private bool _declrmm; // DECLRMM mode (mode 69): when true, CSI s sets left/right margins instead of saving cursor
    
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
    
    // Last printed character for CSI b (REP - repeat) command
    private TerminalCell _lastPrintedCell = TerminalCell.Empty;
    
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
        
        // Get dimensions from presentation adapter (it's the source of truth)
        _width = _presentation.Width > 0 ? _presentation.Width : options.Width;
        _height = _presentation.Height > 0 ? _presentation.Height : options.Height;
        
        // Notify workload of initial dimensions (ResizeAsync handles not firing event on init)
        _ = _workload.ResizeAsync(_width, _height);
        
        _screenBuffer = new TerminalCell[_height, _width];
        _scrollBottom = _height - 1; // Default scroll region is full screen
        _marginRight = _width - 1; // Default left/right margins are full screen
        
        ClearBuffer();

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
            int exitCode = 0;
            if (_runCallback != null)
            {
                exitCode = await _runCallback(ct);
            }
            else
            {
                // No run callback - wait for workload to disconnect
                await WaitForWorkloadDisconnectAsync(ct);
            }
            
            // Notify lifecycle-aware presentation adapters that the terminal has completed
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

                // Tokenize input the same way we tokenize output
                var text = Encoding.UTF8.GetString(data.Span);
                var tokens = AnsiTokenizer.Tokenize(text);
                
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
                    await _workload.WriteInputAsync(data, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }
    
    /// <summary>
    /// Dispatches tokenized input as high-level events to the Hex1bApp workload.
    /// </summary>
    private async Task DispatchTokensAsEventsAsync(IReadOnlyList<AnsiToken> tokens, Hex1bAppWorkloadAdapter workload, CancellationToken ct)
    {
        foreach (var token in tokens)
        {
            ct.ThrowIfCancellationRequested();
            
            var evt = TokenToEvent(token);
            if (evt != null)
            {
                await workload.WriteInputEventAsync(evt, ct);
            }
        }
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
            CursorPositionToken { Row: 1, Column: 1 } => new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None),
            
            // Backtab (Shift+Tab) - CSI Z
            BackTabToken => new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.Shift),
            
            // Text (printable characters, emoji, etc.)
            TextToken text => TextTokenToEvent(text),
            
            // Control characters (Enter, Tab, etc.)
            ControlCharacterToken ctrl => ControlCharToKeyEvent(ctrl),
            
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
            return new CursorMoveToken(arrowDir.Value, 1);
        }
        
        // Home/End in SS3 mode (common for many terminals)
        if (evt.Key == Hex1bKey.Home) return new Ss3Token('H');
        if (evt.Key == Hex1bKey.End) return new Ss3Token('F');
        
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
                var data = await _workload.ReadOutputAsync(ct);
                
                if (data.IsEmpty)
                {
                    // Channel empty - this is a frame boundary
                    await NotifyWorkloadFiltersFrameCompleteAsync();
                    
                    // Small delay to prevent busy-waiting in headless mode
                    await Task.Delay(10, ct);
                    continue;
                }
                
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
                var (completeText, incomplete) = ExtractIncompleteEscapeSequence(text);
                _incompleteSequenceBuffer = incomplete;
                
                if (string.IsNullOrEmpty(completeText))
                    continue; // All content is incomplete, wait for more data
                
                // Tokenize once, use for all processing
                var tokens = AnsiTokenizer.Tokenize(completeText);
                
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
                        var originalBytes = Encoding.UTF8.GetBytes(completeText);
                        await _presentation.WriteOutputAsync(originalBytes);
                    }
                    else
                    {
                        // Pass through presentation filters, serialize and send
                        var filteredTokens = await NotifyPresentationFiltersOutputAsync(appliedTokens);
                        var filteredText = AnsiTokenSerializer.Serialize(filteredTokens);
                        var filteredBytes = Encoding.UTF8.GetBytes(filteredText);
                        await _presentation.WriteOutputAsync(filteredBytes);
                    }
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
                _ = _workload.WriteInputAsync(bytes, CancellationToken.None);
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
                await _workload.WriteInputAsync(bytes, ct);
                
                // Also notify filters of the input
                await NotifyWorkloadFiltersInputAsync(tokens);
            }
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
    /// Resizes the terminal, preserving content where possible.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        lock (_bufferLock)
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
            
            // Reset margins on resize - this matches xterm behavior
            _marginRight = newWidth - 1;
            if (_marginLeft > _marginRight)
                _marginLeft = 0;
            
            // Reset scroll region on resize - this matches xterm behavior
            // The scroll region should cover the full new screen height
            _scrollTop = 0;
            _scrollBottom = newHeight - 1;
        }
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

    private void ClearBuffer(List<CellImpact>? impacts = null)
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                SetCell(y, x, TerminalCell.Empty, impacts);
            }
        }
        _currentForeground = null;
        _currentBackground = null;
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
                if (_originMode)
                {
                    // Origin mode: positions are relative to scroll region
                    _cursorY = Math.Clamp(_scrollTop + cursorToken.Row - 1, _scrollTop, _scrollBottom);
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
                ApplyClearScreen(clearToken.Mode, impacts);
                break;
                
            case ClearLineToken clearLineToken:
                ApplyClearLine(clearLineToken.Mode, impacts);
                break;
                
            case PrivateModeToken privateModeToken:
                if (privateModeToken.Mode == 1049)
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
                break;
            
            case LeftRightMarginToken lrmToken:
                // DECSLRM - Set Left Right Margins
                // Only effective when DECLRMM (mode 69) is enabled
                if (_declrmm)
                {
                    if (lrmToken.Right == 0)
                    {
                        // Reset to full screen width
                        _marginLeft = 0;
                        _marginRight = _width - 1;
                    }
                    else
                    {
                        _marginLeft = Math.Clamp(lrmToken.Left - 1, 0, _width - 1);
                        _marginRight = Math.Clamp(lrmToken.Right - 1, _marginLeft, _width - 1);
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
                if (scrollRegionToken.Bottom == 0)
                {
                    // Reset to full screen
                    _scrollTop = 0;
                    _scrollBottom = _height - 1;
                }
                else
                {
                    _scrollTop = Math.Clamp(scrollRegionToken.Top - 1, 0, _height - 1);
                    _scrollBottom = Math.Clamp(scrollRegionToken.Bottom - 1, _scrollTop, _height - 1);
                }
                // DECSTBM also moves cursor to home position (1,1)
                _pendingWrap = false; // Cursor movement clears pending wrap
                _cursorX = 0;
                _cursorY = 0;
                break;
                
            case SaveCursorToken:
                _savedCursorX = _cursorX;
                _savedCursorY = _cursorY;
                _cursorSaved = true;
                break;
                
            case RestoreCursorToken:
                // Only restore if cursor was previously saved (matches GNOME Terminal behavior)
                if (_cursorSaved)
                {
                    _pendingWrap = false; // Cursor restore clears pending wrap
                    _cursorX = _savedCursorX;
                    _cursorY = _savedCursorY;
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
                
            case DeleteCharacterToken deleteCharToken:
                DeleteCharacters(deleteCharToken.Count, impacts);
                break;
                
            case InsertCharacterToken insertCharToken:
                InsertCharacters(insertCharToken.Count, impacts);
                break;
                
            case EraseCharacterToken eraseCharToken:
                EraseCharacters(eraseCharToken.Count, impacts);
                break;
                
            case RepeatCharacterToken repeatToken:
                RepeatLastCharacter(repeatToken.Count, impacts);
                break;
                
            case IndexToken:
                // Move cursor down one line, scroll if at bottom of scroll region
                if (_cursorY >= _scrollBottom)
                    ScrollUp(impacts);
                else
                    _cursorY++;
                break;
                
            case ReverseIndexToken:
                // Move cursor up one line, scroll if at top of scroll region
                if (_cursorY <= _scrollTop)
                    ScrollDown(impacts);
                else
                    _cursorY--;
                break;
            
            case CharacterSetToken:
                // Character set selection - we pass through to presentation
                // but don't need to track state for buffer purposes
                break;
            
            case KeypadModeToken:
                // Keypad mode - presentation-only, no buffer state
                break;
                
            case UnrecognizedSequenceToken:
                // Ignore unrecognized sequences
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
            // Write response synchronously on thread pool to avoid blocking output pump
            _ = Task.Run(async () =>
            {
                try
                {
                    await _workload.WriteInputAsync(bytes, CancellationToken.None);
                }
                catch (Exception)
                {
                    // Ignore errors - process may have exited
                }
            });
        }
    }

    private void ApplyTextToken(TextToken token, List<CellImpact>? impacts)
    {
        var text = token.Text;
        int i = 0;
        
        while (i < text.Length)
        {
            var grapheme = GetGraphemeAt(text, i);
            var graphemeWidth = DisplayWidth.GetGraphemeWidth(grapheme);
            
            // Deferred wrap: If a wrap was pending from a previous character, perform it now
            // This is standard VT100/xterm behavior - wrap only happens when the NEXT
            // printable character is written, not when cursor reaches the margin.
            if (_pendingWrap)
            {
                _pendingWrap = false;
                // When DECLRMM is enabled, wrap to left margin, not column 0
                _cursorX = _declrmm ? _marginLeft : 0;
                _cursorY++;
            }
            
            // Scroll if cursor is past the bottom of the screen BEFORE writing
            if (_cursorY >= _height)
            {
                ScrollUp(impacts);
                _cursorY = _height - 1;
            }
            
            // Determine effective right margin for wrapping
            int effectiveRightMargin = _declrmm ? _marginRight : _width - 1;
            
            if (_cursorX < _width && _cursorY < _height)
            {
                var sequence = ++_writeSequence;
                var writtenAt = _timeProvider.GetUtcNow();
                
                _currentHyperlink?.AddRef();
                
                var cell = new TerminalCell(
                    grapheme, _currentForeground, _currentBackground, _currentAttributes,
                    sequence, writtenAt, TrackedSixel: null, _currentHyperlink);
                SetCell(_cursorY, _cursorX, cell, impacts);
                
                // Save last printed cell for CSI b (REP) command
                _lastPrintedCell = cell;
                
                for (int w = 1; w < graphemeWidth && _cursorX + w < _width; w++)
                {
                    _currentHyperlink?.AddRef();
                    SetCell(_cursorY, _cursorX + w, new TerminalCell(
                        "", _currentForeground, _currentBackground, _currentAttributes,
                        sequence, writtenAt, TrackedSixel: null, _currentHyperlink), impacts);
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
            }
            i += grapheme.Length;
        }
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
                // When DECLRMM is enabled, CR moves to left margin, not column 0
                _cursorX = _declrmm ? _marginLeft : 0;
                break;
                
            case '\t':
                // Move to next tab stop (every 8 columns)
                _cursorX = Math.Min((_cursorX / 8 + 1) * 8, _width - 1);
                break;
                
            case '\b':
                // Backspace - move cursor left (non-destructive)
                _pendingWrap = false;
                if (_cursorX > 0)
                {
                    _cursorX--;
                }
                break;
        }
    }

    private void ApplyCursorMove(CursorMoveToken token)
    {
        // Any explicit cursor movement clears pending wrap
        _pendingWrap = false;
        
        switch (token.Direction)
        {
            case CursorMoveDirection.Up:
                _cursorY = Math.Max(0, _cursorY - token.Count);
                break;
                
            case CursorMoveDirection.Down:
                _cursorY = Math.Min(_height - 1, _cursorY + token.Count);
                break;
                
            case CursorMoveDirection.Forward:
                _cursorX = Math.Min(_width - 1, _cursorX + token.Count);
                break;
                
            case CursorMoveDirection.Back:
                _cursorX = Math.Max(0, _cursorX - token.Count);
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

    private void ApplyClearScreen(ClearMode mode, List<CellImpact>? impacts)
    {
        switch (mode)
        {
            case ClearMode.ToEnd:
                ClearFromCursor(impacts);
                break;
            case ClearMode.ToStart:
                ClearToCursor(impacts);
                break;
            case ClearMode.All:
            case ClearMode.AllAndScrollback:
                ClearBuffer(impacts);
                break;
        }
    }

    private void ApplyClearLine(ClearMode mode, List<CellImpact>? impacts)
    {
        // When DECLRMM is enabled, clear operations respect left/right margins
        int effectiveLeft = _declrmm ? _marginLeft : 0;
        int effectiveRight = _declrmm ? _marginRight : _width - 1;
        
        int clearedCount = 0;
        switch (mode)
        {
            case ClearMode.ToEnd:
                for (int x = _cursorX; x <= effectiveRight && x < _width; x++)
                {
                    SetCell(_cursorY, x, TerminalCell.Empty, impacts);
                    clearedCount++;
                }
                break;
            case ClearMode.ToStart:
                for (int x = effectiveLeft; x <= _cursorX && x < _width; x++)
                {
                    SetCell(_cursorY, x, TerminalCell.Empty, impacts);
                    clearedCount++;
                }
                break;
            case ClearMode.All:
                for (int x = effectiveLeft; x <= effectiveRight && x < _width; x++)
                {
                    SetCell(_cursorY, x, TerminalCell.Empty, impacts);
                    clearedCount++;
                }
                break;
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
            ClearBuffer(impacts);
        }
        
        _cursorX = 0;
        _cursorY = 0;
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

    private void ClearFromCursor(List<CellImpact>? impacts = null)
    {
        for (int x = _cursorX; x < _width; x++)
        {
            SetCell(_cursorY, x, TerminalCell.Empty, impacts);
        }
        for (int y = _cursorY + 1; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                SetCell(y, x, TerminalCell.Empty, impacts);
            }
        }
    }

    private void ClearToCursor(List<CellImpact>? impacts = null)
    {
        for (int y = 0; y < _cursorY; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                SetCell(y, x, TerminalCell.Empty, impacts);
            }
        }
        for (int x = 0; x <= _cursorX && x < _width; x++)
        {
            SetCell(_cursorY, x, TerminalCell.Empty, impacts);
        }
    }

    private void ScrollUp(List<CellImpact>? impacts = null)
    {
        // Scroll up within the scroll region
        // When DECLRMM is enabled, only scroll within left/right margins
        int leftCol = _declrmm ? _marginLeft : 0;
        int rightCol = _declrmm ? _marginRight : _width - 1;
        
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
        for (int x = leftCol; x <= rightCol; x++)
        {
            SetCell(_scrollBottom, x, TerminalCell.Empty, impacts);
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
        for (int x = leftCol; x <= rightCol; x++)
        {
            SetCell(_scrollTop, x, TerminalCell.Empty, impacts);
        }
    }
    
    private void InsertLines(int count, List<CellImpact>? impacts = null)
    {
        // Insert blank lines at cursor position within scroll region
        // Lines pushed off the bottom of the scroll region are lost
        // When DECLRMM is enabled, only affect columns within left/right margins
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
            for (int x = leftCol; x <= rightCol; x++)
            {
                SetCell(_cursorY, x, TerminalCell.Empty, impacts);
            }
        }
    }
    
    private void DeleteLines(int count, List<CellImpact>? impacts = null)
    {
        // Delete lines at cursor position within scroll region
        // Blank lines are inserted at the bottom of the scroll region
        // When DECLRMM is enabled, only affect columns within left/right margins
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
            for (int x = leftCol; x <= rightCol; x++)
            {
                SetCell(bottom, x, TerminalCell.Empty, impacts);
            }
        }
    }
    
    private void DeleteCharacters(int count, List<CellImpact>? impacts = null)
    {
        // Delete n characters at cursor, shifting remaining characters left
        // Blank characters are inserted at the right margin
        // When DECLRMM is enabled, operations are bounded by left/right margins
        int rightEdge = _declrmm ? _marginRight + 1 : _width;
        count = Math.Min(count, rightEdge - _cursorX);
        
        for (int x = _cursorX; x < rightEdge - count; x++)
        {
            var cellFromRight = _screenBuffer[_cursorY, x + count];
            SetCell(_cursorY, x, cellFromRight, impacts);
        }
        
        // Fill the right edge with blanks
        for (int x = rightEdge - count; x < rightEdge; x++)
        {
            SetCell(_cursorY, x, TerminalCell.Empty, impacts);
        }
    }
    
    private void InsertCharacters(int count, List<CellImpact>? impacts = null)
    {
        // Insert n blank characters at cursor, shifting existing characters right
        // Characters pushed off the right margin are lost
        // When DECLRMM is enabled, operations are bounded by left/right margins
        int rightEdge = _declrmm ? _marginRight + 1 : _width;
        count = Math.Min(count, rightEdge - _cursorX);
        
        // Shift characters right
        for (int x = rightEdge - 1; x >= _cursorX + count; x--)
        {
            var cellFromLeft = _screenBuffer[_cursorY, x - count];
            SetCell(_cursorY, x, cellFromLeft, impacts);
        }
        
        // Insert blanks at cursor position
        for (int x = _cursorX; x < _cursorX + count && x < rightEdge; x++)
        {
            SetCell(_cursorY, x, TerminalCell.Empty, impacts);
        }
    }
    
    private void EraseCharacters(int count, List<CellImpact>? impacts = null)
    {
        // Erase n characters from cursor without moving cursor or shifting
        // When DECLRMM is enabled, operations are bounded by right margin
        int rightEdge = _declrmm ? _marginRight + 1 : _width;
        count = Math.Min(count, rightEdge - _cursorX);
        
        for (int x = _cursorX; x < _cursorX + count; x++)
        {
            SetCell(_cursorY, x, TerminalCell.Empty, impacts);
        }
    }
    
    private void RepeatLastCharacter(int count, List<CellImpact>? impacts)
    {
        // Repeat the last printed graphic character n times
        if (string.IsNullOrEmpty(_lastPrintedCell.Character))
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
                    TrackedHyperlink: null);
                SetCell(_cursorY, _cursorX, cell, impacts);
                
                // Handle wide characters
                for (int w = 1; w < graphemeWidth && _cursorX + w < _width; w++)
                {
                    SetCell(_cursorY, _cursorX + w, new TerminalCell(
                        "", _lastPrintedCell.Foreground, _lastPrintedCell.Background, _lastPrintedCell.Attributes,
                        sequence, writtenAt, TrackedSixel: null, TrackedHyperlink: null), impacts);
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

        if (_presentation != null)
        {
            // Write mouse disable sequences and screen restore DIRECTLY to presentation
            // This ensures they're written before raw mode is exited, avoiding race conditions
            var exitSequences = Input.MouseParser.DisableMouseTracking + 
                "\x1b[?25h" +   // Show cursor
                "\x1b[?1049l";  // Exit alternate screen
            _presentation.WriteOutputAsync(System.Text.Encoding.UTF8.GetBytes(exitSequences), default).AsTask().GetAwaiter().GetResult();
            _presentation.FlushAsync().AsTask().GetAwaiter().GetResult();
            
            // Fire and forget - ExitRawModeAsync is typically synchronous for console
            _ = _presentation.ExitRawModeAsync();
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
            // Write mouse disable sequences and screen restore DIRECTLY to presentation
            // This ensures they're written before raw mode is exited, avoiding race conditions
            // where mouse events could leak to the shell after app exit
            var exitSequences = Input.MouseParser.DisableMouseTracking + 
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
        
        // Find the last ESC character
        int lastEsc = text.LastIndexOf('\x1b');
        if (lastEsc < 0)
            return (text, ""); // No escape sequences
        
        // Check if the sequence starting at lastEsc is complete
        var potentialSequence = text[lastEsc..];
        
        if (IsCompleteEscapeSequence(potentialSequence))
            return (text, ""); // The sequence is complete
        
        // The sequence is incomplete - split it off
        return (text[..lastEsc], potentialSequence);
    }
    
    /// <summary>
    /// Determines if an escape sequence is complete.
    /// </summary>
    private static bool IsCompleteEscapeSequence(string seq)
    {
        if (seq.Length < 2)
            return false; // Just ESC, need more
        
        var second = seq[1];
        
        switch (second)
        {
            case '[': // CSI sequence
                // Need a letter to terminate (but not 'O' which could be SS3)
                for (int i = 2; i < seq.Length; i++)
                {
                    var c = seq[i];
                    // CSI sequences end with a letter (@ through ~, i.e., 0x40-0x7E)
                    if (c >= '@' && c <= '~')
                        return true;
                }
                return false; // No terminator found
                
            case ']': // OSC sequence
                // Terminated by ST (ESC \) or BEL (\x07)
                if (seq.Contains('\x07'))
                    return true;
                if (seq.Contains("\x1b\\"))
                    return true;
                return false;
                
            case 'P': // DCS sequence
            case '_': // APC sequence
                // Terminated by ST (ESC \)
                return seq.Contains("\x1b\\");
                
            case '7': // DECSC
            case '8': // DECRC
            case 'c': // RIS
            case 'D': // IND
            case 'E': // NEL
            case 'H': // HTS
            case 'M': // RI
            case 'N': // SS2
            case 'O': // SS3
            case 'Z': // DECID
            case '=': // DECKPAM (application keypad)
            case '>': // DECKPNM (normal keypad)
                return true; // Two-character sequences are complete
            
            case '(': // G0 character set designation (ESC ( X)
            case ')': // G1 character set designation (ESC ) X)
                return seq.Length >= 3; // Need 3 characters total
                
            default:
                // Unknown sequence type - assume complete
                return true;
        }
    }
}
