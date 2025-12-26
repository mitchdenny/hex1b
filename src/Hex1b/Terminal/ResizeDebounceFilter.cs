using System.Text;
using Hex1b.Theming;

namespace Hex1b.Terminal;

/// <summary>
/// A presentation filter that debounces resize events to prevent UI thrashing.
/// </summary>
/// <remarks>
/// <para>
/// When a terminal is resized rapidly (e.g., dragging a window edge), the app receives
/// many resize events in quick succession. Each resize triggers a full re-render, which
/// can cause significant lag as the terminal struggles to keep up.
/// </para>
/// <para>
/// This filter solves the problem by:
/// <list type="number">
///   <item>Suppressing all output during rapid resize (debounce window)</item>
///   <item>Tracking the pending resize dimensions</item>
///   <item>Only allowing output when dimensions match the pending resize</item>
///   <item>Generating a full screen redraw from the terminal state after resize settles</item>
/// </list>
/// </para>
/// <para>
/// This ensures that after resize completes, the screen is fully redrawn in one clean pass.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
/// {
///     PresentationAdapter = presentation,
///     WorkloadAdapter = workload,
///     PresentationFilters = [new ResizeDebounceFilter(debounceMs: 50)]
/// });
/// </code>
/// </example>
public sealed class ResizeDebounceFilter : IHex1bTerminalPresentationTransformFilter
{
    private readonly int _debounceMs;
    private long _lastResizeTicks;
    private int _pendingWidth;
    private int _pendingHeight;
    private bool _needsFullRedraw;
    
    /// <summary>
    /// Creates a new resize debounce filter.
    /// </summary>
    /// <param name="debounceMs">
    /// Time to wait after a resize event before allowing output (default: 50ms).
    /// Higher values provide smoother resize but increase latency to final redraw.
    /// </param>
    public ResizeDebounceFilter(int debounceMs = 50)
    {
        _debounceMs = debounceMs;
    }

    /// <inheritdoc />
    public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp)
    {
        _lastResizeTicks = 0;
        _pendingWidth = width;
        _pendingHeight = height;
        _needsFullRedraw = false;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnOutputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed)
    {
        // Record when resize happened and the target dimensions
        Interlocked.Exchange(ref _pendingWidth, width);
        Interlocked.Exchange(ref _pendingHeight, height);
        Interlocked.Exchange(ref _lastResizeTicks, DateTime.UtcNow.Ticks);
        _needsFullRedraw = true;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnSessionEndAsync(TimeSpan elapsed)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> TransformOutputAsync(
        ReadOnlyMemory<byte> originalOutput,
        TerminalCell[,] screenBuffer,
        int width,
        int height,
        TimeSpan elapsed)
    {
        var ticksSinceResize = DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastResizeTicks);
        var msSinceResize = ticksSinceResize / TimeSpan.TicksPerMillisecond;
        
        // If we're within the debounce window, suppress output
        if (msSinceResize < _debounceMs)
        {
            return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        }
        
        // Check if the screen buffer dimensions match expected dimensions
        var bufferHeight = screenBuffer.GetLength(0);
        var bufferWidth = screenBuffer.GetLength(1);
        var expectedWidth = Interlocked.CompareExchange(ref _pendingWidth, 0, 0);
        var expectedHeight = Interlocked.CompareExchange(ref _pendingHeight, 0, 0);
        
        // If dimensions don't match, the terminal state isn't ready for the new size yet
        // Suppress output and wait for matching dimensions
        if (bufferWidth != expectedWidth || bufferHeight != expectedHeight ||
            width != expectedWidth || height != expectedHeight)
        {
            return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        }
        
        // Debounce window expired and dimensions match
        // If we need a full redraw after resize, generate it from the screen buffer
        if (_needsFullRedraw)
        {
            _needsFullRedraw = false;
            var fullRedraw = GenerateFullRedraw(screenBuffer, width, height);
            return ValueTask.FromResult(fullRedraw);
        }
        
        // Normal operation - pass through
        return ValueTask.FromResult(originalOutput);
    }

    /// <summary>
    /// Generates ANSI sequences to redraw the entire screen from the terminal state.
    /// </summary>
    private static ReadOnlyMemory<byte> GenerateFullRedraw(TerminalCell[,] screenBuffer, int width, int height)
    {
        var sb = new StringBuilder();
        
        // Clear screen and reset cursor to home
        sb.Append("\x1b[2J\x1b[H");
        
        // Reset all attributes
        sb.Append("\x1b[0m");
        
        Hex1bColor? currentFg = null;
        Hex1bColor? currentBg = null;
        CellAttributes currentAttrs = CellAttributes.None;
        
        for (int y = 0; y < height; y++)
        {
            // Move to start of line
            sb.Append($"\x1b[{y + 1};1H");
            
            for (int x = 0; x < width; x++)
            {
                var cell = screenBuffer[y, x];
                
                // Skip continuation cells (empty string indicates a wide char continuation)
                if (cell.Character == "")
                    continue;
                
                // Update attributes if different
                if (cell.Attributes != currentAttrs)
                {
                    // Reset and reapply (simpler than tracking individual changes)
                    sb.Append("\x1b[0m");
                    currentFg = null;
                    currentBg = null;
                    currentAttrs = CellAttributes.None;
                    
                    if ((cell.Attributes & CellAttributes.Bold) != 0)
                        sb.Append("\x1b[1m");
                    if ((cell.Attributes & CellAttributes.Dim) != 0)
                        sb.Append("\x1b[2m");
                    if ((cell.Attributes & CellAttributes.Italic) != 0)
                        sb.Append("\x1b[3m");
                    if ((cell.Attributes & CellAttributes.Underline) != 0)
                        sb.Append("\x1b[4m");
                    if ((cell.Attributes & CellAttributes.Blink) != 0)
                        sb.Append("\x1b[5m");
                    if ((cell.Attributes & CellAttributes.Reverse) != 0)
                        sb.Append("\x1b[7m");
                    if ((cell.Attributes & CellAttributes.Hidden) != 0)
                        sb.Append("\x1b[8m");
                    if ((cell.Attributes & CellAttributes.Strikethrough) != 0)
                        sb.Append("\x1b[9m");
                    
                    currentAttrs = cell.Attributes;
                }
                
                // Update foreground color if different
                if (!Equals(cell.Foreground, currentFg))
                {
                    if (cell.Foreground.HasValue)
                    {
                        var fg = cell.Foreground.Value;
                        sb.Append($"\x1b[38;2;{fg.R};{fg.G};{fg.B}m");
                    }
                    else
                    {
                        sb.Append("\x1b[39m");
                    }
                    currentFg = cell.Foreground;
                }
                
                // Update background color if different
                if (!Equals(cell.Background, currentBg))
                {
                    if (cell.Background.HasValue)
                    {
                        var bg = cell.Background.Value;
                        sb.Append($"\x1b[48;2;{bg.R};{bg.G};{bg.B}m");
                    }
                    else
                    {
                        sb.Append("\x1b[49m");
                    }
                    currentBg = cell.Background;
                }
                
                // Write the character
                sb.Append(cell.Character ?? " ");
            }
        }
        
        // Reset attributes at end
        sb.Append("\x1b[0m");
        
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
