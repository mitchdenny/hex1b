namespace Hex1b.Surfaces;

/// <summary>
/// A surface composed of multiple layered surface sources with deferred resolution.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="Surface"/> which immediately copies cells during compositing,
/// <see cref="CompositeSurface"/> records layers and resolves them lazily when
/// cells are accessed or when <see cref="Flatten"/> is called.
/// </para>
/// <para>
/// This deferred approach enables:
/// <list type="bullet">
///   <item>Computed cells that can query cells from layers below</item>
///   <item>Efficient nested composition without intermediate copies</item>
///   <item>Layer manipulation before final resolution</item>
/// </list>
/// </para>
/// <para>
/// Layers are z-ordered by add order: first added is at the bottom, last added is on top.
/// When resolving a cell, layers are composited bottom-up with transparency support.
/// </para>
/// </remarks>
public sealed class CompositeSurface : ISurfaceSource
{
    private readonly List<Layer> _layers = [];

    /// <summary>
    /// Gets the width of the composite surface in columns.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the composite surface in rows.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the cell metrics for this composite surface.
    /// </summary>
    /// <remarks>
    /// All layers with sixel content must have matching cell metrics.
    /// This is validated when layers are added.
    /// </remarks>
    public CellMetrics CellMetrics { get; }

    /// <summary>
    /// Gets whether any layer in this composite contains sixel graphics.
    /// </summary>
    public bool HasSixels
    {
        get
        {
            foreach (var layer in _layers)
            {
                if (layer.Source.HasSixels)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets the number of layers in this composite.
    /// </summary>
    public int LayerCount => _layers.Count;

    /// <summary>
    /// Creates a new composite surface with the specified dimensions and default cell metrics.
    /// </summary>
    /// <param name="width">The width in columns. Must be positive.</param>
    /// <param name="height">The height in rows. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if width or height is not positive.</exception>
    public CompositeSurface(int width, int height) : this(width, height, CellMetrics.Default)
    {
    }

    /// <summary>
    /// Creates a new composite surface with the specified dimensions and cell metrics.
    /// </summary>
    /// <param name="width">The width in columns. Must be positive.</param>
    /// <param name="height">The height in rows. Must be positive.</param>
    /// <param name="cellMetrics">The pixel dimensions of terminal cells.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if width or height is not positive.</exception>
    public CompositeSurface(int width, int height, CellMetrics cellMetrics)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        CellMetrics = cellMetrics;
    }

    /// <summary>
    /// Adds a layer to the composite surface.
    /// </summary>
    /// <remarks>
    /// Layers are z-ordered by add order. The first layer added is at the bottom (z=0),
    /// subsequent layers are stacked on top. The last layer added is rendered on top.
    /// </remarks>
    /// <param name="source">The surface source to add as a layer.</param>
    /// <param name="offsetX">The X offset where the source's (0,0) will be placed.</param>
    /// <param name="offsetY">The Y offset where the source's (0,0) will be placed.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the source contains sixel graphics but has different cell metrics.
    /// </exception>
    public void AddLayer(ISurfaceSource source, int offsetX = 0, int offsetY = 0)
    {
        ValidateLayerMetrics(source);
        _layers.Add(new Layer(source, offsetX, offsetY, null));
    }

    /// <summary>
    /// Adds a computed layer to the composite surface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A computed layer uses a delegate to compute cell values dynamically.
    /// The delegate receives a <see cref="ComputeContext"/> that provides access to
    /// cells from layers below and adjacent cells.
    /// </para>
    /// <para>
    /// Computed layers are useful for effects like drop shadows, overlays, or
    /// any effect that depends on the content of layers below.
    /// </para>
    /// </remarks>
    /// <param name="width">The width of the computed layer.</param>
    /// <param name="height">The height of the computed layer.</param>
    /// <param name="compute">The delegate that computes cell values.</param>
    /// <param name="offsetX">The X offset where the layer's (0,0) will be placed.</param>
    /// <param name="offsetY">The Y offset where the layer's (0,0) will be placed.</param>
    public void AddComputedLayer(int width, int height, CellCompute compute, int offsetX = 0, int offsetY = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(compute);

        var placeholder = new ComputedLayerSource(width, height);
        _layers.Add(new Layer(placeholder, offsetX, offsetY, compute));
    }

    /// <summary>
    /// Removes all layers from the composite surface.
    /// </summary>
    public void Clear()
    {
        _layers.Clear();
    }

    /// <summary>
    /// Gets whether this composite has any computed layers.
    /// </summary>
    private bool HasComputedLayers
    {
        get
        {
            foreach (var layer in _layers)
            {
                if (layer.Compute is not null)
                    return true;
            }
            return false;
        }
    }

    /// <inheritdoc />
    public SurfaceCell GetCell(int x, int y)
    {
        if (x < 0 || x >= Width)
            throw new ArgumentOutOfRangeException(nameof(x), x, $"X must be between 0 and {Width - 1}");
        if (y < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Y must be between 0 and {Height - 1}");

        // Fast path for non-computed composites
        if (!HasComputedLayers)
            return ResolveCellFast(x, y);

        var context = new LayerResolutionContext(this);
        return context.ResolveCell(x, y);
    }

    /// <inheritdoc />
    public bool TryGetCell(int x, int y, out SurfaceCell cell)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            // Fast path for non-computed composites
            if (!HasComputedLayers)
            {
                cell = ResolveCellFast(x, y);
                return true;
            }
            
            var context = new LayerResolutionContext(this);
            cell = context.ResolveCell(x, y);
            return true;
        }
        cell = default;
        return false;
    }

    /// <summary>
    /// Fast cell resolution without cycle detection overhead.
    /// Only valid when there are no computed layers.
    /// </summary>
    private SurfaceCell ResolveCellFast(int x, int y)
    {
        var result = SurfaceCells.Empty;

        foreach (var layer in _layers)
        {
            var srcX = x - layer.OffsetX;
            var srcY = y - layer.OffsetY;

            if (!layer.Source.IsInBounds(srcX, srcY))
                continue;

            var srcCell = layer.Source.GetCell(srcX, srcY);
            result = CompositeCellFast(result, srcCell);
        }

        return result;
    }

    /// <summary>
    /// Fast cell compositing without computed cell overhead.
    /// </summary>
    private static SurfaceCell CompositeCellFast(SurfaceCell below, SurfaceCell above)
    {
        // Handle transparency
        if (above.HasTransparentBackground)
            above = above with { Background = below.Background };

        if (above.HasTransparentForeground)
            above = above with { Foreground = below.Foreground };

        if (above.IsContinuation)
            return above;
        
        // Unwritten cells are fully transparent - show what's below
        if (above.Character == SurfaceCells.UnwrittenMarker)
            return below;
        
        if (above.Character != " " || above.Background is not null)
            return above;

        return above with
        {
            Character = below.Character,
            Foreground = below.Foreground,
            Attributes = below.Attributes | above.Attributes
        };
    }

    /// <inheritdoc />
    public bool IsInBounds(int x, int y)
        => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// Flattens all layers into a single <see cref="Surface"/>.
    /// </summary>
    /// <remarks>
    /// This resolves all layers bottom-up, handling transparency and
    /// computed cells. The result is a new Surface with all cells resolved
    /// to their final values.
    /// </remarks>
    /// <returns>A new Surface containing the flattened result.</returns>
    public Surface Flatten()
    {
        var result = new Surface(Width, Height, CellMetrics);
        var context = new LayerResolutionContext(this);

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                result[x, y] = context.ResolveCell(x, y);
            }
        }

        return result;
    }

    /// <summary>
    /// Computes sixel fragments for all sixels in this composite, accounting for occlusion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method scans all layers for sixel graphics and computes their visibility
    /// after considering occlusion from layers above. Sixels that are partially
    /// occluded will be split into multiple fragments.
    /// </para>
    /// <para>
    /// The fragments are returned in render order (bottom to top, row by row).
    /// </para>
    /// </remarks>
    /// <returns>List of sixel fragments to render.</returns>
    public IReadOnlyList<SixelFragment> GetSixelFragments()
    {
        if (!HasSixels)
            return [];

        // Step 1: Find all sixels and their positions
        var sixelVisibilities = new List<SixelVisibility>();

        for (var layerIndex = 0; layerIndex < _layers.Count; layerIndex++)
        {
            var layer = _layers[layerIndex];
            if (!layer.Source.HasSixels)
                continue;

            // Scan for sixel anchor cells in this layer
            ScanLayerForSixels(layer, layerIndex, sixelVisibilities);
        }

        if (sixelVisibilities.Count == 0)
            return [];

        // Step 2: Apply occlusions from higher layers
        for (var i = 0; i < sixelVisibilities.Count; i++)
        {
            var sixelVis = sixelVisibilities[i];
            ApplyOcclusionsToSixel(sixelVis);
        }

        // Step 3: Generate fragments for all visible sixels
        var fragments = new List<SixelFragment>();
        foreach (var sixelVis in sixelVisibilities)
        {
            if (!sixelVis.IsFullyOccluded)
            {
                fragments.AddRange(sixelVis.GenerateFragments(CellMetrics));
            }
        }

        // Sort by position for consistent render order
        fragments.Sort((a, b) =>
        {
            var yCompare = a.CellPosition.Y.CompareTo(b.CellPosition.Y);
            return yCompare != 0 ? yCompare : a.CellPosition.X.CompareTo(b.CellPosition.X);
        });

        return fragments;
    }

    /// <summary>
    /// Scans a layer for sixel anchor cells and adds them to the visibility list.
    /// </summary>
    private void ScanLayerForSixels(Layer layer, int layerIndex, List<SixelVisibility> sixelVisibilities)
    {
        var source = layer.Source;
        
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var cell = source.GetCell(x, y);
                if (cell.Sixel is null)
                    continue;

                // This is a sixel anchor cell
                var globalX = x + layer.OffsetX;
                var globalY = y + layer.OffsetY;

                // Check if within composite bounds
                if (globalX < 0 || globalY < 0 || globalX >= Width || globalY >= Height)
                    continue;

                sixelVisibilities.Add(new SixelVisibility(cell.Sixel, globalX, globalY, layerIndex));
            }
        }
    }

    /// <summary>
    /// Applies occlusions from higher layers to a sixel.
    /// </summary>
    private void ApplyOcclusionsToSixel(SixelVisibility sixelVis)
    {
        var sixelData = sixelVis.Sixel.Data;
        var sixelRect = new Layout.Rect(
            sixelVis.AnchorPosition.X,
            sixelVis.AnchorPosition.Y,
            sixelData.WidthInCells,
            sixelData.HeightInCells);

        // Check all layers above the sixel's layer
        for (var layerIndex = sixelVis.LayerIndex + 1; layerIndex < _layers.Count; layerIndex++)
        {
            var layer = _layers[layerIndex];

            // Calculate layer bounds in global coordinates
            var layerRect = new Layout.Rect(
                layer.OffsetX,
                layer.OffsetY,
                layer.Source.Width,
                layer.Source.Height);

            // Quick bounds check
            if (!RectsOverlap(sixelRect, layerRect))
                continue;

            // First, collect all sixel regions in this layer for bulk occlusion
            var sixelOcclusions = CollectSixelRegions(layer, layerRect, sixelRect);
            foreach (var occ in sixelOcclusions)
            {
                sixelVis.ApplyOcclusion(occ, CellMetrics);
            }

            // Then scan the overlapping region for opaque text/background cells
            var overlapLeft = Math.Max(sixelRect.X, layerRect.X);
            var overlapTop = Math.Max(sixelRect.Y, layerRect.Y);
            var overlapRight = Math.Min(sixelRect.Right, layerRect.Right);
            var overlapBottom = Math.Min(sixelRect.Bottom, layerRect.Bottom);

            for (var y = overlapTop; y < overlapBottom; y++)
            {
                for (var x = overlapLeft; x < overlapRight; x++)
                {
                    var srcX = x - layer.OffsetX;
                    var srcY = y - layer.OffsetY;
                    var cell = layer.Source.GetCell(srcX, srcY);

                    // Skip sixel anchor cells (already handled above)
                    if (cell.Sixel is not null)
                        continue;

                    // Check if this cell would occlude the sixel
                    if (IsOpaqueCell(cell))
                    {
                        // Apply single-cell occlusion
                        var occlusionRect = new Layout.Rect(x, y, 1, 1);
                        sixelVis.ApplyOcclusion(occlusionRect, CellMetrics);
                    }
                }
            }
        }

        // Also check for composite bounds clipping
        if (sixelRect.X < 0 || sixelRect.Y < 0 || 
            sixelRect.Right > Width || sixelRect.Bottom > Height)
        {
            // Clip to composite bounds
            var visibleLeft = Math.Max(0, sixelRect.X);
            var visibleTop = Math.Max(0, sixelRect.Y);
            var visibleRight = Math.Min(Width, sixelRect.Right);
            var visibleBottom = Math.Min(Height, sixelRect.Bottom);

            // Apply edge clipping as occlusions
            if (sixelRect.X < 0)
            {
                sixelVis.ApplyOcclusion(
                    new Layout.Rect(sixelRect.X, sixelRect.Y, -sixelRect.X, sixelRect.Height),
                    CellMetrics);
            }
            if (sixelRect.Y < 0)
            {
                sixelVis.ApplyOcclusion(
                    new Layout.Rect(sixelRect.X, sixelRect.Y, sixelRect.Width, -sixelRect.Y),
                    CellMetrics);
            }
            if (sixelRect.Right > Width)
            {
                sixelVis.ApplyOcclusion(
                    new Layout.Rect(Width, sixelRect.Y, sixelRect.Right - Width, sixelRect.Height),
                    CellMetrics);
            }
            if (sixelRect.Bottom > Height)
            {
                sixelVis.ApplyOcclusion(
                    new Layout.Rect(sixelRect.X, Height, sixelRect.Width, sixelRect.Bottom - Height),
                    CellMetrics);
            }
        }
    }

    /// <summary>
    /// Checks if a cell is opaque (would occlude a sixel behind it).
    /// </summary>
    private static bool IsOpaqueCell(SurfaceCell cell)
    {
        // A sixel always occludes content behind it
        if (cell.Sixel is not null)
            return true;
        
        // A cell is opaque if it has a non-transparent background
        // or if it has visible content (non-space character, not unwritten)
        if (cell.Background is not null)
            return true;
        if (cell.Character != " " && cell.Character != string.Empty && cell.Character != SurfaceCells.UnwrittenMarker)
            return true;
        return false;
    }

    /// <summary>
    /// Checks if two rectangles overlap.
    /// </summary>
    private static bool RectsOverlap(Layout.Rect a, Layout.Rect b)
    {
        return a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
    }

    /// <summary>
    /// Collects all sixel regions in a layer that overlap with the target rect.
    /// </summary>
    private static List<Layout.Rect> CollectSixelRegions(Layer layer, Layout.Rect layerRect, Layout.Rect targetRect)
    {
        var results = new List<Layout.Rect>();
        var source = layer.Source;
        
        if (!source.HasSixels)
            return results;

        // Scan for sixel anchors in the layer
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var cell = source.GetCell(x, y);
                if (cell.Sixel is null)
                    continue;

                // Calculate the sixel's global rect
                var globalX = x + layer.OffsetX;
                var globalY = y + layer.OffsetY;
                var sixelData = cell.Sixel.Data;
                var sixelRect = new Layout.Rect(globalX, globalY, sixelData.WidthInCells, sixelData.HeightInCells);

                // Check if this sixel overlaps with the target
                if (RectsOverlap(sixelRect, targetRect))
                {
                    // Calculate the overlapping region
                    var overlapLeft = Math.Max(sixelRect.X, targetRect.X);
                    var overlapTop = Math.Max(sixelRect.Y, targetRect.Y);
                    var overlapRight = Math.Min(sixelRect.Right, targetRect.Right);
                    var overlapBottom = Math.Min(sixelRect.Bottom, targetRect.Bottom);

                    if (overlapRight > overlapLeft && overlapBottom > overlapTop)
                    {
                        results.Add(new Layout.Rect(overlapLeft, overlapTop, 
                            overlapRight - overlapLeft, overlapBottom - overlapTop));
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Represents a layer in the composite surface.
    /// </summary>
    private readonly record struct Layer(
        ISurfaceSource Source,
        int OffsetX,
        int OffsetY,
        CellCompute? Compute);

    /// <summary>
    /// Placeholder source for computed layers - just provides bounds.
    /// </summary>
    private sealed class ComputedLayerSource : ISurfaceSource
    {
        public int Width { get; }
        public int Height { get; }
        public CellMetrics CellMetrics => CellMetrics.Default;
        public bool HasSixels => false; // Computed layers don't have sixels directly

        public ComputedLayerSource(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public SurfaceCell GetCell(int x, int y) => SurfaceCells.Empty;
        public bool TryGetCell(int x, int y, out SurfaceCell cell)
        {
            cell = SurfaceCells.Empty;
            return IsInBounds(x, y);
        }
        public bool IsInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
    }

    /// <summary>
    /// Validates that a layer's cell metrics match this composite's metrics if it has sixels.
    /// </summary>
    private void ValidateLayerMetrics(ISurfaceSource source)
    {
        if (source.HasSixels && source.CellMetrics != CellMetrics)
        {
            throw new InvalidOperationException(
                $"Cannot add layer with sixels when CellMetrics differ. " +
                $"Composite: {CellMetrics}, Layer: {source.CellMetrics}");
        }
    }

    /// <summary>
    /// Context for resolving cells with cycle detection.
    /// </summary>
    internal sealed class LayerResolutionContext
    {
        private readonly CompositeSurface _composite;
        private HashSet<(int X, int Y, int LayerIndex)>? _computing;

        public LayerResolutionContext(CompositeSurface composite)
        {
            _composite = composite;
        }

        public bool IsInBounds(int x, int y) => _composite.IsInBounds(x, y);
        
        public CellMetrics CellMetrics => _composite.CellMetrics;

        /// <summary>
        /// Resolves the cell at the specified position by compositing all layers.
        /// </summary>
        public SurfaceCell ResolveCell(int x, int y)
        {
            return ResolveCellUpToLayer(x, y, _composite._layers.Count);
        }

        /// <summary>
        /// Resolves the cell at the specified position, compositing layers up to (but not including) maxLayerIndex.
        /// </summary>
        public SurfaceCell ResolveCellUpToLayer(int x, int y, int maxLayerIndex)
        {
            var result = SurfaceCells.Empty;

            for (var i = 0; i < maxLayerIndex && i < _composite._layers.Count; i++)
            {
                var layer = _composite._layers[i];
                var srcX = x - layer.OffsetX;
                var srcY = y - layer.OffsetY;

                if (!layer.Source.IsInBounds(srcX, srcY))
                    continue;

                SurfaceCell srcCell;
                if (layer.Compute is not null)
                {
                    srcCell = ResolveComputedCell(x, y, i, layer.Compute);
                }
                else
                {
                    srcCell = layer.Source.GetCell(srcX, srcY);
                }

                result = CompositeCell(result, srcCell);
            }

            return result;
        }

        /// <summary>
        /// Resolves the cell at the specified position on a specific layer.
        /// </summary>
        public SurfaceCell ResolveCellAtLayer(int x, int y, int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= _composite._layers.Count)
                return SurfaceCells.Empty;

            var layer = _composite._layers[layerIndex];
            var srcX = x - layer.OffsetX;
            var srcY = y - layer.OffsetY;

            if (!layer.Source.IsInBounds(srcX, srcY))
                return SurfaceCells.Empty;

            if (layer.Compute is not null)
            {
                return ResolveComputedCell(x, y, layerIndex, layer.Compute);
            }

            return layer.Source.GetCell(srcX, srcY);
        }

        /// <summary>
        /// Finds a sixel at the specified cell position in layers below maxLayerIndex.
        /// </summary>
        /// <returns>Tuple of (SixelData, anchorX, anchorY) or null if no sixel found.</returns>
        public (SixelData Data, int AnchorX, int AnchorY)? FindSixelAtPosition(int x, int y, int maxLayerIndex)
        {
            // Search layers from top to bottom (most recent wins)
            for (var i = Math.Min(maxLayerIndex - 1, _composite._layers.Count - 1); i >= 0; i--)
            {
                var layer = _composite._layers[i];
                if (!layer.Source.HasSixels)
                    continue;

                // Check if this position could be within a sixel on this layer
                var result = FindSixelInLayer(layer, x, y);
                if (result is not null)
                    return result;
            }

            return null;
        }

        private static (SixelData Data, int AnchorX, int AnchorY)? FindSixelInLayer(Layer layer, int x, int y)
        {
            // We need to find a sixel anchor that covers position (x, y)
            // Unfortunately, we can't know where sixel anchors are without scanning
            // For now, scan the area that could contain an anchor for position (x, y)
            
            var source = layer.Source;
            
            // A sixel at anchor (ax, ay) covers cells (ax, ay) to (ax + width - 1, ay + height - 1)
            // So an anchor at position (x, y) must have:
            //   ax <= x and ax + widthInCells > x
            //   ay <= y and ay + heightInCells > y
            // Maximum sixel size we'll check: 100 cells in each direction
            const int maxSixelSpan = 100;
            
            var srcX = x - layer.OffsetX;
            var srcY = y - layer.OffsetY;
            
            // Check positions that could be anchors covering (srcX, srcY)
            var startCheckX = Math.Max(0, srcX - maxSixelSpan + 1);
            var startCheckY = Math.Max(0, srcY - maxSixelSpan + 1);
            var endCheckX = Math.Min(source.Width, srcX + 1);
            var endCheckY = Math.Min(source.Height, srcY + 1);

            for (var checkY = startCheckY; checkY < endCheckY; checkY++)
            {
                for (var checkX = startCheckX; checkX < endCheckX; checkX++)
                {
                    var cell = source.GetCell(checkX, checkY);
                    if (cell.Sixel is null)
                        continue;

                    var sixelData = cell.Sixel.Data;
                    var anchorGlobalX = checkX + layer.OffsetX;
                    var anchorGlobalY = checkY + layer.OffsetY;

                    // Check if this sixel covers position (x, y)
                    if (x >= anchorGlobalX && x < anchorGlobalX + sixelData.WidthInCells &&
                        y >= anchorGlobalY && y < anchorGlobalY + sixelData.HeightInCells)
                    {
                        return (sixelData, anchorGlobalX, anchorGlobalY);
                    }
                }
            }

            return null;
        }

        private SurfaceCell ResolveComputedCell(int x, int y, int layerIndex, CellCompute compute)
        {
            var key = (x, y, layerIndex);

            // Lazy allocate HashSet only when computing (most composites have no computed layers)
            _computing ??= [];
            
            // Cycle detection
            if (!_computing.Add(key))
            {
                // Already computing this cell - return fallback to break cycle
                return SurfaceCells.Empty;
            }

            try
            {
                var context = new ComputeContext(x, y, layerIndex, this);
                return compute(context);
            }
            finally
            {
                _computing.Remove(key);
            }
        }

        private static SurfaceCell CompositeCell(SurfaceCell below, SurfaceCell above)
        {
            // Handle transparency
            if (above.HasTransparentBackground)
            {
                above = above with { Background = below.Background };
            }

            if (above.HasTransparentForeground)
            {
                above = above with { Foreground = below.Foreground };
            }

            // If the cell is a continuation, preserve it but blend colors
            if (above.IsContinuation)
            {
                return above;
            }
            
            // Unwritten cells are fully transparent - show what's below
            if (above.Character == SurfaceCells.UnwrittenMarker)
            {
                return below;
            }
            
            if (above.Character != " " || above.Background is not null)
            {
                // Non-space or has explicit background - this layer takes over
                return above;
            }
            
            if (above.Foreground is not null || above.Attributes != CellAttributes.None)
            {
                // Has styling even though it's a space - apply it
                return above;
            }
            
            // Transparent space with no styling, keep what's below
            return below;
        }
    }
}
