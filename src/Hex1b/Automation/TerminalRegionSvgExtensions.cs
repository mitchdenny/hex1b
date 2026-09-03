using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Web;

namespace Hex1b.Automation;

/// <summary>
/// Extension methods for rendering terminal regions to SVG format.
/// </summary>
public static class TerminalRegionSvgExtensions
{
    /// <summary>
    /// Default options for SVG rendering.
    /// </summary>
    public static readonly TerminalSvgOptions DefaultOptions = new();

    /// <summary>
    /// Renders the terminal region to an SVG string.
    /// </summary>
    /// <param name="region">The terminal region to render.</param>
    /// <param name="options">Optional rendering options.</param>
    /// <returns>An SVG string representation of the terminal region.</returns>
    public static string ToSvg(this IHex1bTerminalRegion region, TerminalSvgOptions? options = null)
    {
        options ??= DefaultOptions;
        return RenderToSvg(region, options, cursorX: null, cursorY: null);
    }

    /// <summary>
    /// Renders the terminal snapshot to an SVG string, including cursor position.
    /// </summary>
    /// <param name="snapshot">The terminal snapshot to render.</param>
    /// <param name="options">Optional rendering options. If null, uses default options with snapshot's cell dimensions.</param>
    /// <returns>An SVG string representation of the terminal snapshot.</returns>
    public static string ToSvg(this Hex1bTerminalSnapshot snapshot, TerminalSvgOptions? options = null)
    {
        // Use snapshot's cell dimensions as defaults if no options provided
        options ??= new TerminalSvgOptions
        {
            CellWidth = snapshot.CellPixelWidth,
            CellHeight = snapshot.CellPixelHeight
        };
        return RenderToSvg(snapshot, options, snapshot.CursorX, snapshot.CursorY, snapshot.ScrollbackLineCount);
    }

    private static string RenderToSvg(IHex1bTerminalRegion region, TerminalSvgOptions options, int? cursorX, int? cursorY, int scrollbackLineCount = 0)
    {
        var cellWidth = options.CellWidth;
        var cellHeight = options.CellHeight;
        var width = region.Width * cellWidth;
        var height = region.Height * cellHeight;

        var sb = new StringBuilder();

        // SVG header
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");

        // Pre-scan cells to identify unique cell groups (e.g., hyperlinks with same ID/URI)
        // This allows related cells to be highlighted together
        var hyperlinkGroups = new Dictionary<object, string>(); // TrackedObject reference -> group class name
        var groupId = 0;
        
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                var cell = region.GetCell(x, y);
                if (cell.TrackedHyperlink is { } trackedLink && !hyperlinkGroups.ContainsKey(trackedLink))
                {
                    // Generate a stable class name based on the hyperlink content
                    // Use both URI and parameters to create the group (cells with same id= should group together)
                    var linkData = trackedLink.Data;
                    var groupKey = $"link-{groupId++}";
                    hyperlinkGroups[trackedLink] = groupKey;
                }
            }
        }

        // Style definitions including blink animation and cell group highlighting
        sb.AppendLine("  <defs>");
        sb.AppendLine("    <style>");
        sb.AppendLine($"      .terminal-text {{ font-family: {options.FontFamily}; font-size: {options.FontSize}px; }}");
        sb.AppendLine($"      .cursor {{ fill: {options.CursorColor}; opacity: 0.7; }}");
        sb.AppendLine("      @keyframes blink { 0%, 49% { opacity: 1; } 50%, 100% { opacity: 0.3; } }");
        sb.AppendLine("      .blink { animation: blink 1s infinite; }");
        sb.AppendLine($"      .cell-grid {{ stroke: {options.CellGridColor}; stroke-width: 0.5; vector-effect: non-scaling-stroke; }}");
        sb.AppendLine($"      .pixel-grid {{ stroke: {options.PixelGridColor}; stroke-width: 0.25; vector-effect: non-scaling-stroke; }}");
        sb.AppendLine("      /* Cell group highlighting for related cells (hyperlinks, etc.) */");
        sb.AppendLine("      .cell { pointer-events: bounding-box; }");
        sb.AppendLine("      .cell.highlight > rect.cell-bg { stroke: #ff6b6b; stroke-width: 2; }");
        sb.AppendLine("      .cell.highlight > text { fill: #ff6b6b !important; }");
        sb.AppendLine("    </style>");

        // Pre-generate clip paths for wide character truncation
        // Only create clips for wide characters that have FEWER owned cells than expected
        // (i.e., their continuation cell was overwritten by something else)
        var clipId = 0;
        var clipPaths = new Dictionary<(int x, int y, int widthCells), string>();
        
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                var cell = region.GetCell(x, y);
                var ch = cell.Character;
                
                // Skip empty/continuation/unwritten cells
                if (string.IsNullOrEmpty(ch) || ch == "\0" || ch == "\uE000")
                    continue;
                
                // Calculate expected display width
                var expectedWidth = DisplayWidth.GetGraphemeWidth(ch);
                
                // Only need clips for wide characters
                if (expectedWidth <= 1)
                    continue;
                
                // Count how many continuation cells have matching sequence
                var ownedCells = 1;
                for (int i = 1; i < expectedWidth && (x + i) < region.Width; i++)
                {
                    var contCell = region.GetCell(x + i, y);
                    // Continuation cell should be empty string with same sequence
                    if (contCell.Character == "" && contCell.Sequence == cell.Sequence)
                        ownedCells++;
                    else
                        break;
                }
                
                // Only generate clip path if this character is TRUNCATED (fewer owned cells than expected)
                if (ownedCells < expectedWidth)
                {
                    var clipKey = (x, y, ownedCells);
                    if (!clipPaths.ContainsKey(clipKey))
                    {
                        var id = $"clip-{clipId++}";
                        clipPaths[clipKey] = id;
                        var clipX = x * cellWidth;
                        var clipY = y * cellHeight;
                        var clipW = ownedCells * cellWidth;
                        sb.AppendLine($"""    <clipPath id="{id}"><rect x="{clipX}" y="{clipY}" width="{clipW}" height="{cellHeight}"/></clipPath>""");
                    }
                }
            }
        }
        
        sb.AppendLine("  </defs>");

        // Background rectangle
        sb.AppendLine($"""  <rect width="{width}" height="{height}" fill="{options.DefaultBackground}"/>""");

        // Collect all cells with their positions for sequence-ordered rendering
        var cells = new List<(int X, int Y, TerminalCell Cell)>();
        for (int y = 0; y < region.Height; y++)
        {
            for (int x = 0; x < region.Width; x++)
            {
                cells.Add((x, y, region.GetCell(x, y)));
            }
        }

        // Sort by sequence number (ascending) so older writes render first, newer writes render on top
        cells.Sort((a, b) => a.Cell.Sequence.CompareTo(b.Cell.Sequence));

        // PASS 1: Backgrounds (bottom layer)
        // Render all cell backgrounds first so they are behind images and text
        sb.AppendLine("  <g class=\"terminal-bg\">");

        foreach (var (x, y, cell) in cells)
        {
            var attrs = cell.Attributes;
            var ch = cell.Character;
            var isReverse = (attrs & CellAttributes.Reverse) != 0;

            // Skip background rendering for continuation cells (empty string)
            // These cells are visually "owned" by the wide character in the previous cell
            var isContinuationCell = ch == "";

            if (!isContinuationCell)
            {
                // Render background for this cell - always opaque for proper clipping behavior
                var rectX = x * cellWidth;
                var rectY = y * cellHeight;

                string bgColor;
                if (isReverse)
                {
                    // Reverse: use foreground as background (or default foreground)
                    bgColor = cell.Foreground.HasValue 
                        ? $"rgb({cell.Foreground.Value.R},{cell.Foreground.Value.G},{cell.Foreground.Value.B})"
                        : options.DefaultForeground;
                }
                else if (cell.Background.HasValue)
                {
                    var bg = cell.Background.Value;
                    bgColor = $"rgb({bg.R},{bg.G},{bg.B})";
                }
                else
                {
                    // Use default background for opaque cells
                    bgColor = options.DefaultBackground;
                }

                // For wide characters, render a background that spans all owned cells
                var bgCharWidth = string.IsNullOrEmpty(ch) || ch == "\0" || ch == "\uE000" ? 1 : DisplayWidth.GetGraphemeWidth(ch);
                var bgWidth = cellWidth;

                if (bgCharWidth > 1)
                {
                    // Count how many continuation cells this character owns
                    var ownedCells = 1;
                    for (int i = 1; i < bgCharWidth && (x + i) < region.Width; i++)
                    {
                        var contCell = region.GetCell(x + i, y);
                        if (contCell.Character == "" && contCell.Sequence == cell.Sequence)
                            ownedCells++;
                        else
                            break;
                    }
                    bgWidth = ownedCells * cellWidth;
                }

                sb.AppendLine($"""    <rect class="cell-bg" x="{rectX}" y="{rectY}" width="{bgWidth}" height="{cellHeight}" fill="{bgColor}"/>""");

                // Blink indicator: subtle border/glow around blinking cells
                if ((attrs & CellAttributes.Blink) != 0)
                {
                    sb.AppendLine($"""    <rect x="{rectX}" y="{rectY}" width="{bgWidth}" height="{cellHeight}" fill="none" stroke="#ffcc00" stroke-width="1" stroke-dasharray="2,2" class="blink"/>""");
                }
            }
        }

        sb.AppendLine("  </g>");

        // PASS 2: Images (middle layer - between backgrounds and text)
        // Render KGP images and Sixel graphics
        sb.AppendLine("  <g class=\"terminal-images\">");

        // KGP images - sort by ZIndex so lower z-values render first (further back)
        if (region is Hex1bTerminalSnapshot snapshot2)
        {
            var sortedPlacements = snapshot2.KgpPlacements
                .OrderBy(placement => placement.ZIndex)
                .ThenBy(placement => placement.ImageId)
                .ThenBy(placement => placement.GraphId)
                .ThenBy(placement => placement.Row)
                .ThenBy(placement => placement.Column)
                .ToList();
            var placeholderImageDefinitions =
                new Dictionary<uint, (string ElementId, string DataUri)>();
            foreach (var placement in sortedPlacements)
            {
                if (placeholderImageDefinitions.ContainsKey(placement.ImageId) ||
                    !snapshot2.KgpImages.TryGetValue(placement.ImageId, out var image) ||
                    (placement.RenderGeometry is null &&
                     image.CurrentFrameFormat != KgpFormat.Png))
                {
                    continue;
                }

                var dataUri = EncodeKgpImageToDataUri(
                    image.CurrentFrameData,
                    image.Width,
                    image.Height,
                    image.CurrentFrameFormat);
                if (dataUri is not null)
                {
                    placeholderImageDefinitions.Add(
                        placement.ImageId,
                        (
                            $"kgp-placeholder-image-{placeholderImageDefinitions.Count}",
                            dataUri));
                }
            }

            if (placeholderImageDefinitions.Count > 0)
            {
                sb.AppendLine("    <defs>");
                foreach (var definition in placeholderImageDefinitions.Values)
                {
                    sb.AppendLine($"""      <symbol id="{definition.ElementId}" viewBox="0 0 1 1" preserveAspectRatio="none"><image x="0" y="0" width="1" height="1" href="{definition.DataUri}" preserveAspectRatio="none"/></symbol>""");
                }
                sb.AppendLine("    </defs>");
            }

            var placeholderClipIndex = 0;
            foreach (var placement in sortedPlacements)
            {
                if (snapshot2.KgpImages.TryGetValue(placement.ImageId, out var imageData))
                {
                    if (placement.RenderGeometry is { } geometry)
                    {
                        var imageX = (placement.Column +
                            geometry.ImageOffsetXInCells) * cellWidth;
                        var imageY = (placement.Row +
                            geometry.ImageOffsetYInCells) * cellHeight;
                        var imageWidth = geometry.ImageWidthInCells * cellWidth;
                        var imageHeight = geometry.ImageHeightInCells * cellHeight;
                        var clipX = (placement.Column +
                            geometry.ClipOffsetXInCells) * cellWidth;
                        var clipY = (placement.Row +
                            geometry.ClipOffsetYInCells) * cellHeight;
                        var clipWidth = geometry.ClipWidthInCells * cellWidth;
                        var clipHeight = geometry.ClipHeightInCells * cellHeight;
                        if (!placeholderImageDefinitions.TryGetValue(
                                placement.ImageId,
                                out var definition))
                        {
                            continue;
                        }

                        var placeholderClipId =
                            $"kgp-placeholder-clip-{placeholderClipIndex++}";
                        sb.AppendLine($"""    <clipPath id="{placeholderClipId}"><rect x="{FormatSvgNumber(clipX)}" y="{FormatSvgNumber(clipY)}" width="{FormatSvgNumber(clipWidth)}" height="{FormatSvgNumber(clipHeight)}"/></clipPath>""");
                        sb.AppendLine($"""    <g clip-path="url(#{placeholderClipId})"><use href="#{definition.ElementId}" x="{FormatSvgNumber(imageX)}" y="{FormatSvgNumber(imageY)}" width="{FormatSvgNumber(imageWidth)}" height="{FormatSvgNumber(imageHeight)}" data-image-id="{placement.ImageId}" style="image-rendering: pixelated;"/></g>""");
                    }
                    else if (imageData.CurrentFrameFormat == KgpFormat.Png)
                    {
                        var destinationX = placement.Column * cellWidth;
                        var destinationY = placement.Row * cellHeight;
                        var destinationWidth =
                            (double)placement.DisplayColumns * cellWidth;
                        var destinationHeight =
                            (double)placement.DisplayRows * cellHeight;
                        if (!placeholderImageDefinitions.TryGetValue(
                                placement.ImageId,
                                out var definition) ||
                            !TryGetPngRenderBounds(
                                placement,
                                imageData,
                                destinationX,
                                destinationY,
                                destinationWidth,
                                destinationHeight,
                                out var imageX,
                                out var imageY,
                                out var imageWidth,
                                out var imageHeight,
                                out var requiresClip))
                        {
                            continue;
                        }

                        var imageUse = $"""<use href="#{definition.ElementId}" x="{FormatSvgNumber(imageX)}" y="{FormatSvgNumber(imageY)}" width="{FormatSvgNumber(imageWidth)}" height="{FormatSvgNumber(imageHeight)}" data-image-id="{placement.ImageId}" style="image-rendering: pixelated;"/>""";
                        if (requiresClip)
                        {
                            var pngClipId =
                                $"kgp-png-clip-{placeholderClipIndex++}";
                            sb.AppendLine($"""    <clipPath id="{pngClipId}"><rect x="{FormatSvgNumber(destinationX)}" y="{FormatSvgNumber(destinationY)}" width="{FormatSvgNumber(destinationWidth)}" height="{FormatSvgNumber(destinationHeight)}"/></clipPath>""");
                            sb.AppendLine($"""    <g clip-path="url(#{pngClipId})">{imageUse}</g>""");
                        }
                        else
                        {
                            sb.AppendLine($"    {imageUse}");
                        }
                    }
                    else
                    {
                        var imgX = placement.Column * cellWidth;
                        var imgY = placement.Row * cellHeight;
                        var imgWidth = (int)placement.DisplayColumns * cellWidth;
                        var imgHeight = (int)placement.DisplayRows * cellHeight;
                        var dataUri = EncodeKgpImageToDataUri(
                            imageData.CurrentFrameData,
                            imageData.Width,
                            imageData.Height,
                            imageData.CurrentFrameFormat,
                            placement.SourceX,
                            placement.SourceY,
                            placement.SourceWidth,
                            placement.SourceHeight);
                        if (dataUri is not null)
                        {
                            sb.AppendLine($"""    <image x="{imgX}" y="{imgY}" width="{imgWidth}" height="{imgHeight}" href="{dataUri}" preserveAspectRatio="none" data-image-id="{placement.ImageId}" style="image-rendering: pixelated;"/>""");
                        }
                    }
                }
            }
        }

        // Sixel graphics: iterate placements directly (not cell attributes) so
        // rendering reflects the independent placement/image lifetime model.
        // Deduplicate by content hash so shared raster content across
        // multiple placements is only encoded once.
        if (region is Hex1bTerminalSnapshot snapshot3)
        {
            var renderedSixelImages = new Dictionary<byte[], string?>(SixelContentHashComparer.Instance);
            foreach (var placement in snapshot3.SixelPlacements.OrderBy(p => p.Sequence))
            {
                if (!placement.HasPaintedExtent)
                    continue; // Geometry-only placements paint nothing.

                if (!renderedSixelImages.TryGetValue(placement.Image.ContentHash, out var dataUri))
                {
                    var decoded = SixelDecoder.Decode(placement.Image);
                    dataUri = decoded is { Width: > 0, Height: > 0 } ? BmpEncoder.ToDataUri(decoded) : null;
                    renderedSixelImages[placement.Image.ContentHash] = dataUri;
                }

                if (dataUri is null)
                    continue;

                var imgX = placement.PaintedLeft * cellWidth;
                var imgY = placement.PaintedTop * cellHeight;
                var imgWidth = placement.PaintedColumnCount * cellWidth;
                var imgHeight = placement.PaintedRowCount * cellHeight;

                sb.AppendLine($"""    <image x="{imgX}" y="{imgY}" width="{imgWidth}" height="{imgHeight}" href="{dataUri}" preserveAspectRatio="none" style="image-rendering: pixelated;"/>""");
            }
        }

        sb.AppendLine("  </g>");

        // PASS 3: Text content with decorations (top layer)
        sb.AppendLine("  <g class=\"terminal-text\">");

        foreach (var (x, y, cell) in cells)
        {
            var attrs = cell.Attributes;
            var ch = cell.Character;
            var isReverse = (attrs & CellAttributes.Reverse) != 0;

            // Render text content (skip for empty/continuation cells)
            if (!string.IsNullOrEmpty(ch))
            {
                var displayCh = ch;

                // Normalize null character or unwritten-cell marker to space.
                // The unwritten marker (U+E000, private use) is what Surface
                // emits for cells that were never painted.
                if (displayCh == "\0" || displayCh == "\uE000")
                    displayCh = " ";

                // Hidden attribute: don't render the character at all
                var shouldRenderText =
                    (attrs & CellAttributes.Hidden) == 0 &&
                    !KgpUnicodePlaceholder.IsPlaceholder(displayCh);

                // Skip spaces unless they have a foreground color or special attributes
                if (displayCh == " " && !cell.Foreground.HasValue && attrs == CellAttributes.None)
                    shouldRenderText = false;

                if (shouldRenderText)
                {
                    // Determine cell group class (e.g., for hyperlink grouping)
                    var groupClass = "";
                    if (cell.TrackedHyperlink is { } trackedLink && hyperlinkGroups.TryGetValue(trackedLink, out var linkGroup))
                    {
                        groupClass = $" {linkGroup}";
                    }

                    // Wrap in cell group for interaction (hover highlight, hyperlinks)
                    sb.AppendLine($"""    <g class="cell{groupClass}" data-x="{x}" data-y="{y}">""");

                    var textX = x * cellWidth;
                    var textY = y * cellHeight + (cellHeight * 0.75); // Baseline adjustment

                    // Determine foreground color
                    string fgColor;
                    if (isReverse)
                    {
                        // Reverse: use background as foreground (or default background)
                        fgColor = cell.Background.HasValue 
                            ? $"rgb({cell.Background.Value.R},{cell.Background.Value.G},{cell.Background.Value.B})"
                            : options.DefaultBackground;
                    }
                    else if (cell.Foreground.HasValue)
                    {
                        var fg = cell.Foreground.Value;
                        fgColor = $"rgb({fg.R},{fg.G},{fg.B})";
                    }
                    else
                    {
                        fgColor = options.DefaultForeground;
                    }

                    // Build style attributes based on CellAttributes
                    var styleBuilder = new StringBuilder();

                    // Bold
                    if ((attrs & CellAttributes.Bold) != 0)
                        styleBuilder.Append("font-weight:bold;");

                    // Dim (reduced opacity)
                    if ((attrs & CellAttributes.Dim) != 0)
                        styleBuilder.Append("opacity:0.5;");

                    // Italic
                    if ((attrs & CellAttributes.Italic) != 0)
                        styleBuilder.Append("font-style:italic;");

                    // Text decorations (can be combined)
                    // Underline is rendered as a separate <line> element for color control
                    var decorations = new List<string>();
                    if ((attrs & CellAttributes.Strikethrough) != 0)
                        decorations.Add("line-through");
                    if ((attrs & CellAttributes.Overline) != 0)
                        decorations.Add("overline");

                    if (decorations.Count > 0)
                        styleBuilder.Append($"text-decoration:{string.Join(" ", decorations)};");

                    var style = styleBuilder.Length > 0 ? $""" style="{styleBuilder}" """ : "";
                    var blinkClass = (attrs & CellAttributes.Blink) != 0 ? " blink" : "";

                    // Use non-breaking space for spaces with text decorations (underline, strikethrough, overline)
                    // Regular spaces don't receive text-decoration in SVG/HTML, but &nbsp; does
                    bool hasUnderline = (attrs & CellAttributes.Underline) != 0;
                    var escapedChar = (displayCh == " " && (decorations.Count > 0 || hasUnderline)) 
                        ? "&#160;" 
                        : HttpUtility.HtmlEncode(displayCh);

                    // Check if this is a wide character that needs clipping
                    var charWidth = DisplayWidth.GetGraphemeWidth(displayCh);
                    string clipAttr = "";

                    if (charWidth > 1)
                    {
                        // Count owned continuation cells (matching sequence)
                        var ownedCells = 1;
                        for (int i = 1; i < charWidth && (x + i) < region.Width; i++)
                        {
                            var contCell = region.GetCell(x + i, y);
                            if (contCell.Character == "" && contCell.Sequence == cell.Sequence)
                                ownedCells++;
                            else
                                break;
                        }

                        // If truncated (owned < expected), apply clip path
                        if (ownedCells < charWidth)
                        {
                            var clipKey = (x, y, ownedCells);
                            if (clipPaths.TryGetValue(clipKey, out var clipPathId))
                            {
                                clipAttr = $""" clip-path="url(#{clipPathId})" """;
                            }
                        }
                    }

                    var indent = "      ";
                    var textClass = string.IsNullOrEmpty(blinkClass) ? "" : $""" class="{blinkClass.Trim()}" """;
                    sb.AppendLine($"""{indent}<text x="{textX:F1}" y="{textY:F1}" fill="{fgColor}" text-anchor="start"{style}{textClass}{clipAttr}>{escapedChar}</text>""");

                    if (hasUnderline)
                    {
                        string ulColor;
                        if (cell.UnderlineColor.HasValue)
                            ulColor = $"rgb({cell.UnderlineColor.Value.R},{cell.UnderlineColor.Value.G},{cell.UnderlineColor.Value.B})";
                        else
                            ulColor = fgColor;
                        var ulY = y * cellHeight + cellHeight * 0.9;
                        var ulX1 = x * cellWidth;
                        var ulX2 = (x + 1) * cellWidth;
                        
                        var ulStyle = cell.UnderlineStyle == UnderlineStyle.None 
                            ? UnderlineStyle.Single 
                            : cell.UnderlineStyle;
                        
                        switch (ulStyle)
                        {
                            case UnderlineStyle.Double:
                                // Two parallel lines, offset above and below the baseline
                                var ulY1 = ulY - 1.5;
                                var ulY2 = ulY + 1.5;
                                sb.AppendLine($"""{indent}<line x1="{ulX1:F1}" y1="{ulY1:F1}" x2="{ulX2:F1}" y2="{ulY1:F1}" stroke="{ulColor}" stroke-width="0.8"/>""");
                                sb.AppendLine($"""{indent}<line x1="{ulX1:F1}" y1="{ulY2:F1}" x2="{ulX2:F1}" y2="{ulY2:F1}" stroke="{ulColor}" stroke-width="0.8"/>""");
                                break;
                                
                            case UnderlineStyle.Curly:
                                // Wavy/sine curve using SVG path with cubic beziers
                                var amplitude = cellHeight * 0.08;
                                var halfW = (ulX2 - ulX1) / 2.0;
                                sb.AppendLine($"""{indent}<path d="M {ulX1:F1} {ulY:F1} C {ulX1 + halfW * 0.5:F1} {ulY - amplitude:F1}, {ulX1 + halfW * 0.5:F1} {ulY - amplitude:F1}, {ulX1 + halfW:F1} {ulY:F1} S {ulX2 - halfW * 0.5:F1} {ulY + amplitude:F1}, {ulX2:F1} {ulY:F1}" fill="none" stroke="{ulColor}" stroke-width="1"/>""");
                                break;
                                
                            case UnderlineStyle.Dotted:
                                sb.AppendLine($"""{indent}<line x1="{ulX1:F1}" y1="{ulY:F1}" x2="{ulX2:F1}" y2="{ulY:F1}" stroke="{ulColor}" stroke-width="1" stroke-dasharray="1.5,1.5"/>""");
                                break;
                                
                            case UnderlineStyle.Dashed:
                                sb.AppendLine($"""{indent}<line x1="{ulX1:F1}" y1="{ulY:F1}" x2="{ulX2:F1}" y2="{ulY:F1}" stroke="{ulColor}" stroke-width="1" stroke-dasharray="3,2"/>""");
                                break;
                                
                            default: // Single
                                sb.AppendLine($"""{indent}<line x1="{ulX1:F1}" y1="{ulY:F1}" x2="{ulX2:F1}" y2="{ulY:F1}" stroke="{ulColor}" stroke-width="1"/>""");
                                break;
                        }
                    }

                    sb.AppendLine("    </g>");
                }
            }
        }

        sb.AppendLine("  </g>");

        // Render cursor if within bounds
        if (cursorX.HasValue && cursorY.HasValue &&
            cursorX.Value >= 0 && cursorX.Value < region.Width &&
            cursorY.Value >= 0 && cursorY.Value < region.Height)
        {
            var cursorRectX = cursorX.Value * cellWidth;
            var cursorRectY = cursorY.Value * cellHeight;
            sb.AppendLine($"""  <rect class="cursor" x="{cursorRectX}" y="{cursorRectY}" width="{cellWidth}" height="{cellHeight}"/>""");
        }

        // Render scrollback separator line (bright dotted line between scrollback and visible area)
        if (scrollbackLineCount > 0)
        {
            var separatorY = scrollbackLineCount * cellHeight;
            // Draw a glow behind the line for visibility on both light and dark backgrounds
            sb.AppendLine($"""  <line x1="0" y1="{separatorY}" x2="{width}" y2="{separatorY}" stroke="rgba(0,0,0,0.5)" stroke-width="5" stroke-dasharray="8,4" />""");
            sb.AppendLine($"""  <line x1="0" y1="{separatorY}" x2="{width}" y2="{separatorY}" stroke="#ff6b6b" stroke-width="2" stroke-dasharray="8,4" />""");
        }

        // Render pixel grid lines (shows pixel boundaries within each cell)
        if (options.ShowPixelGrid)
        {
            sb.AppendLine("  <g class=\"pixel-grid\">");
            // Vertical pixel lines - one per pixel column
            for (int px = 1; px < width; px++)
            {
                sb.AppendLine($"""    <line x1="{px}" y1="0" x2="{px}" y2="{height}"/>""");
            }
            // Horizontal pixel lines - one per pixel row
            for (int py = 1; py < height; py++)
            {
                sb.AppendLine($"""    <line x1="0" y1="{py}" x2="{width}" y2="{py}"/>""");
            }
            sb.AppendLine("  </g>");
        }

        // Render cell grid lines (coarser grid, one line per cell boundary)
        if (options.ShowCellGrid)
        {
            sb.AppendLine("  <g class=\"cell-grid\">");
            // Vertical cell lines
            for (int col = 1; col < region.Width; col++)
            {
                var lineX = col * cellWidth;
                sb.AppendLine($"""    <line x1="{lineX}" y1="0" x2="{lineX}" y2="{height}"/>""");
            }
            // Horizontal cell lines
            for (int row = 1; row < region.Height; row++)
            {
                var lineY = row * cellHeight;
                sb.AppendLine($"""    <line x1="0" y1="{lineY}" x2="{width}" y2="{lineY}"/>""");
            }
            sb.AppendLine("  </g>");
        }

        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    private static string? EncodeKgpImageToDataUri(
        byte[] data, uint width, uint height, KgpFormat format,
        uint sourceX = 0, uint sourceY = 0,
        uint sourceWidth = 0, uint sourceHeight = 0)
    {
        if (data.Length == 0)
            return null;

        if (format == KgpFormat.Png)
            return "data:image/png;base64," + Convert.ToBase64String(data);

        var bytesPerPixel = format == KgpFormat.Rgb24 ? 3 : 4;
        if (!TryGetRasterCrop(
                data,
                width,
                height,
                bytesPerPixel,
                sourceX,
                sourceY,
                sourceWidth,
                sourceHeight,
                out var fullWidth,
                out var cropX,
                out var cropY,
                out var cropWidth,
                out var cropHeight))
        {
            return null;
        }

        return format == KgpFormat.Rgba32
            ? EncodeRgbaPng(
                data,
                fullWidth,
                cropX,
                cropY,
                cropWidth,
                cropHeight)
            : EncodeRgbBmp(
                data,
                fullWidth,
                cropX,
                cropY,
                cropWidth,
                cropHeight);
    }

    private static string EncodeRgbBmp(
        byte[] data,
        int fullWidth,
        int cropX,
        int cropY,
        int width,
        int height)
    {
        // Create BMP with bottom-up row order
        int rowSize = (width * 3 + 3) & ~3; // BMP rows are 4-byte aligned
        int headerSize = 54;
        int imageSize = rowSize * height;
        int fileSize = headerSize + imageSize;
        var bmp = new byte[fileSize];

        // BMP header
        bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
        BitConverter.TryWriteBytes(bmp.AsSpan(2), fileSize);
        BitConverter.TryWriteBytes(bmp.AsSpan(10), headerSize);
        // DIB header
        BitConverter.TryWriteBytes(bmp.AsSpan(14), 40);
        BitConverter.TryWriteBytes(bmp.AsSpan(18), width);
        BitConverter.TryWriteBytes(bmp.AsSpan(22), height);
        bmp[26] = 1; // planes
        bmp[28] = 24; // bits per pixel (output as RGB24)
        BitConverter.TryWriteBytes(bmp.AsSpan(34), imageSize);

        // Write pixel data (BMP is bottom-up, BGR order)
        for (int y = 0; y < height; y++)
        {
            int srcRow = (cropY + height - 1 - y) * fullWidth * 3;
            int dstRow = y * rowSize;
            for (int x = 0; x < width; x++)
            {
                int si = srcRow + (cropX + x) * 3;
                int di = dstRow + x * 3;
                bmp[headerSize + di] = data[si + 2]; // B
                bmp[headerSize + di + 1] = data[si + 1]; // G
                bmp[headerSize + di + 2] = data[si]; // R
            }
        }

        return "data:image/bmp;base64," + Convert.ToBase64String(bmp);
    }

    private static string EncodeRgbaPng(
        byte[] data,
        int fullWidth,
        int cropX,
        int cropY,
        int width,
        int height)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(
            compressed,
            CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0);
                var sourceOffset =
                    ((cropY + y) * fullWidth + cropX) * 4;
                zlib.Write(data.AsSpan(sourceOffset, width * 4));
            }
        }

        using var png = new MemoryStream();
        png.Write(
        [
            0x89, (byte)'P', (byte)'N', (byte)'G',
            0x0D, 0x0A, 0x1A, 0x0A,
        ]);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], checked((uint)height));
        header[8] = 8;
        header[9] = 6;
        WritePngChunk(png, "IHDR"u8, header);
        WritePngChunk(png, "IDAT"u8, compressed.ToArray());
        WritePngChunk(png, "IEND"u8, []);

        return "data:image/png;base64," +
            Convert.ToBase64String(png.ToArray());
    }

    private static void WritePngChunk(
        Stream destination,
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> payload)
    {
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(
            value,
            checked((uint)payload.Length));
        destination.Write(value);
        destination.Write(type);
        destination.Write(payload);
        BinaryPrimitives.WriteUInt32BigEndian(
            value,
            ComputePngCrc(type, payload));
        destination.Write(value);
    }

    private static uint ComputePngCrc(
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> payload)
    {
        var crc = uint.MaxValue;
        UpdatePngCrc(ref crc, type);
        UpdatePngCrc(ref crc, payload);
        return ~crc;
    }

    private static void UpdatePngCrc(
        ref uint crc,
        ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0
                    ? (crc >> 1) ^ 0xEDB88320u
                    : crc >> 1;
            }
        }
    }

    private static bool TryGetRasterCrop(
        byte[] data,
        uint width,
        uint height,
        int bytesPerPixel,
        uint sourceX,
        uint sourceY,
        uint sourceWidth,
        uint sourceHeight,
        out int fullWidth,
        out int cropX,
        out int cropY,
        out int cropWidth,
        out int cropHeight)
    {
        fullWidth = 0;
        cropX = 0;
        cropY = 0;
        cropWidth = 0;
        cropHeight = 0;
        if (width == 0 ||
            height == 0 ||
            width > int.MaxValue ||
            height > int.MaxValue ||
            sourceX >= width ||
            sourceY >= height ||
            data.LongLength < (long)width * height * bytesPerPixel)
        {
            return false;
        }

        fullWidth = checked((int)width);
        var fullHeight = checked((int)height);
        cropX = checked((int)sourceX);
        cropY = checked((int)sourceY);
        cropWidth = sourceWidth == 0
            ? fullWidth - cropX
            : checked((int)Math.Min(sourceWidth, width - sourceX));
        cropHeight = sourceHeight == 0
            ? fullHeight - cropY
            : checked((int)Math.Min(sourceHeight, height - sourceY));
        return cropWidth > 0 && cropHeight > 0;
    }

    private static bool TryGetPngRenderBounds(
        KgpPlacement placement,
        KgpImageData image,
        double destinationX,
        double destinationY,
        double destinationWidth,
        double destinationHeight,
        out double imageX,
        out double imageY,
        out double imageWidth,
        out double imageHeight,
        out bool requiresClip)
    {
        imageX = destinationX;
        imageY = destinationY;
        imageWidth = destinationWidth;
        imageHeight = destinationHeight;
        requiresClip = false;

        double fullWidth;
        double fullHeight;
        if (image.Width > 0 && image.Height > 0)
        {
            fullWidth = image.Width;
            fullHeight = image.Height;
        }
        else
        {
            return placement.SourceX == 0 &&
                placement.SourceY == 0 &&
                placement.SourceWidth == 0 &&
                placement.SourceHeight == 0;
        }

        if (placement.SourceX >= fullWidth ||
            placement.SourceY >= fullHeight)
        {
            return false;
        }

        var sourceWidth = placement.SourceWidth > 0
            ? Math.Min(placement.SourceWidth, fullWidth - placement.SourceX)
            : fullWidth - placement.SourceX;
        var sourceHeight = placement.SourceHeight > 0
            ? Math.Min(placement.SourceHeight, fullHeight - placement.SourceY)
            : fullHeight - placement.SourceY;
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return false;

        requiresClip = placement.SourceX > 0 ||
            placement.SourceY > 0 ||
            sourceWidth < fullWidth ||
            sourceHeight < fullHeight;
        if (!requiresClip)
            return true;

        var scaleX = destinationWidth / sourceWidth;
        var scaleY = destinationHeight / sourceHeight;
        imageX = destinationX - placement.SourceX * scaleX;
        imageY = destinationY - placement.SourceY * scaleY;
        imageWidth = fullWidth * scaleX;
        imageHeight = fullHeight * scaleY;
        return double.IsFinite(imageX) &&
            double.IsFinite(imageY) &&
            double.IsFinite(imageWidth) &&
            double.IsFinite(imageHeight) &&
            imageWidth > 0 &&
            imageHeight > 0;
    }

    private static string FormatSvgNumber(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>
/// Options for SVG rendering of terminal regions.
/// </summary>
public class TerminalSvgOptions
{
    /// <summary>
    /// The font family to use for rendering. Should be a monospace font.
    /// </summary>
    public string FontFamily { get; set; } = "'Cascadia Code', 'Fira Code', Consolas, Monaco, 'Courier New', monospace";

    /// <summary>
    /// The font size in pixels.
    /// </summary>
    public int FontSize { get; set; } = 14;

    /// <summary>
    /// The width of each cell in pixels.
    /// </summary>
    public int CellWidth { get; set; } = 9;

    /// <summary>
    /// The height of each cell in pixels.
    /// </summary>
    public int CellHeight { get; set; } = 18;

    /// <summary>
    /// The default background color (CSS color string).
    /// </summary>
    public string DefaultBackground { get; set; } = "#1e1e1e";

    /// <summary>
    /// The default foreground color (CSS color string).
    /// </summary>
    public string DefaultForeground { get; set; } = "#d4d4d4";

    /// <summary>
    /// The cursor color (CSS color string).
    /// </summary>
    public string CursorColor { get; set; } = "#ffffff";

    /// <summary>
    /// Whether to show cell grid lines. Default is true.
    /// </summary>
    public bool ShowCellGrid { get; set; } = true;

    /// <summary>
    /// Whether to show pixel grid lines. Default is false.
    /// </summary>
    public bool ShowPixelGrid { get; set; } = false;

    /// <summary>
    /// The color of the cell grid lines (CSS color string).
    /// </summary>
    public string CellGridColor { get; set; } = "rgba(128, 128, 128, 0.5)";

    /// <summary>
    /// The color of the pixel grid lines (CSS color string).
    /// </summary>
    public string PixelGridColor { get; set; } = "rgba(64, 64, 64, 0.3)";
}
