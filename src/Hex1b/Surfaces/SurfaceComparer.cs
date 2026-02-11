using Hex1b.Theming;
using Hex1b.Tokens;

namespace Hex1b.Surfaces;

/// <summary>
/// Provides methods for comparing surfaces and generating output.
/// </summary>
public static class SurfaceComparer
{
    /// <summary>
    /// Compares two surfaces and returns the differences.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This performs a cell-by-cell comparison of the two surfaces.
    /// Cells are considered equal if all their visual properties match
    /// (character, foreground, background, attributes).
    /// </para>
    /// <para>
    /// The surfaces must have the same dimensions.
    /// </para>
    /// </remarks>
    /// <param name="previous">The previous surface state.</param>
    /// <param name="current">The current surface state.</param>
    /// <returns>A <see cref="SurfaceDiff"/> containing all changed cells.</returns>
    /// <exception cref="ArgumentException">Thrown if the surfaces have different dimensions.</exception>
    public static SurfaceDiff Compare(Surface previous, Surface current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        // Fast path: same instance means no changes
        if (ReferenceEquals(previous, current))
            return SurfaceDiff.Empty;

        if (previous.Width != current.Width || previous.Height != current.Height)
        {
            throw new ArgumentException(
                $"Surfaces must have the same dimensions. Previous: {previous.Width}x{previous.Height}, Current: {current.Width}x{current.Height}");
        }

        var width = current.Width;
        var height = current.Height;
        var prevCells = previous.CellsUnsafe;
        var currCells = current.CellsUnsafe;
        
        var changedCells = new List<ChangedCell>();

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                var index = rowOffset + x;
                var prevCell = prevCells[index];
                var currCell = currCells[index];

                if (!CellsEqual(prevCell, currCell))
                {
                    changedCells.Add(new ChangedCell(x, y, currCell));
                }
            }
        }

        return new SurfaceDiff(changedCells);
    }

    /// <summary>
    /// Compares a surface against an empty (default) state.
    /// </summary>
    /// <remarks>
    /// This is useful for initial rendering where there is no previous state.
    /// All non-empty cells in the surface will be included in the diff.
    /// </remarks>
    /// <param name="surface">The surface to compare against empty.</param>
    /// <returns>A <see cref="SurfaceDiff"/> containing all non-empty cells.</returns>
    public static SurfaceDiff CompareToEmpty(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var width = surface.Width;
        var height = surface.Height;
        var cells = surface.CellsUnsafe;
        var empty = SurfaceCells.Empty;
        
        var changedCells = new List<ChangedCell>();

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                var cell = cells[rowOffset + x];

                if (!CellsEqual(cell, empty))
                {
                    changedCells.Add(new ChangedCell(x, y, cell));
                }
            }
        }

        return new SurfaceDiff(changedCells);
    }

    /// <summary>
    /// Generates an optimized list of ANSI tokens to render the diff.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generated tokens are optimized for minimal output:
    /// <list type="bullet">
    ///   <item>Cells are processed in row-major order for optimal cursor movement</item>
    ///   <item>SGR state is tracked to avoid redundant attribute sequences</item>
    ///   <item>Cursor positioning is omitted when the cursor naturally advances</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="diff">The surface diff to render.</param>
    /// <returns>A list of ANSI tokens that will render the changes.</returns>
    public static IReadOnlyList<AnsiToken> ToTokens(SurfaceDiff diff)
        => ToTokens(diff, currentSurface: null);
    
    /// <summary>
    /// Generates a list of ANSI tokens to render the diff, with sixel awareness.
    /// </summary>
    /// <param name="diff">The surface diff to render.</param>
    /// <param name="currentSurface">The current surface, used to find sixels that need re-rendering when their region is dirty.</param>
    /// <returns>A list of ANSI tokens that will render the changes.</returns>
    public static IReadOnlyList<AnsiToken> ToTokens(SurfaceDiff diff, Surface? currentSurface)
    {
        ArgumentNullException.ThrowIfNull(diff);

        if (diff.IsEmpty)
            return Array.Empty<AnsiToken>();

        var tokens = new List<AnsiToken>();
        
        // Track regions covered by sixels (we need to skip cells under sixels)
        // Each entry is (x, y, width, height, cell) of a sixel region
        var sixelRegions = new List<(int X, int Y, int Width, int Height, SurfaceCell Cell)>();
        
        // If we have the current surface, find sixels whose regions intersect with dirty cells
        // These sixels need to be re-emitted even if their anchor cell hasn't changed
        // We collect them here but emit them all BEFORE any text cells
        if (currentSurface != null && currentSurface.HasSixels)
        {
            // Build a set of dirty cell positions for fast lookup
            var dirtyCells = new HashSet<(int, int)>();
            foreach (var change in diff.ChangedCells)
            {
                dirtyCells.Add((change.X, change.Y));
            }
            
            // Scan surface for sixels and check if their regions have dirty cells
            for (var y = 0; y < currentSurface.Height; y++)
            {
                for (var x = 0; x < currentSurface.Width; x++)
                {
                    var cell = currentSurface[x, y];
                    if (cell.HasSixel && cell.Sixel?.Data is not null)
                    {
                        var sixelData = cell.Sixel.Data;
                        var regionHasDirtyCell = false;
                        
                        // Check if any cell in this sixel's region is dirty
                        for (var sy = y; sy < y + sixelData.HeightInCells && sy < currentSurface.Height; sy++)
                        {
                            for (var sx = x; sx < x + sixelData.WidthInCells && sx < currentSurface.Width; sx++)
                            {
                                if (dirtyCells.Contains((sx, sy)))
                                {
                                    regionHasDirtyCell = true;
                                    break;
                                }
                            }
                            if (regionHasDirtyCell) break;
                        }
                        
                        if (regionHasDirtyCell)
                        {
                            // This sixel needs to be re-emitted
                            sixelRegions.Add((x, y, sixelData.WidthInCells, sixelData.HeightInCells, cell));
                        }
                    }
                }
            }
            
            // TEMPORARY: Skip fragmentation - just emit full sixels
            // This tests whether the alignment issue is in fragmentation logic
            // Use environment variable to toggle: HEX1B_SIXEL_FRAGMENTATION=1 to enable
            var useFragmentation = Environment.GetEnvironmentVariable("HEX1B_SIXEL_FRAGMENTATION") == "1";
            
            if (useFragmentation)
            {
                // For each sixel, compute occlusions from overlapping text and fragment accordingly
                var fragmentsToEmit = new List<(SixelFragment Fragment, CellMetrics Metrics)>();
                
                foreach (var (sx, sy, sw, sh, cell) in sixelRegions)
                {
                    // Create a SixelVisibility tracker for this sixel
                    var visibility = new SixelVisibility(cell.Sixel!, sx, sy, 0);
                    var metrics = currentSurface.CellMetrics;
                    
                    // Scan for overlapping text cells and apply as occlusions
                    for (var y = sy; y < sy + sh && y < currentSurface.Height; y++)
                    {
                        for (var x = sx; x < sx + sw && x < currentSurface.Width; x++)
                        {
                            var checkCell = currentSurface[x, y];
                            // If there's a non-space, non-unwritten cell in the sixel region, it occludes
                            if (checkCell.Character != " " && checkCell.Character != SurfaceCells.UnwrittenMarker)
                            {
                                // This single cell is an occlusion
                                visibility.ApplyOcclusion(new Layout.Rect(x, y, 1, 1), metrics);
                            }
                        }
                    }
                    
                    // Generate fragments for the visible portions
                    var fragments = visibility.GenerateFragments(metrics);
                    
                    if (visibility.IsFullyOccluded)
                    {
                        System.IO.File.AppendAllText("/tmp/sixel-tokens.log", 
                            $"[{DateTime.Now:HH:mm:ss.fff}] SIXEL at ({sx},{sy}) FULLY OCCLUDED - skipped\n");
                        continue;
                    }
                    
                    System.IO.File.AppendAllText("/tmp/sixel-tokens.log", 
                        $"[{DateTime.Now:HH:mm:ss.fff}] SIXEL at ({sx},{sy}) -> {fragments.Count} fragments, fullyVisible={visibility.IsFullyVisible}\n");
                    
                    foreach (var fragment in fragments)
                    {
                        fragmentsToEmit.Add((fragment, metrics));
                    }
                }
                
                // Emit ALL sixel fragments first (before any text cells)
                foreach (var (fragment, metrics) in fragmentsToEmit)
                {
                    var payload = fragment.GetPayload();
                    if (payload is null)
                    {
                        System.IO.File.AppendAllText("/tmp/sixel-tokens.log", 
                            $"[{DateTime.Now:HH:mm:ss.fff}] Fragment at ({fragment.CellPosition.X},{fragment.CellPosition.Y}) - FAILED to encode\n");
                        continue;
                    }
                    
                    var (fx, fy) = fragment.CellPosition;
                    System.IO.File.AppendAllText("/tmp/sixel-tokens.log", 
                        $"[{DateTime.Now:HH:mm:ss.fff}] Fragment at ({fx},{fy}) region={fragment.PixelRegion}, isComplete={fragment.IsComplete}\n");
                    
                    // Position cursor at fragment position
                    tokens.Add(new CursorPositionToken(fy + 1, fx + 1));
                    // Emit the fragment
                    tokens.Add(new UnrecognizedSequenceToken(payload));
                }
            }
            else
            {
                // No fragmentation - emit full sixels, rely on text to overwrite
                foreach (var (sx, sy, sw, sh, cell) in sixelRegions)
                {
                    System.IO.File.AppendAllText("/tmp/sixel-tokens.log", 
                        $"[{DateTime.Now:HH:mm:ss.fff}] SIXEL at ({sx},{sy}) span {sw}x{sh} - NO FRAGMENTATION\n");
                    
                    // Position cursor at sixel anchor
                    tokens.Add(new CursorPositionToken(sy + 1, sx + 1));
                    // Emit the full sixel
                    tokens.Add(new UnrecognizedSequenceToken(cell.Sixel!.Data.Payload));
                }
            }
        }

        // Track current state to minimize redundant tokens
        int cursorX = -1;
        int cursorY = -1;
        Hex1bColor? currentFg = null;
        Hex1bColor? currentBg = null;
        CellAttributes currentAttrs = CellAttributes.None;
        bool stateUnknown = true;
        TrackedObject<HyperlinkData>? currentHyperlink = null;

        foreach (var change in diff.ChangedCells)
        {
            // Skip continuation cells - they're handled by the wide character before them
            if (change.Cell.IsContinuation)
                continue;
            
            // Skip cells that are covered by a sixel (either from diff or pre-emitted)
            // Only skip if the cell has no content to render over the sixel
            if (IsCoveredBySixelRegion(change.X, change.Y, change.Cell, sixelRegions))
                continue;

            // Position cursor if needed
            if (change.Y != cursorY || change.X != cursorX)
            {
                // ANSI cursor position is 1-based
                tokens.Add(new CursorPositionToken(change.Y + 1, change.X + 1));
                cursorY = change.Y;
                cursorX = change.X;
            }
            
            // Emit OSC 8 if hyperlink state changed
            if (!HyperlinksEqual(change.Cell.Hyperlink, currentHyperlink))
            {
                var hyperlink = change.Cell.Hyperlink;
                if (hyperlink != null)
                {
                    // Start hyperlink: OSC 8 ; params ; URI ST
                    // Use ESC \ terminator for maximum compatibility (Kitty requires it)
                    tokens.Add(new OscToken("8", hyperlink.Data.Parameters, hyperlink.Data.Uri, UseEscBackslash: true));
                }
                else
                {
                    // End hyperlink: OSC 8 ; ; ST (empty URI)
                    tokens.Add(new OscToken("8", "", "", UseEscBackslash: true));
                }
                currentHyperlink = hyperlink;
            }

            // Generate SGR if attributes changed or state is unknown
            var needsSgr = stateUnknown ||
                           !ColorsEqual(change.Cell.Foreground, currentFg) ||
                           !ColorsEqual(change.Cell.Background, currentBg) ||
                           change.Cell.Attributes != currentAttrs;

            if (needsSgr)
            {
                var sgrParams = BuildSgrParameters(
                    change.Cell,
                    stateUnknown,
                    ref currentFg,
                    ref currentBg,
                    ref currentAttrs);
                
                if (!string.IsNullOrEmpty(sgrParams))
                {
                    tokens.Add(new SgrToken(sgrParams));
                }
                stateUnknown = false;
            }

            // Check if this cell has sixel data
            if (change.Cell.HasSixel && change.Cell.Sixel?.Data is not null)
            {
                // Check if this sixel was already emitted in the pre-loop
                bool alreadyEmitted = false;
                foreach (var (sx, sy, _, _, _) in sixelRegions)
                {
                    if (sx == change.X && sy == change.Y)
                    {
                        alreadyEmitted = true;
                        break;
                    }
                }
                
                if (!alreadyEmitted)
                {
                    // Track the region this sixel covers so we skip cells underneath
                    var sixelData = change.Cell.Sixel.Data;
                    sixelRegions.Add((change.X, change.Y, sixelData.WidthInCells, sixelData.HeightInCells, change.Cell));
                    
                    // Emit the sixel DCS sequence as raw - the payload already contains ESC P ... ESC \
                    tokens.Add(new UnrecognizedSequenceToken(change.Cell.Sixel.Data.Payload));
                    
                    // Sixel rendering moves cursor, mark position as unknown
                    cursorX = -1;
                    cursorY = -1;
                }
                continue;
            }

            // Output the character (convert unwritten marker to space)
            var charToOutput = change.Cell.Character == SurfaceCells.UnwrittenMarker 
                ? " " 
                : change.Cell.Character;
            
            // Log text cells that are inside a sixel region
            foreach (var (sx, sy, sw, sh, _) in sixelRegions)
            {
                if (change.X >= sx && change.X < sx + sw && change.Y >= sy && change.Y < sy + sh)
                {
                    System.IO.File.AppendAllText("/tmp/sixel-tokens.log", 
                        $"[{DateTime.Now:HH:mm:ss.fff}] TEXT '{charToOutput}' at ({change.X},{change.Y}) OVER sixel at ({sx},{sy})\n");
                    break;
                }
            }
            
            tokens.Add(new TextToken(charToOutput));
            
            // Cursor advances by display width
            cursorX += Math.Max(1, change.Cell.DisplayWidth);
        }
        
        // If we ended in a hyperlink, close it
        if (currentHyperlink != null)
        {
            tokens.Add(new OscToken("8", "", "", UseEscBackslash: true));
        }

        return tokens;
    }
    
    /// <summary>
    /// Checks if a cell position is covered by any sixel region.
    /// </summary>
    /// <remarks>
    /// Only returns true if the cell is within a sixel region AND the cell itself
    /// doesn't have content that should override the sixel.
    /// </remarks>
    private static bool IsCoveredBySixelRegion(int x, int y, SurfaceCell cell, List<(int X, int Y, int Width, int Height, SurfaceCell Cell)> regions)
    {
        // If the cell has actual content, it should be rendered over the sixel
        if (cell.Character != " " && cell.Character != string.Empty && cell.Character != SurfaceCells.UnwrittenMarker)
            return false;
        
        foreach (var (sx, sy, sw, sh, _) in regions)
        {
            // Skip the anchor cell itself (it has the sixel, not covered by it)
            if (x == sx && y == sy)
                continue;
                
            if (x >= sx && x < sx + sw && y >= sy && y < sy + sh)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Generates an ANSI string to render the diff.
    /// </summary>
    /// <remarks>
    /// This is a convenience method that generates tokens and serializes them
    /// to a single ANSI string. For integration with existing token pipelines,
    /// use <see cref="ToTokens(SurfaceDiff)"/> instead.
    /// </remarks>
    /// <param name="diff">The surface diff to render.</param>
    /// <returns>An ANSI escape sequence string that will render the changes.</returns>
    public static string ToAnsiString(SurfaceDiff diff)
        => ToAnsiString(diff, currentSurface: null);
    
    /// <summary>
    /// Generates an ANSI string to render the diff, with sixel awareness.
    /// </summary>
    /// <param name="diff">The surface diff to render.</param>
    /// <param name="currentSurface">The current surface, used to find sixels that need re-rendering.</param>
    /// <returns>An ANSI escape sequence string that will render the changes.</returns>
    public static string ToAnsiString(SurfaceDiff diff, Surface? currentSurface)
    {
        var tokens = ToTokens(diff, currentSurface);
        
        if (tokens.Count == 0)
            return string.Empty;

        return AnsiTokenSerializer.Serialize(tokens);
    }

    /// <summary>
    /// Creates a full-surface diff that treats the entire surface as changed.
    /// </summary>
    /// <remarks>
    /// This is useful for forced full refreshes (e.g., after resize).
    /// </remarks>
    /// <param name="surface">The surface to create a full diff for.</param>
    /// <returns>A <see cref="SurfaceDiff"/> containing all cells.</returns>
    public static SurfaceDiff CreateFullDiff(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var changedCells = new List<ChangedCell>(surface.CellCount);

        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                changedCells.Add(new ChangedCell(x, y, surface[x, y]));
            }
        }

        return new SurfaceDiff(changedCells);
    }

    #region Private Helpers

    private static bool CellsEqual(SurfaceCell a, SurfaceCell b)
    {
        // Compare visual properties and hyperlink
        if (a.Character != b.Character ||
            !ColorsEqual(a.Foreground, b.Foreground) ||
            !ColorsEqual(a.Background, b.Background) ||
            a.Attributes != b.Attributes ||
            a.DisplayWidth != b.DisplayWidth)
        {
            return false;
        }
        
        // Compare sixels
        if (!SixelsEqual(a.Sixel, b.Sixel))
            return false;
        
        // Compare hyperlinks by content (URI and parameters)
        return HyperlinksEqual(a.Hyperlink, b.Hyperlink);
    }
    
    private static bool SixelsEqual(TrackedObject<SixelData>? a, TrackedObject<SixelData>? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        
        // Compare by content hash
        return SixelData.HashEquals(a.Data.ContentHash, b.Data.ContentHash);
    }
    
    private static bool HyperlinksEqual(TrackedObject<HyperlinkData>? a, TrackedObject<HyperlinkData>? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        
        // Compare by content, not reference
        return a.Data.Uri == b.Data.Uri && a.Data.Parameters == b.Data.Parameters;
    }

    private static bool ColorsEqual(Hex1bColor? a, Hex1bColor? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Value.R == b.Value.R && 
               a.Value.G == b.Value.G && 
               a.Value.B == b.Value.B;
    }

    private static string BuildSgrParameters(
        SurfaceCell targetCell,
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
        if (!ColorsEqual(targetCell.Foreground, currentFg) && targetCell.Foreground is not null)
        {
            parts.Add(BuildColorSgr(targetCell.Foreground.Value, isForeground: true));
        }

        // Add background color if different
        if (!ColorsEqual(targetCell.Background, currentBg) && targetCell.Background is not null)
        {
            parts.Add(BuildColorSgr(targetCell.Background.Value, isForeground: false));
        }

        // Update tracked state
        currentAttrs = targetCell.Attributes;
        currentFg = targetCell.Foreground;
        currentBg = targetCell.Background;

        return string.Join(";", parts);
    }

    private static string BuildColorSgr(Hex1bColor color, bool isForeground)
    {
        // Use 24-bit color (SGR 38;2;r;g;b for foreground, 48;2;r;g;b for background)
        var prefix = isForeground ? "38;2" : "48;2";
        return $"{prefix};{color.R};{color.G};{color.B}";
    }

    #endregion

    #region Sixel Support

    /// <summary>
    /// Generates tokens for sixel fragments from a composite surface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sixel fragments are rendered in order (bottom-to-top for overlapping sixels).
    /// Each fragment is positioned using cursor movement, then the sixel DCS sequence is emitted.
    /// </para>
    /// <para>
    /// For fragments that have been cropped due to occlusion, the sixel data is re-encoded
    /// from the original pixel data.
    /// </para>
    /// </remarks>
    /// <param name="composite">The composite surface containing sixels.</param>
    /// <returns>A list of ANSI tokens that will render the sixel fragments.</returns>
    public static IReadOnlyList<AnsiToken> SixelFragmentsToTokens(CompositeSurface composite)
    {
        ArgumentNullException.ThrowIfNull(composite);

        var fragments = composite.GetSixelFragments();
        if (fragments.Count == 0)
            return Array.Empty<AnsiToken>();

        var tokens = new List<AnsiToken>();

        foreach (var fragment in fragments)
        {
            // Position cursor at the fragment's cell position (ANSI is 1-based)
            tokens.Add(new CursorPositionToken(fragment.CellPosition.Y + 1, fragment.CellPosition.X + 1));

            // Get the sixel payload (may be re-encoded for cropped fragments)
            // The payload already contains ESC P ... ESC \, so emit as raw sequence
            var payload = fragment.GetPayload();
            if (payload is not null)
            {
                tokens.Add(new UnrecognizedSequenceToken(payload));
            }
        }

        return tokens;
    }

    /// <summary>
    /// Generates tokens for a composite surface including both cells and sixels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method handles the complete rendering of a composite surface:
    /// <list type="bullet">
    ///   <item>Flattens the surface to resolve all layers and computed cells</item>
    ///   <item>Generates text/attribute tokens for non-sixel cells</item>
    ///   <item>Generates sixel tokens for visible sixel fragments</item>
    /// </list>
    /// </para>
    /// <para>
    /// Sixels are rendered after text content to ensure proper layering.
    /// </para>
    /// </remarks>
    /// <param name="composite">The composite surface to render.</param>
    /// <param name="previous">Optional previous surface state for diff-based rendering.</param>
    /// <returns>A list of ANSI tokens that will render the surface.</returns>
    public static IReadOnlyList<AnsiToken> CompositeToTokens(
        CompositeSurface composite,
        Surface? previous = null)
    {
        ArgumentNullException.ThrowIfNull(composite);

        var tokens = new List<AnsiToken>();

        // First, flatten and generate cell tokens (diff against previous if provided)
        var flattened = composite.Flatten();
        var cellDiff = previous is null 
            ? CompareToEmpty(flattened) 
            : Compare(previous, flattened);

        if (!cellDiff.IsEmpty)
        {
            var cellTokens = ToTokens(cellDiff);
            tokens.AddRange(cellTokens);
        }

        // Then, add sixel tokens
        var sixelTokens = SixelFragmentsToTokens(composite);
        tokens.AddRange(sixelTokens);

        return tokens;
    }

    /// <summary>
    /// Generates an ANSI string for a composite surface including cells and sixels.
    /// </summary>
    /// <param name="composite">The composite surface to render.</param>
    /// <param name="previous">Optional previous surface state for diff-based rendering.</param>
    /// <returns>An ANSI escape sequence string that will render the surface.</returns>
    public static string CompositeToAnsiString(
        CompositeSurface composite,
        Surface? previous = null)
    {
        var tokens = CompositeToTokens(composite, previous);
        
        if (tokens.Count == 0)
            return string.Empty;

        return AnsiTokenSerializer.Serialize(tokens);
    }

    #endregion
}
