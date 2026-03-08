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
    
    // Terminal capabilities inherited from parent context
    private TerminalCapabilities? _capabilities;

    // KGP occlusion tracking
    private Kgp.KgpImageRegistry? _kgpRegistry;
    private int _kgpLayer;

    // Maximum dimension for a child surface to prevent overflow in width*height allocation.
    // Real terminal content rarely exceeds 10000 rows; this prevents int.MaxValue-sized children
    // (from unconstrained measure passes) from causing OverflowException in Surface allocation.
    private const int MaxSurfaceDimension = 10_000;
    
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
    private UnderlineStyle _currentUnderlineStyle;
    private Hex1bColor? _currentUnderlineColor;
    
    // Current hyperlink state (from parsed OSC 8 sequences)
    private TrackedObject<HyperlinkData>? _currentHyperlink;
    
    // Store for tracking hyperlink objects (for deduplication and reference counting)
    private readonly TrackedObjectStore _trackedObjects;
    
    /// <summary>
    /// Gets the tracked object store for creating sixels and hyperlinks.
    /// </summary>
    internal TrackedObjectStore TrackedObjectStore => _trackedObjects;

    /// <summary>
    /// Metrics instance for per-node render timing. Null when per-node metrics are disabled.
    /// </summary>
    internal Diagnostics.Hex1bMetrics? Metrics { get; init; }

    /// <summary>
    /// Optional surface pool for reusing temporary surfaces during rendering.
    /// </summary>
    internal SurfacePool? SurfacePool { get; init; }

    /// <summary>
    /// Gets or sets the KGP image registry for occlusion tracking.
    /// When set, <see cref="WriteKgp"/> records images in the registry,
    /// and the <see cref="Kgp.KgpOcclusionSolver"/> uses the data post-rendering.
    /// </summary>
    internal Kgp.KgpImageRegistry? KgpRegistry
    {
        get => _kgpRegistry;
        set => _kgpRegistry = value;
    }

    /// <summary>
    /// Advances to the next KGP occlusion layer. Call before rendering each
    /// window or layer group so images and occluders get correct z-levels.
    /// </summary>
    internal void PushKgpLayer()
    {
        _kgpLayer++;
        _kgpRegistry?.PushLayer();
    }

    /// <summary>
    /// Gets the current KGP occlusion layer.
    /// </summary>
    internal int KgpLayer => _kgpLayer;

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
    /// Gets terminal capabilities. Returns capabilities inherited from parent context,
    /// falling back to <see cref="TerminalCapabilities.Modern"/> if not set.
    /// </summary>
    public override TerminalCapabilities Capabilities => _capabilities ?? TerminalCapabilities.Modern;
    
    /// <summary>
    /// Sets the terminal capabilities for this context and child contexts.
    /// Call this to propagate capabilities from a parent render context.
    /// </summary>
    public void SetCapabilities(TerminalCapabilities capabilities) => _capabilities = capabilities;

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
    /// Writes a KGP image at the current cursor position by populating surface cells
    /// with structured <see cref="KgpCellData"/>. The anchor cell (top-left) carries
    /// the KGP metadata; remaining cells in the region are filled with spaces.
    /// <see cref="SurfaceComparer"/> later reads the KGP data and emits transmit/placement
    /// tokens with proper z-ordering.
    /// </summary>
    public override void WriteKgp(byte[] imageData, int pixelWidth, int pixelHeight,
        int cellWidth, int cellHeight, KgpZOrder zOrder,
        int clipX = 0, int clipY = 0, int clipW = 0, int clipH = 0)
    {
        var writeX = _cursorX - _offsetX;
        var writeY = _cursorY - _offsetY;

        if (writeX < 0 || writeY < 0 || writeX >= _surface.Width || writeY >= _surface.Height)
            return;

        var contentHash = System.Security.Cryptography.SHA256.HashData(imageData);
        var imageId = (uint)(contentHash[0] << 24 | contentHash[1] << 16 | contentHash[2] << 8 | contentHash[3]);
        var zIndex = zOrder == KgpZOrder.AboveText ? 1 : -1;

        var base64 = Convert.ToBase64String(imageData);
        var transmitPayload = $"\x1b_Ga=t,f=32,s={pixelWidth},v={pixelHeight},i={imageId},t=d,q=2;{base64}\x1b\\";

        var kgpData = new KgpCellData(
            transmitPayload,
            imageId,
            cellWidth,
            cellHeight,
            (uint)pixelWidth,
            (uint)pixelHeight,
            contentHash,
            clipX: clipX,
            clipY: clipY,
            clipW: clipW,
            clipH: clipH,
            zIndex: zIndex);

        var tracked = _trackedObjects.GetOrCreateKgp(kgpData);

        // Register with the occlusion registry (if active) using absolute coordinates
        _kgpRegistry?.RegisterImage(kgpData, _cursorX, _cursorY);

        // Anchor cell carries the KGP metadata
        tracked.AddRef();
        _surface[writeX, writeY] = new SurfaceCell(" ", null, null, Kgp: tracked);

        // Fill remaining cells in the region with spaces so they occupy space
        for (var dy = 0; dy < cellHeight; dy++)
        {
            for (var dx = 0; dx < cellWidth; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var cx = writeX + dx;
                var cy = writeY + dy;
                if (cx >= 0 && cx < _surface.Width && cy >= 0 && cy < _surface.Height)
                    _surface[cx, cy] = new SurfaceCell(" ", null, null);
            }
        }
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
        _currentUnderlineStyle = UnderlineStyle.None;
        _currentUnderlineColor = null;
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
    /// <summary>
    /// Renders a child node, recording per-node render duration when metrics are enabled.
    /// </summary>
    private void RenderChildTimed(Hex1bNode child, Hex1bRenderContext context)
    {
        var metrics = Metrics;
        if (metrics?.NodeRenderDuration == null)
        {
            child.Render(context);
            return;
        }

        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        child.Render(context);
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
        metrics.NodeRenderDuration.Record(elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("node", child.GetMetricPath()));
    }

    public override void RenderChild(Hex1bNode child)
    {
        if (child == null) return;
        
        // If caching is disabled, render directly into this surface
        // UNLESS there's a layout provider requiring clipping (e.g., ScrollPanel viewport)
        if (!CachingEnabled)
        {
            if (CurrentLayoutProvider != null && child.Bounds.Width > 0 && child.Bounds.Height > 0)
            {
                // Must use a child surface + composite so the clip rect is respected.
                // Without this, content inside ScrollPanels bleeds past the viewport.
                // Clamp dimensions so width*height doesn't overflow int.MaxValue while
                // preserving the child's full extent (needed for scroll offset rendering).
                var clampedWidth = Math.Min(child.Bounds.Width, MaxSurfaceDimension);
                var clampedHeight = Math.Min(child.Bounds.Height, MaxSurfaceDimension);
                var pool = SurfacePool;
                var childSurface = pool != null
                    ? pool.Rent(clampedWidth, clampedHeight, CellMetrics)
                    : new Surface(clampedWidth, clampedHeight, CellMetrics);

                try
                {
                    var childContext = new SurfaceRenderContext(childSurface, child.Bounds.X, child.Bounds.Y, Theme, _trackedObjects)
                    {
                        CachingEnabled = false,
                        MouseX = MouseX,
                        MouseY = MouseY,
                        CellMetrics = CellMetrics,
                        Metrics = Metrics,
                        SurfacePool = pool,
                        _capabilities = _capabilities,
                        _kgpRegistry = _kgpRegistry,
                        _kgpLayer = _kgpLayer
                    };
                    childContext.SetCursorPosition(child.Bounds.X, child.Bounds.Y);
                    RenderChildTimed(child, childContext);

                    if (!child.FillBackground.IsDefault)
                    {
                        childSurface.FillBackground(child.FillBackground);
                    }

                    var providerClip = CurrentLayoutProvider.ClipRect;
                    var clipRect = new Rect(
                        providerClip.X - _offsetX,
                        providerClip.Y - _offsetY,
                        providerClip.Width,
                        providerClip.Height);
                    _surface.Composite(childSurface, child.Bounds.X - _offsetX, child.Bounds.Y - _offsetY, clipRect);
                    return;
                }
                finally
                {
                    if (pool != null)
                        pool.Return(childSurface);
                }
            }

            SetCursorPosition(child.Bounds.X, child.Bounds.Y);
            RenderChildTimed(child, this);
            
            // Post-process: fill transparent backgrounds in the child's region.
            // In direct rendering mode, child writes overwrite fill cells with
            // transparent bg. We fix them up after rendering.
            if (!child.FillBackground.IsDefault && child.Bounds.Width > 0 && child.Bounds.Height > 0)
            {
                _surface.FillBackgroundRegion(child.FillBackground,
                    child.Bounds.X - _offsetX, child.Bounds.Y - _offsetY,
                    child.Bounds.Width, child.Bounds.Height);
            }
            return;
        }
        
        // Check if we can use cached surface.
        // Reconciled trees use an O(1) subtree dirty-version gate.
        // For manually-constructed trees without parent links, fall back to recursive NeedsRender().
        if (!child.IsDirty
            && child.CachedSurface != null
            && child.CachedBounds == child.Bounds
            && child.CachedSurface.Width == child.Bounds.Width
            && child.CachedSurface.Height == child.Bounds.Height)
        {
            var subtreeIsClean = HasConsistentParentLinks(child)
                ? child.CachedSubtreeRenderVersion == child.SubtreeRenderVersion
                : !child.NeedsRender();

            if (subtreeIsClean)
            {
                var cacheHintAllowsReuse = child.CachePredicate?.Invoke(new RenderCacheContext(child, this)) ?? true;
                if (cacheHintAllowsReuse)
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
                    return;
                }
            }
        }

        // Cache miss - render and cache
        CacheMisses++;
        
        // Only cache if the node has non-zero bounds
        if (child.Bounds.Width > 0 && child.Bounds.Height > 0)
        {
            // Create a surface for this child's content with matching cell metrics
            // Clamp dimensions to prevent overflow with unconstrained children
            var clampedWidth = Math.Min(child.Bounds.Width, MaxSurfaceDimension);
            var clampedHeight = Math.Min(child.Bounds.Height, MaxSurfaceDimension);
            var childSurface = new Surface(clampedWidth, clampedHeight, CellMetrics);
            var subtreeVersionBeforeRender = child.SubtreeRenderVersion;
            
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
                CellMetrics = CellMetrics,  // Propagate cell metrics for sixel sizing
                Metrics = Metrics,
                _capabilities = _capabilities,
                _kgpRegistry = _kgpRegistry,
                _kgpLayer = _kgpLayer
                // CurrentLayoutProvider intentionally not set - child renders in its own coordinate space
            };
            
            // Set cursor position to child's origin so Write() calls work correctly
            // (the offset will translate this to 0,0 on the child surface)
            childContext.SetCursorPosition(child.Bounds.X, child.Bounds.Y);
            
            // Render to the child surface (child uses its normal absolute coordinates,
            // context translates them via the offset)
            RenderChildTimed(child, childContext);
            
            // Post-process: fill transparent backgrounds with the node's fill color.
            // This prevents background bleed-through in layered compositing by ensuring
            // all cells on this surface have an explicit background color.
            if (!child.FillBackground.IsDefault)
            {
                childSurface.FillBackground(child.FillBackground);
            }
            
            // Cache the result
            child.CachedSurface = childSurface;
            child.CachedBounds = child.Bounds;
            child.CachedSubtreeRenderVersion = subtreeVersionBeforeRender;
            
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
            RenderChildTimed(child, this);
        }
    }

    private static bool HasConsistentParentLinks(Hex1bNode node)
    {
        foreach (var child in node.GetChildren())
        {
            if (!ReferenceEquals(child.Parent, node))
                return false;
        }

        return true;
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
                        Hyperlink: _currentHyperlink,
                        UnderlineStyle: _currentUnderlineStyle,
                        UnderlineColor: _currentUnderlineColor);

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
                        Hyperlink: _currentHyperlink,
                        UnderlineStyle: _currentUnderlineStyle,
                        UnderlineColor: _currentUnderlineColor);
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

        // PERF: StringInfo.GetTextElementEnumerator allocates a new string per call.
        // GetNextGrapheme is called for EVERY character written to a surface cell during
        // rendering, making it one of the highest-frequency allocation sites.
        //
        // For printable ASCII (0x20–0x7E), each char is always a complete single-char grapheme.
        // char.ToString() for chars <= 0x7F returns a cached (interned) string in .NET 6+,
        // making this path entirely allocation-free.
        //
        // PITFALL: We must also check that the NEXT character is ASCII (or end-of-string).
        // Keycap emoji sequences start with an ASCII char followed by U+FE0F (variation
        // selector) + U+20E3 (combining enclosing keycap), e.g. 1️⃣ = '1' + U+FE0F + U+20E3.
        // Without the next-char guard, we'd return '1' as a standalone grapheme and break
        // the keycap sequence. All Unicode combining marks and variation selectors are >= 0x80,
        // so checking text[start+1] < 0x80 is sufficient to detect this.
        var ch = text[start];
        if (ch >= 0x20 && ch < 0x7F
            && (start + 1 >= text.Length || text[start + 1] < 0x80))
        {
            charCount = 1;
            return ch.ToString();
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
        return ch.ToString();
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
            var part = parts[i];

            // Handle colon-separated sub-parameters (e.g., "4:3" for curly underline)
            if (part.Contains(':'))
            {
                ParseSgrColonSubParams(part);
                i++;
                continue;
            }

            if (!int.TryParse(part, out var code))
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
                case 4: // Underline (basic — single)
                    _currentAttributes |= CellAttributes.Underline;
                    _currentUnderlineStyle = UnderlineStyle.Single;
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
                case 21: // Double underline (ECMA-48)
                    _currentAttributes |= CellAttributes.Underline;
                    _currentUnderlineStyle = UnderlineStyle.Double;
                    break;
                case 22: // Normal intensity (not bold, not dim)
                    _currentAttributes &= ~(CellAttributes.Bold | CellAttributes.Dim);
                    break;
                case 23: // Not italic
                    _currentAttributes &= ~CellAttributes.Italic;
                    break;
                case 24: // Not underlined
                    _currentAttributes &= ~CellAttributes.Underline;
                    _currentUnderlineStyle = UnderlineStyle.None;
                    _currentUnderlineColor = null;
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
                case 58: // Underline color (extended — semicolon syntax)
                    i = ParseUnderlineColor(parts, i);
                    break;
                case 59: // Default underline color
                    _currentUnderlineColor = null;
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
    /// Handles SGR parameters with colon-separated sub-parameters (e.g., "4:3" for curly underline,
    /// "58:2:R:G:B" for underline color).
    /// </summary>
    private void ParseSgrColonSubParams(string part)
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
                            _ => UnderlineStyle.Single,
                        };
                    }
                }
                break;

            case 58: // Underline color (colon syntax: 58:2:R:G:B or 58:2:CS:R:G:B)
                ParseColonUnderlineColor(subs);
                break;
        }
    }

    /// <summary>
    /// Parses underline color from colon-separated sub-parameters (58:2:R:G:B or 58:2:CS:R:G:B).
    /// </summary>
    private void ParseColonUnderlineColor(string[] subs)
    {
        if (subs.Length < 3) return;
        if (!int.TryParse(subs[1], out var colorType)) return;

        if (colorType == 2) // RGB
        {
            // Format: 58:2:R:G:B or 58:2:CS:R:G:B (CS = colorspace, optional)
            int r, g, b;
            if (subs.Length >= 5 &&
                int.TryParse(subs[2], out r) &&
                int.TryParse(subs[3], out g) &&
                int.TryParse(subs[4], out b) &&
                r is >= 0 and <= 255 && g is >= 0 and <= 255 && b is >= 0 and <= 255)
            {
                _currentUnderlineColor = Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
            }
            else if (subs.Length >= 6 &&
                int.TryParse(subs[3], out r) &&
                int.TryParse(subs[4], out g) &&
                int.TryParse(subs[5], out b) &&
                r is >= 0 and <= 255 && g is >= 0 and <= 255 && b is >= 0 and <= 255)
            {
                // 58:2:CS:R:G:B — skip colorspace index
                _currentUnderlineColor = Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
            }
        }
        else if (colorType == 5 && subs.Length >= 3) // 256-color
        {
            // 256-color underline is rare; store as-is would require palette lookup.
            // Skip for now — RGB (type 2) is the common path.
        }
    }

    /// <summary>
    /// Parses underline color from semicolon-separated parameters (58;2;R;G;B or 58;5;N).
    /// </summary>
    private int ParseUnderlineColor(string[] parts, int index)
    {
        if (index + 1 >= parts.Length)
            return index;

        if (!int.TryParse(parts[index + 1], out var colorType))
            return index;

        if (colorType == 2 && index + 4 < parts.Length)
        {
            // 58;2;R;G;B
            if (int.TryParse(parts[index + 2], out var r) &&
                int.TryParse(parts[index + 3], out var g) &&
                int.TryParse(parts[index + 4], out var b) &&
                r is >= 0 and <= 255 && g is >= 0 and <= 255 && b is >= 0 and <= 255)
            {
                _currentUnderlineColor = Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
            }
            return index + 4;
        }
        else if (colorType == 5 && index + 2 < parts.Length)
        {
            // 256-color underline — rare, skip for now
            return index + 2;
        }

        return index;
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
