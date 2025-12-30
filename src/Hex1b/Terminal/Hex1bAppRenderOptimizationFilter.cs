using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Terminal;

/// <summary>
/// A presentation filter that optimizes output by only transmitting cells that have changed.
/// </summary>
/// <remarks>
/// <para>
/// This filter maintains dual buffers for delta encoding:
/// <list type="bullet">
///   <item><b>Pending buffer</b>: Accumulates changes during the current frame</item>
///   <item><b>Committed buffer</b>: Represents what was last sent to the presentation layer</item>
/// </list>
/// </para>
/// <para>
/// When frame buffering is enabled (via <see cref="FrameBeginToken"/> and <see cref="FrameEndToken"/>),
/// updates are accumulated in the pending buffer without emitting output. When the frame ends,
/// the filter compares pending vs committed and emits only the net changes, eliminating
/// intermediate states (like clear-then-render) that cause flicker.
/// </para>
/// <para>
/// Benefits:
/// <list type="bullet">
///   <item>Reduces bandwidth for remote terminal connections</item>
///   <item>Improves rendering performance by avoiding redundant updates</item>
///   <item>Enables efficient partial screen updates</item>
///   <item>Eliminates flicker from intermediate render states</item>
/// </list>
/// </para>
/// </remarks>
public sealed class Hex1bAppRenderOptimizationFilter : IHex1bTerminalPresentationFilter
{
    // Pending buffer: accumulates changes during the current frame
    private ShadowCell[,]? _pendingBuffer;
    
    // Committed buffer: represents what was last sent to the terminal
    private ShadowCell[,]? _committedBuffer;
    
    private int _width;
    private int _height;
    private bool _forceFullRefresh;
    
    // Frame buffering state
    private bool _isBuffering;
    private List<AnsiToken>? _bufferedControlTokens;

    /// <summary>
    /// Represents a cell in the shadow buffer, containing only the visual properties needed for comparison.
    /// </summary>
    private readonly record struct ShadowCell(
        string Character,
        Hex1bColor? Foreground,
        Hex1bColor? Background,
        CellAttributes Attributes)
    {
        public static readonly ShadowCell Empty = new(" ", null, null, CellAttributes.None);

        /// <summary>
        /// Creates a ShadowCell from a TerminalCell, extracting only the visual properties.
        /// </summary>
        public static ShadowCell FromTerminalCell(TerminalCell cell)
            => new(cell.Character, cell.Foreground, cell.Background, cell.Attributes);
    }

    /// <summary>
    /// Represents a changed cell with its position and new state.
    /// </summary>
    private readonly record struct ChangedCell(int X, int Y, ShadowCell Cell);

    /// <inheritdoc />
    public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
    {
        InitializeBuffers(width, height);
        _forceFullRefresh = true; // First frame should pass through
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
        IReadOnlyList<AppliedToken> appliedTokens,
        TimeSpan elapsed,
        CancellationToken ct = default)
    {
        // Handle force full refresh (first frame or after resize)
        if (_pendingBuffer is null || _committedBuffer is null || _forceFullRefresh)
        {
            // Check if there are any content tokens (not just frame boundary tokens)
            // We don't want orphaned frame tokens after a resize to consume the force refresh flag
            var contentTokens = appliedTokens
                .Where(at => at.Token is not FrameBeginToken and not FrameEndToken)
                .ToList();
            
            if (contentTokens.Count == 0)
            {
                // Only frame boundary tokens - handle them but don't consume force refresh
                foreach (var appliedToken in appliedTokens)
                {
                    switch (appliedToken.Token)
                    {
                        case FrameBeginToken:
                            _isBuffering = true;
                            _bufferedControlTokens = [];
                            break;
                        case FrameEndToken:
                            // Ignore orphaned FrameEnd - the frame was cancelled by resize
                            _isBuffering = false;
                            _bufferedControlTokens = null;
                            break;
                    }
                }
                return ValueTask.FromResult<IReadOnlyList<AnsiToken>>([]);
            }
            
            // We have real content - consume the force refresh flag
            _forceFullRefresh = false;
            
            // Update both buffers with all impacts, then pass through tokens
            // (excluding internal frame boundary tokens)
            foreach (var appliedToken in appliedTokens)
            {
                foreach (var impact in appliedToken.CellImpacts)
                {
                    if (impact.X >= 0 && impact.X < _width && impact.Y >= 0 && impact.Y < _height)
                    {
                        var cell = ShadowCell.FromTerminalCell(impact.Cell);
                        _pendingBuffer![impact.Y, impact.X] = cell;
                        _committedBuffer![impact.Y, impact.X] = cell;
                    }
                }
            }
            
            // On force refresh (after resize), we must clear the entire screen first.
            // The terminal's buffer may have old content in newly expanded areas that our
            // shadow buffers don't know about. 
            // IMPORTANT: Reset SGR BEFORE clearing, so the clear uses default colors.
            var result = new List<AnsiToken> 
            { 
                new SgrToken("0"),                     // Reset attributes first!
                new ClearScreenToken(ClearMode.All),   // Clear entire terminal buffer (uses current bg)
            };
            result.AddRange(appliedTokens
                .Select(at => at.Token)
                .Where(t => t is not FrameBeginToken and not FrameEndToken));
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(result);
        }

        // Accumulate results for immediate mode processing
        List<AnsiToken>? immediateResults = null;

        // Process each token
        foreach (var appliedToken in appliedTokens)
        {
            var token = appliedToken.Token;
            
            // Check for frame boundary tokens first
            switch (token)
            {
                case FrameBeginToken:
                    // If already buffering, flush previous frame first (handles unmatched begin)
                    if (_isBuffering)
                    {
                        var flushed = CommitFrame();
                        if (flushed.Count > 0)
                        {
                            immediateResults ??= [];
                            immediateResults.AddRange(flushed);
                        }
                    }
                    _isBuffering = true;
                    _bufferedControlTokens = [];
                    continue;
                    
                case FrameEndToken:
                    if (_isBuffering)
                    {
                        _isBuffering = false;
                        var frameOutput = CommitFrame();
                        _bufferedControlTokens = null;
                        // Add frame output to results and continue processing remaining tokens
                        // (e.g., the ?2026l token that follows FrameEndToken)
                        if (frameOutput.Count > 0)
                        {
                            immediateResults ??= [];
                            immediateResults.AddRange(frameOutput);
                        }
                    }
                    // FrameEnd without FrameBegin - ignore
                    continue;
            }
            
            // Handle tokens based on whether we're buffering
            if (_isBuffering)
            {
                ProcessTokenBuffered(appliedToken);
            }
            else
            {
                // Not buffering - use immediate mode, accumulate all results
                var immediateResult = ProcessTokenImmediate(appliedToken);
                if (immediateResult.Count > 0)
                {
                    immediateResults ??= [];
                    immediateResults.AddRange(immediateResult);
                }
            }
        }

        // Return accumulated immediate mode results, or empty if buffering/no changes
        if (immediateResults is { Count: > 0 })
        {
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(immediateResults);
        }
        
        return ValueTask.FromResult<IReadOnlyList<AnsiToken>>([]);
    }
    
    /// <summary>
    /// Processes a token in buffered mode - updates pending buffer but doesn't emit.
    /// </summary>
    private void ProcessTokenBuffered(AppliedToken appliedToken)
    {
        var token = appliedToken.Token;
        
        switch (token)
        {
            case ClearScreenToken clearToken:
                // Clear only the pending buffer (committed will be updated on frame end)
                ClearBuffer(_pendingBuffer, clearToken.Mode);
                // Don't buffer ClearScreenToken - we'll handle via cell diffs
                return;
                
            case ClearLineToken:
            case PrivateModeToken:
            case CursorShapeToken:
            case SaveCursorToken:
            case RestoreCursorToken:
            case ScrollRegionToken:
            case OscToken:
            case DcsToken:
                // Buffer control tokens to emit at frame end
                _bufferedControlTokens?.Add(token);
                return;
                
            case CursorPositionToken when appliedToken.CellImpacts.Count == 0:
                // Standalone cursor positioning - buffer it
                _bufferedControlTokens?.Add(token);
                return;
        }
        
        // Process cell impacts - update pending buffer only
        foreach (var impact in appliedToken.CellImpacts)
        {
            if (impact.X < 0 || impact.X >= _width || impact.Y < 0 || impact.Y >= _height)
                continue;

            var newCell = ShadowCell.FromTerminalCell(impact.Cell);
            _pendingBuffer![impact.Y, impact.X] = newCell;
        }
    }
    
    /// <summary>
    /// Processes a token in immediate mode - updates both buffers and returns tokens to emit.
    /// </summary>
    private IReadOnlyList<AnsiToken> ProcessTokenImmediate(AppliedToken appliedToken)
    {
        var token = appliedToken.Token;
        var changedCells = new List<ChangedCell>();
        
        switch (token)
        {
            case ClearScreenToken clearToken:
                // Clear both buffers and pass through
                ClearBuffer(_pendingBuffer, clearToken.Mode);
                ClearBuffer(_committedBuffer, clearToken.Mode);
                return [token];
                
            case ClearLineToken:
            case PrivateModeToken:
            case CursorShapeToken:
            case SaveCursorToken:
            case RestoreCursorToken:
            case ScrollRegionToken:
            case OscToken:
            case DcsToken:
                // Pass through control tokens
                return [token];
                
            case CursorPositionToken when appliedToken.CellImpacts.Count == 0:
                // Standalone cursor positioning passes through
                return [token];
        }
        
        // Process cell impacts
        foreach (var impact in appliedToken.CellImpacts)
        {
            if (impact.X < 0 || impact.X >= _width || impact.Y < 0 || impact.Y >= _height)
                continue;

            var newCell = ShadowCell.FromTerminalCell(impact.Cell);
            var currentCommitted = _committedBuffer![impact.Y, impact.X];

            _pendingBuffer![impact.Y, impact.X] = newCell;

            if (newCell != currentCommitted)
            {
                changedCells.Add(new ChangedCell(impact.X, impact.Y, newCell));
                _committedBuffer[impact.Y, impact.X] = newCell;
            }
        }
        
        if (changedCells.Count > 0)
        {
            return GenerateTokens(changedCells);
        }
        
        return [];
    }
    
    /// <summary>
    /// Commits the current frame by comparing pending vs committed buffers.
    /// Returns the tokens needed to update the terminal to the pending state.
    /// </summary>
    private IReadOnlyList<AnsiToken> CommitFrame()
    {
        if (_pendingBuffer is null || _committedBuffer is null)
            return [];
            
        var changedCells = new List<ChangedCell>();
        
        // Compare pending vs committed to find all changed cells
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                var pending = _pendingBuffer[y, x];
                var committed = _committedBuffer[y, x];
                
                if (pending != committed)
                {
                    changedCells.Add(new ChangedCell(x, y, pending));
                    // Update committed buffer
                    _committedBuffer[y, x] = pending;
                }
            }
        }
        
        // Build output: buffered control tokens first, then cell changes
        var output = new List<AnsiToken>();
        
        if (_bufferedControlTokens is { Count: > 0 })
        {
            output.AddRange(_bufferedControlTokens);
        }
        
        if (changedCells.Count > 0)
        {
            output.AddRange(GenerateTokens(changedCells));
        }
        
        return output;
    }

    /// <inheritdoc />
    public ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed, CancellationToken ct = default)
    {
        // Input doesn't affect delta encoding
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
    {
        // Reinitialize both buffers on resize and force a full refresh
        InitializeBuffers(width, height);
        _forceFullRefresh = true;
        
        // If we were buffering when the resize happened, cancel the current frame.
        // The buffered content is now invalid because:
        // 1. The buffers have been reinitialized to the new size
        // 2. The pending content was for the old terminal dimensions
        // The next frame will be a full refresh due to _forceFullRefresh = true
        _isBuffering = false;
        _bufferedControlTokens = null;
        
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
    {
        _pendingBuffer = null;
        _committedBuffer = null;
        _width = 0;
        _height = 0;
        _forceFullRefresh = false;
        _isBuffering = false;
        _bufferedControlTokens = null;
        return ValueTask.CompletedTask;
    }

    private void InitializeBuffers(int width, int height)
    {
        _width = width;
        _height = height;
        _pendingBuffer = new ShadowCell[height, width];
        _committedBuffer = new ShadowCell[height, width];

        // Initialize both buffers with empty cells
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _pendingBuffer[y, x] = ShadowCell.Empty;
                _committedBuffer[y, x] = ShadowCell.Empty;
            }
        }
    }

    /// <summary>
    /// Clears a buffer based on the clear mode.
    /// </summary>
    private void ClearBuffer(ShadowCell[,]? buffer, ClearMode mode)
    {
        if (buffer is null) return;
        
        // For simplicity, treat all clear modes as full clear
        // A more sophisticated implementation could track cursor position
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                buffer[y, x] = ShadowCell.Empty;
            }
        }
    }

    /// <summary>
    /// Generates an optimized token stream for the changed cells.
    /// </summary>
    /// <remarks>
    /// Strategy:
    /// 1. Sort cells by position (row-major order) for optimal cursor movement
    /// 2. Group contiguous cells on the same row to minimize cursor repositioning
    /// 3. Track current SGR state to avoid redundant attribute sequences
    /// </remarks>
    private IReadOnlyList<AnsiToken> GenerateTokens(List<ChangedCell> changedCells)
    {
        if (changedCells.Count == 0)
            return Array.Empty<AnsiToken>();

        var tokens = new List<AnsiToken>();

        // Sort by Y, then X for optimal cursor movement
        changedCells.Sort((a, b) =>
        {
            var yCompare = a.Y.CompareTo(b.Y);
            return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
        });

        // Track current state to minimize redundant tokens
        int cursorX = -1;
        int cursorY = -1;
        Hex1bColor? currentFg = null;
        Hex1bColor? currentBg = null;
        CellAttributes currentAttrs = CellAttributes.None;
        bool stateUnknown = true; // Start with unknown state - need to emit reset first

        foreach (var cell in changedCells)
        {
            // Position cursor if needed
            if (cell.Y != cursorY || cell.X != cursorX)
            {
                // ANSI cursor position is 1-based
                tokens.Add(new CursorPositionToken(cell.Y + 1, cell.X + 1));
                cursorY = cell.Y;
                cursorX = cell.X;
            }

            // Generate SGR if attributes changed or state is unknown
            var needsSgr = stateUnknown ||
                           !Equals(cell.Cell.Foreground, currentFg) ||
                           !Equals(cell.Cell.Background, currentBg) ||
                           cell.Cell.Attributes != currentAttrs;

            if (needsSgr)
            {
                var sgrParams = BuildSgrParameters(cell.Cell, stateUnknown, ref currentFg, ref currentBg, ref currentAttrs);
                tokens.Add(new SgrToken(sgrParams));
                stateUnknown = false;
            }

            // Output the character
            tokens.Add(new TextToken(cell.Cell.Character));
            cursorX++; // Cursor advances after text output
        }

        return tokens;
    }

    /// <summary>
    /// Builds SGR parameters for transitioning to the target cell's attributes.
    /// </summary>
    private static string BuildSgrParameters(
        ShadowCell targetCell,
        bool stateUnknown,
        ref Hex1bColor? currentFg,
        ref Hex1bColor? currentBg,
        ref CellAttributes currentAttrs)
    {
        var parts = new List<string>();

        // Check if we need a reset first
        // Reset if: state is unknown, OR turning OFF any attributes, OR clearing colors
        var turnedOff = currentAttrs & ~targetCell.Attributes;
        bool needsReset = stateUnknown ||
                         turnedOff != CellAttributes.None ||
                         (currentFg is not null && targetCell.Foreground is null) ||
                         (currentBg is not null && targetCell.Background is null);

        if (needsReset)
        {
            parts.Add("0"); // Reset to defaults
            currentAttrs = CellAttributes.None;
            currentFg = null;
            currentBg = null;
        }

        // Add attributes that need to be turned on
        var toTurnOn = targetCell.Attributes & ~currentAttrs;

        if ((toTurnOn & CellAttributes.Bold) != 0)
            parts.Add("1");
        if ((toTurnOn & CellAttributes.Dim) != 0)
            parts.Add("2");
        if ((toTurnOn & CellAttributes.Italic) != 0)
            parts.Add("3");
        if ((toTurnOn & CellAttributes.Underline) != 0)
            parts.Add("4");
        if ((toTurnOn & CellAttributes.Blink) != 0)
            parts.Add("5");
        if ((toTurnOn & CellAttributes.Reverse) != 0)
            parts.Add("7");
        if ((toTurnOn & CellAttributes.Hidden) != 0)
            parts.Add("8");
        if ((toTurnOn & CellAttributes.Strikethrough) != 0)
            parts.Add("9");
        if ((toTurnOn & CellAttributes.Overline) != 0)
            parts.Add("53");

        // Add foreground color if different
        if (!Equals(targetCell.Foreground, currentFg) && targetCell.Foreground is not null)
        {
            parts.Add(BuildColorSgr(targetCell.Foreground.Value, isForeground: true));
        }

        // Add background color if different
        if (!Equals(targetCell.Background, currentBg) && targetCell.Background is not null)
        {
            parts.Add(BuildColorSgr(targetCell.Background.Value, isForeground: false));
        }
        // Update tracked state
        currentAttrs = targetCell.Attributes;
        currentFg = targetCell.Foreground;
        currentBg = targetCell.Background;

        return string.Join(";", parts);
    }

    /// <summary>
    /// Builds the SGR parameter string for a color.
    /// </summary>
    private static string BuildColorSgr(Hex1bColor color, bool isForeground)
    {
        // Use 24-bit color (SGR 38;2;r;g;b for foreground, 48;2;r;g;b for background)
        var prefix = isForeground ? "38;2" : "48;2";
        return $"{prefix};{color.R};{color.G};{color.B}";
    }
}
