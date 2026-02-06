using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Surfaces;

/// <summary>
/// A render context that writes to a <see cref="Surface"/> instead of emitting ANSI strings.
/// </summary>
/// <remarks>
/// <para>
/// This context extends <see cref="Hex1bRenderContext"/> but captures
/// rendered output to a Surface for later diffing and optimized output generation.
/// </para>
/// <para>
/// ANSI escape codes embedded in text are parsed and converted to SurfaceCell properties
/// (foreground, background, attributes). This allows existing node rendering code to work
/// unchanged while benefiting from Surface-based diffing.
/// </para>
/// </remarks>
public class SurfaceRenderContext : Hex1bRenderContext
{
    private readonly Surface _surface;
    private int _cursorX;
    private int _cursorY;
    
    // Coordinate offset for child rendering - allows absolute coordinates to map to a smaller surface
    private int _offsetX;
    private int _offsetY;
    
    /// <summary>
    /// Gets the X offset applied to coordinates.
    /// </summary>
    internal int OffsetX => _offsetX;
    
    /// <summary>
    /// Gets the Y offset applied to coordinates.
    /// </summary>
    internal int OffsetY => _offsetY;
    
    /// <summary>
    /// Gets the cell metrics (pixel dimensions per cell).
    /// </summary>
    public CellMetrics CellMetrics { get; init; } = CellMetrics.Default;
    
    // Current style state (from parsed ANSI codes)
    private Hex1bColor? _currentForeground;
    private Hex1bColor? _currentBackground;
    private CellAttributes _currentAttributes;
    
    // Current hyperlink state (from parsed OSC 8 sequences)
    private TrackedObject<HyperlinkData>? _currentHyperlink;
    
    // Store for tracking hyperlink objects (for deduplication and reference counting)
    private readonly TrackedObjectStore _trackedObjects;
    
    /// <summary>
    /// Gets the tracked object store for creating sixels and hyperlinks.
    /// </summary>
    internal TrackedObjectStore TrackedObjectStore => _trackedObjects;

    /// <summary>
    /// Creates a new SurfaceRenderContext that writes to the specified surface.
    /// </summary>
    /// <param name="surface">The surface to write to.</param>
    /// <param name="theme">The theme to use for styling. Defaults to <see cref="Hex1bThemes.Default"/>.</param>
    public SurfaceRenderContext(Surface surface, Hex1bTheme? theme = null)
        : base(theme)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _trackedObjects = new TrackedObjectStore();
    }
    
    /// <summary>
    /// Creates a new SurfaceRenderContext with coordinate offset for child rendering.
    /// </summary>
    /// <param name="surface">The surface to write to.</param>
    /// <param name="offsetX">X offset to subtract from all coordinates.</param>
    /// <param name="offsetY">Y offset to subtract from all coordinates.</param>
    /// <param name="theme">The theme to use for styling. Defaults to <see cref="Hex1bThemes.Default"/>.</param>
    public SurfaceRenderContext(Surface surface, int offsetX, int offsetY, Hex1bTheme? theme = null)
        : base(theme)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _offsetX = offsetX;
        _offsetY = offsetY;
        _trackedObjects = new TrackedObjectStore();
    }
    
    /// <summary>
    /// Creates a new SurfaceRenderContext with coordinate offset for child rendering,
    /// sharing the parent's tracked object store.
    /// </summary>
    internal SurfaceRenderContext(Surface surface, int offsetX, int offsetY, Hex1bTheme? theme, TrackedObjectStore sharedStore)
        : base(theme)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _offsetX = offsetX;
        _offsetY = offsetY;
        _trackedObjects = sharedStore ?? new TrackedObjectStore();
    }

    /// <summary>
    /// Gets the width of the surface.
    /// </summary>
    public override int Width => _surface.Width;

    /// <summary>
    /// Gets the height of the surface.
    /// </summary>
    public override int Height => _surface.Height;

    /// <summary>
    /// Gets terminal capabilities. For Surface rendering, reports modern capabilities.
    /// </summary>
    public override TerminalCapabilities Capabilities => TerminalCapabilities.Modern;

    /// <summary>
    /// Gets the underlying surface being written to.
    /// </summary>
    public Surface Surface => _surface;

    /// <summary>
    /// Sets the cursor position for subsequent writes.
    /// </summary>
    public override void SetCursorPosition(int left, int top)
    {
        _cursorX = left;
        _cursorY = top;
    }

    /// <summary>
    /// Writes text at the current cursor position, parsing any embedded ANSI codes.
    /// </summary>
    public override void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        WriteToSurface(_cursorX, _cursorY, text, updateCursor: true);
    }

    /// <summary>
    /// Writes text at the specified position, respecting the current layout provider's clipping.
    /// </summary>
    public override void WriteClipped(int x, int y, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (CurrentLayoutProvider == null)
        {
            SetCursorPosition(x, y);
            WriteToSurface(x, y, text, updateCursor: true);
            return;
        }

        // Apply clipping from the layout provider
        var (adjustedX, clippedText) = CurrentLayoutProvider.ClipString(x, y, text);
        if (clippedText.Length > 0)
        {
            SetCursorPosition(adjustedX, y);
            WriteToSurface(adjustedX, y, clippedText, updateCursor: true);
        }
    }

    /// <summary>
    /// Clears a rectangular region by filling with spaces.
    /// </summary>
    public override void ClearRegion(Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // Clamp to surface bounds
        var startX = Math.Max(0, rect.X);
        var startY = Math.Max(0, rect.Y);
        var endX = Math.Min(Width, rect.X + rect.Width);
        var endY = Math.Min(Height, rect.Y + rect.Height);

        if (startX >= endX || startY >= endY) return;

        // Use global background color from theme if set
        var bg = Theme.GetGlobalBackground();
        Hex1bColor? bgColor = bg.IsDefault ? null : bg;

        var clearCell = new SurfaceCell(" ", null, bgColor);
        _surface.Fill(new Rect(startX, startY, endX - startX, endY - startY), clearCell);
    }

    /// <summary>
    /// Clears the entire surface.
    /// </summary>
    public override void Clear()
    {
        var bg = Theme.GetGlobalBackground();
        Hex1bColor? bgColor = bg.IsDefault ? null : bg;
        var clearCell = new SurfaceCell(" ", null, bgColor);
        _surface.Fill(new Rect(0, 0, Width, Height), clearCell);
    }

    /// <summary>
    /// Checks if a position should be rendered based on the current layout provider.
    /// </summary>
    public override bool ShouldRenderAt(int x, int y)
    {
        return CurrentLayoutProvider?.ShouldRenderAt(x, y) ?? true;
    }

    /// <summary>
    /// Signals the beginning of a render frame. No-op for Surface rendering
    /// as the Surface itself handles frame boundaries.
    /// </summary>
    public override void BeginFrame()
    {
        // No-op - Surface handles this differently
    }

    /// <summary>
    /// Signals the end of a render frame. No-op for Surface rendering.
    /// </summary>
    public override void EndFrame()
    {
        // No-op - Surface handles this differently
    }

    /// <summary>
    /// Enter alternate screen - no-op for Surface rendering.
    /// </summary>
    public override void EnterAlternateScreen()
    {
        // No-op - lifecycle managed by Hex1bApp
    }

    /// <summary>
    /// Exit alternate screen - no-op for Surface rendering.
    /// </summary>
    public override void ExitAlternateScreen()
    {
        // No-op - lifecycle managed by Hex1bApp
    }

    /// <summary>
    /// Resets the current style state to defaults.
    /// </summary>
    public void ResetStyle()
    {
        _currentForeground = null;
        _currentBackground = null;
        _currentAttributes = CellAttributes.None;
        // Note: We don't reset hyperlink here - OSC 8 reset is explicit with empty URI
    }
    
    /// <summary>
    /// Whether render caching is enabled. When true, RenderChild will use cached
    /// surfaces for nodes that are not dirty. Default is true.
    /// </summary>
    public bool CachingEnabled { get; set; } = true;
    
    /// <summary>
    /// Statistics about cache usage for the current frame.
    /// </summary>
    public int CacheHits { get; private set; }
    public int CacheMisses { get; private set; }
    
    /// <summary>
    /// Resets cache statistics. Call at the start of each frame.
    /// </summary>
    public void ResetCacheStats()
    {
        CacheHits = 0;
        CacheMisses = 0;
    }

    /// <summary>
    /// Renders a child node with caching support.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the child is not dirty and has a valid cached surface that matches its
    /// current bounds, the cached surface is composited directly. Otherwise, the
    /// child is rendered to a new surface which is then cached.
    /// </para>
    /// <para>
    /// Container nodes should call this method instead of child.Render(context)
    /// to benefit from automatic caching.
    /// </para>
    /// </remarks>
    public override void RenderChild(Hex1bNode child)
    {
        if (child == null) return;
        
        // If caching is disabled, just render normally
        if (!CachingEnabled)
        {
            SetCursorPosition(child.Bounds.X, child.Bounds.Y);
            child.Render(this);
            return;
        }
        
        // Check if we can use cached surface
        // CRITICAL: Must use NeedsRender() not just IsDirty - a node's cache is invalid
        // if ANY descendant is dirty, because skipping render would skip the descendants too
        var canUseCache = !child.NeedsRender() 
            && child.CachedSurface != null 
            && child.CachedBounds == child.Bounds
            && child.CachedSurface.Width == child.Bounds.Width
            && child.CachedSurface.Height == child.Bounds.Height;
        
        if (canUseCache)
        {
            // Cache hit - composite cached surface at RELATIVE position
            // (child.Bounds are absolute, but _surface may have its own offset)
            CacheHits++;
            // If there's a layout provider, clip to its bounds
            Rect? clipRect = null;
            if (CurrentLayoutProvider != null)
            {
                // Convert ClipRect to surface-relative coordinates
                var providerClip = CurrentLayoutProvider.ClipRect;
                clipRect = new Rect(
                    providerClip.X - _offsetX,
                    providerClip.Y - _offsetY,
                    providerClip.Width,
                    providerClip.Height
                );
            }
            _surface.Composite(child.CachedSurface!, child.Bounds.X - _offsetX, child.Bounds.Y - _offsetY, clipRect);
        }
        else
        {
            // Cache miss - render and cache
            CacheMisses++;
            
            // Only cache if the node has non-zero bounds
            if (child.Bounds.Width > 0 && child.Bounds.Height > 0)
            {
                // Create a surface for this child's content with matching cell metrics
                var childSurface = new Surface(child.Bounds.Width, child.Bounds.Height, CellMetrics);
                
                // Create context with offset so child's absolute coordinates map to surface (0,0)
                // Share the tracked object store so sixels created by children are properly tracked
                // Note: We don't pass CurrentLayoutProvider to the child context because:
                // 1. The child surface already represents the child's bounds
                // 2. The parent's layout provider would clip incorrectly
                // 3. Clipping happens implicitly via surface bounds
                var childContext = new SurfaceRenderContext(childSurface, child.Bounds.X, child.Bounds.Y, Theme, _trackedObjects)
                {
                    CachingEnabled = CachingEnabled,
                    MouseX = MouseX,  // Pass mouse position to children
                    MouseY = MouseY,
                    CellMetrics = CellMetrics  // Propagate cell metrics for sixel sizing
                    // CurrentLayoutProvider intentionally not set - child renders in its own coordinate space
                };
                
                // Set cursor position to child's origin so Write() calls work correctly
                // (the offset will translate this to 0,0 on the child surface)
                childContext.SetCursorPosition(child.Bounds.X, child.Bounds.Y);
                
                // Render to the child surface (child uses its normal absolute coordinates,
                // context translates them via the offset)
                child.Render(childContext);
                
                // Cache the result
                child.CachedSurface = childSurface;
                child.CachedBounds = child.Bounds;
                
                // Composite onto our surface at RELATIVE position
                // (child.Bounds are absolute, but _surface may have its own offset)
                // If there's a layout provider, clip to its bounds
                Rect? clipRect = null;
                if (CurrentLayoutProvider != null)
                {
                    // Convert ClipRect to surface-relative coordinates
                    var providerClip = CurrentLayoutProvider.ClipRect;
                    clipRect = new Rect(
                        providerClip.X - _offsetX,
                        providerClip.Y - _offsetY,
                        providerClip.Width,
                        providerClip.Height
                    );
                }
                _surface.Composite(childSurface, child.Bounds.X - _offsetX, child.Bounds.Y - _offsetY, clipRect);
            }
            else
            {
                // Zero-sized node, just call Render directly
                SetCursorPosition(child.Bounds.X, child.Bounds.Y);
                child.Render(this);
            }
        }
    }

    #region ANSI Parsing and Surface Writing

    /// <summary>
    /// Writes text to the surface, parsing ANSI escape codes.
    /// Uses grapheme cluster enumeration for proper Unicode handling.
    /// </summary>
    private void WriteToSurface(int x, int y, string text, bool updateCursor)
    {
        // Apply coordinate offset for child surface rendering
        var writeX = x - _offsetX;
        var writeY = y - _offsetY;
        var i = 0;

        while (i < text.Length)
        {
            // Check for escape sequence
            if (text[i] == '\x1b' && i + 1 < text.Length)
            {
                var consumed = ParseEscapeSequence(text, i);
                if (consumed > 0)
                {
                    i += consumed;
                    continue;
                }
            }

            // Find the extent of the current grapheme cluster
            var grapheme = GetNextGrapheme(text, i, out var charCount);
            
            // Write to surface if in bounds (using offset-adjusted coordinates)
            if (writeX >= 0 && writeX < _surface.Width && writeY >= 0 && writeY < _surface.Height)
            {
                var displayWidth = DisplayWidth.GetGraphemeWidth(grapheme);
                
                // Add ref to hyperlink if present (each cell holds a reference)
                _currentHyperlink?.AddRef();

                // Check if this is a wide character
                if (displayWidth == 2)
                {
                    // Write main cell
                    _surface[writeX, writeY] = new SurfaceCell(
                        grapheme,
                        _currentForeground,
                        _currentBackground,
                        _currentAttributes,
                        displayWidth,
                        Hyperlink: _currentHyperlink);

                    // Write continuation cell if space allows
                    if (writeX + 1 < _surface.Width)
                    {
                        _surface[writeX + 1, writeY] = SurfaceCell.CreateContinuation(_currentBackground);
                    }

                    writeX += 2;
                }
                else if (displayWidth == 1)
                {
                    _surface[writeX, writeY] = new SurfaceCell(
                        grapheme,
                        _currentForeground,
                        _currentBackground,
                        _currentAttributes,
                        displayWidth,
                        Hyperlink: _currentHyperlink);
                    writeX++;
                }
                else
                {
                    // Zero-width - release the ref we added
                    _currentHyperlink?.Release();
                }
                // Zero-width characters are ignored for cursor advancement
            }
            else
            {
                // Out of bounds - still need to advance cursor based on display width
                var displayWidth = DisplayWidth.GetGraphemeWidth(grapheme);
                writeX += displayWidth;
            }

            i += charCount;
        }

        if (updateCursor)
        {
            // Store cursor in original coordinate space (without offset applied)
            _cursorX = writeX + _offsetX;
            _cursorY = writeY + _offsetY;
        }
    }
    
    /// <summary>
    /// Gets the next grapheme cluster from the string starting at the given index.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="start">The starting index.</param>
    /// <param name="charCount">The number of UTF-16 chars consumed.</param>
    /// <returns>The grapheme cluster as a string.</returns>
    private static string GetNextGrapheme(string text, int start, out int charCount)
    {
        if (start >= text.Length)
        {
            charCount = 0;
            return "";
        }
        
        // Use .NET's grapheme cluster enumeration to properly handle:
        // - Surrogate pairs (emoji like 🖥)
        // - Combining characters (variation selectors like U+FE0F)
        // - Extended grapheme clusters (emoji + skin tones, ZWJ sequences)
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text, start);
        if (enumerator.MoveNext())
        {
            var grapheme = enumerator.GetTextElement();
            charCount = grapheme.Length;
            return grapheme;
        }
        
        // Fallback (shouldn't happen)
        charCount = 1;
        return text[start].ToString();
    }

    /// <summary>
    /// Parses an escape sequence starting at the given index.
    /// Returns the number of characters consumed, or 0 if not a recognized sequence.
    /// </summary>
    private int ParseEscapeSequence(string text, int start)
    {
        if (start + 1 >= text.Length)
            return 0;

        var next = text[start + 1];

        // CSI sequence: ESC [
        if (next == '[')
        {
            return ParseCsiSequence(text, start);
        }

        // OSC sequence: ESC ] (hyperlinks, etc.)
        if (next == ']')
        {
            return ParseOscSequence(text, start);
        }

        // DCS sequence: ESC P (sixels) - skip for now
        if (next == 'P')
        {
            return SkipDcsSequence(text, start);
        }

        // APC sequence: ESC _ (frame markers) - skip
        if (next == '_')
        {
            return SkipApcSequence(text, start);
        }

        return 0;
    }

    /// <summary>
    /// Parses a CSI (Control Sequence Introducer) sequence.
    /// </summary>
    private int ParseCsiSequence(string text, int start)
    {
        // Find the end of the sequence (final byte in range 0x40-0x7E)
        var i = start + 2; // Skip ESC [
        while (i < text.Length)
        {
            var c = text[i];
            if (c >= 0x40 && c <= 0x7E)
            {
                // This is the final byte
                var seqLength = i - start + 1;
                var sequence = text.Substring(start + 2, i - start - 2);

                if (c == 'm')
                {
                    // SGR sequence - parse colors and attributes
                    ParseSgrSequence(sequence);
                }

                return seqLength;
            }
            i++;
        }

        return 0;
    }

    /// <summary>
    /// Parses an SGR (Select Graphic Rendition) sequence.
    /// </summary>
    private void ParseSgrSequence(string parameters)
    {
        if (string.IsNullOrEmpty(parameters))
        {
            // ESC[m is equivalent to ESC[0m (reset)
            ResetStyle();
            return;
        }

        var parts = parameters.Split(';');
        var i = 0;

        while (i < parts.Length)
        {
            if (!int.TryParse(parts[i], out var code))
            {
                i++;
                continue;
            }

            switch (code)
            {
                case 0: // Reset
                    ResetStyle();
                    break;
                case 1: // Bold
                    _currentAttributes |= CellAttributes.Bold;
                    break;
                case 2: // Dim
                    _currentAttributes |= CellAttributes.Dim;
                    break;
                case 3: // Italic
                    _currentAttributes |= CellAttributes.Italic;
                    break;
                case 4: // Underline
                    _currentAttributes |= CellAttributes.Underline;
                    break;
                case 5: // Blink
                    _currentAttributes |= CellAttributes.Blink;
                    break;
                case 7: // Reverse
                    _currentAttributes |= CellAttributes.Reverse;
                    break;
                case 8: // Hidden
                    _currentAttributes |= CellAttributes.Hidden;
                    break;
                case 9: // Strikethrough
                    _currentAttributes |= CellAttributes.Strikethrough;
                    break;
                case 22: // Normal intensity (not bold, not dim)
                    _currentAttributes &= ~(CellAttributes.Bold | CellAttributes.Dim);
                    break;
                case 23: // Not italic
                    _currentAttributes &= ~CellAttributes.Italic;
                    break;
                case 24: // Not underlined
                    _currentAttributes &= ~CellAttributes.Underline;
                    break;
                case 25: // Not blinking
                    _currentAttributes &= ~CellAttributes.Blink;
                    break;
                case 27: // Not reversed
                    _currentAttributes &= ~CellAttributes.Reverse;
                    break;
                case 28: // Not hidden
                    _currentAttributes &= ~CellAttributes.Hidden;
                    break;
                case 29: // Not strikethrough
                    _currentAttributes &= ~CellAttributes.Strikethrough;
                    break;
                case 38: // Foreground color (extended)
                    i = ParseExtendedColor(parts, i, isForeground: true);
                    break;
                case 39: // Default foreground
                    _currentForeground = null;
                    break;
                case 48: // Background color (extended)
                    i = ParseExtendedColor(parts, i, isForeground: false);
                    break;
                case 49: // Default background
                    _currentBackground = null;
                    break;
                case 53: // Overline
                    _currentAttributes |= CellAttributes.Overline;
                    break;
                case 55: // Not overlined
                    _currentAttributes &= ~CellAttributes.Overline;
                    break;
                default:
                    // Handle basic 16 colors (30-37 fg, 40-47 bg, 90-97 bright fg, 100-107 bright bg)
                    if (code >= 30 && code <= 37)
                    {
                        _currentForeground = GetBasicColor(code - 30);
                    }
                    else if (code >= 40 && code <= 47)
                    {
                        _currentBackground = GetBasicColor(code - 40);
                    }
                    else if (code >= 90 && code <= 97)
                    {
                        _currentForeground = GetBrightColor(code - 90);
                    }
                    else if (code >= 100 && code <= 107)
                    {
                        _currentBackground = GetBrightColor(code - 100);
                    }
                    break;
            }

            i++;
        }
    }

    /// <summary>
    /// Parses extended color (256-color or 24-bit RGB).
    /// </summary>
    private int ParseExtendedColor(string[] parts, int index, bool isForeground)
    {
        if (index + 1 >= parts.Length)
            return index;

        if (!int.TryParse(parts[index + 1], out var colorType))
            return index;

        if (colorType == 2 && index + 4 < parts.Length)
        {
            // 24-bit RGB: 38;2;r;g;b or 48;2;r;g;b
            if (int.TryParse(parts[index + 2], out var r) &&
                int.TryParse(parts[index + 3], out var g) &&
                int.TryParse(parts[index + 4], out var b))
            {
                var color = Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
                if (isForeground)
                    _currentForeground = color;
                else
                    _currentBackground = color;
            }
            return index + 4;
        }
        else if (colorType == 5 && index + 2 < parts.Length)
        {
            // 256-color: 38;5;n or 48;5;n
            if (int.TryParse(parts[index + 2], out var colorIndex))
            {
                var color = Get256Color(colorIndex);
                if (isForeground)
                    _currentForeground = color;
                else
                    _currentBackground = color;
            }
            return index + 2;
        }

        return index;
    }

    /// <summary>
    /// Parses an OSC sequence (ESC ] ... ST) and handles OSC 8 hyperlinks.
    /// </summary>
    /// <remarks>
    /// OSC 8 format: ESC ] 8 ; params ; URI ST
    /// where ST is either ESC \ or BEL (\x07)
    /// An empty URI ends the hyperlink.
    /// </remarks>
    private int ParseOscSequence(string text, int start)
    {
        var i = start + 2; // Skip ESC ]
        var contentStart = i;
        var contentEnd = -1;
        var terminatorLength = 0;
        
        // Find the terminator (ST or BEL)
        while (i < text.Length)
        {
            if (text[i] == '\x07')
            {
                contentEnd = i;
                terminatorLength = 1;
                break;
            }
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '\\')
            {
                contentEnd = i;
                terminatorLength = 2;
                break;
            }
            i++;
        }
        
        if (contentEnd < 0)
            return 0; // Incomplete sequence
        
        var content = text.Substring(contentStart, contentEnd - contentStart);
        
        // Parse OSC command number
        var firstSemicolon = content.IndexOf(';');
        if (firstSemicolon < 0)
        {
            // No command parameters, skip
            return contentEnd - start + terminatorLength;
        }
        
        var command = content[..firstSemicolon];
        
        // Handle OSC 8 (hyperlinks)
        if (command == "8")
        {
            // Format: 8;params;URI
            var rest = content[(firstSemicolon + 1)..];
            var secondSemicolon = rest.IndexOf(';');
            
            if (secondSemicolon >= 0)
            {
                var parameters = rest[..secondSemicolon];
                var uri = rest[(secondSemicolon + 1)..];
                
                if (string.IsNullOrEmpty(uri))
                {
                    // Empty URI ends the hyperlink
                    _currentHyperlink?.Release();
                    _currentHyperlink = null;
                }
                else
                {
                    // Start a new hyperlink
                    _currentHyperlink?.Release(); // Release any previous
                    _currentHyperlink = _trackedObjects.GetOrCreateHyperlink(uri, parameters);
                }
            }
        }
        
        return contentEnd - start + terminatorLength;
    }
    
    /// <summary>
    /// Skips a DCS sequence (ESC P ... ST).
    /// </summary>
    private static int SkipDcsSequence(string text, int start)
    {
        var i = start + 2;
        while (i < text.Length)
        {
            // Look for ST (ESC \)
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '\\')
                return i - start + 2;
            i++;
        }
        return 0;
    }

    /// <summary>
    /// Skips an APC sequence (ESC _ ... ST).
    /// </summary>
    private static int SkipApcSequence(string text, int start)
    {
        var i = start + 2;
        while (i < text.Length)
        {
            // Look for ST (ESC \)
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '\\')
                return i - start + 2;
            i++;
        }
        return 0;
    }

    #endregion

    #region Color Helpers

    private static Hex1bColor GetBasicColor(int index) => index switch
    {
        0 => Hex1bColor.FromRgb(0, 0, 0),       // Black
        1 => Hex1bColor.FromRgb(128, 0, 0),     // Red
        2 => Hex1bColor.FromRgb(0, 128, 0),     // Green
        3 => Hex1bColor.FromRgb(128, 128, 0),   // Yellow
        4 => Hex1bColor.FromRgb(0, 0, 128),     // Blue
        5 => Hex1bColor.FromRgb(128, 0, 128),   // Magenta
        6 => Hex1bColor.FromRgb(0, 128, 128),   // Cyan
        7 => Hex1bColor.FromRgb(192, 192, 192), // White
        _ => Hex1bColor.FromRgb(128, 128, 128)
    };

    private static Hex1bColor GetBrightColor(int index) => index switch
    {
        0 => Hex1bColor.FromRgb(128, 128, 128), // Bright Black (Gray)
        1 => Hex1bColor.FromRgb(255, 0, 0),     // Bright Red
        2 => Hex1bColor.FromRgb(0, 255, 0),     // Bright Green
        3 => Hex1bColor.FromRgb(255, 255, 0),   // Bright Yellow
        4 => Hex1bColor.FromRgb(0, 0, 255),     // Bright Blue
        5 => Hex1bColor.FromRgb(255, 0, 255),   // Bright Magenta
        6 => Hex1bColor.FromRgb(0, 255, 255),   // Bright Cyan
        7 => Hex1bColor.FromRgb(255, 255, 255), // Bright White
        _ => Hex1bColor.FromRgb(255, 255, 255)
    };

    private static Hex1bColor Get256Color(int index)
    {
        if (index < 16)
        {
            // Basic 16 colors
            return index < 8 ? GetBasicColor(index) : GetBrightColor(index - 8);
        }
        else if (index < 232)
        {
            // 6x6x6 color cube (indices 16-231)
            var i = index - 16;
            var r = (i / 36) * 51;
            var g = ((i / 6) % 6) * 51;
            var b = (i % 6) * 51;
            return Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
        }
        else
        {
            // Grayscale (indices 232-255)
            var gray = (index - 232) * 10 + 8;
            return Hex1bColor.FromRgb((byte)gray, (byte)gray, (byte)gray);
        }
    }

    #endregion
}
