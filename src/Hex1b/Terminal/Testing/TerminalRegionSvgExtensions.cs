using System.Text;
using System.Web;

namespace Hex1b.Terminal.Testing;

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
    /// <param name="options">Optional rendering options.</param>
    /// <returns>An SVG string representation of the terminal snapshot.</returns>
    public static string ToSvg(this Hex1bTerminalSnapshot snapshot, TerminalSvgOptions? options = null)
    {
        options ??= DefaultOptions;
        return RenderToSvg(snapshot, options, snapshot.CursorX, snapshot.CursorY);
    }

    private static string RenderToSvg(IHex1bTerminalRegion region, TerminalSvgOptions options, int? cursorX, int? cursorY)
    {
        var cellWidth = options.CellWidth;
        var cellHeight = options.CellHeight;
        var width = region.Width * cellWidth;
        var height = region.Height * cellHeight;

        var sb = new StringBuilder();

        // SVG header
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");

        // Style definitions including blink animation
        sb.AppendLine("  <defs>");
        sb.AppendLine("    <style>");
        sb.AppendLine($"      .terminal-text {{ font-family: {options.FontFamily}; font-size: {options.FontSize}px; }}");
        sb.AppendLine($"      .cursor {{ fill: {options.CursorColor}; opacity: 0.7; }}");
        sb.AppendLine("      @keyframes blink { 0%, 49% { opacity: 1; } 50%, 100% { opacity: 0.3; } }");
        sb.AppendLine("      .blink { animation: blink 1s infinite; }");
        sb.AppendLine("    </style>");
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

        // Group for cells
        sb.AppendLine("  <g class=\"terminal-text\">");

        // Render all cells in sequence order (backgrounds first, then text)
        // This allows newer content to naturally overlap/obscure older wide characters
        foreach (var (x, y, cell) in cells)
        {
            var attrs = cell.Attributes;
            
            // Render background for this cell - always opaque for proper clipping behavior
            var isReverse = (attrs & CellAttributes.Reverse) != 0;
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
            
            sb.AppendLine($"""    <rect x="{rectX}" y="{rectY}" width="{cellWidth}" height="{cellHeight}" fill="{bgColor}"/>""");
            
            // Blink indicator: subtle border/glow around blinking cells
            if ((attrs & CellAttributes.Blink) != 0)
            {
                sb.AppendLine($"""    <rect x="{rectX}" y="{rectY}" width="{cellWidth}" height="{cellHeight}" fill="none" stroke="#ffcc00" stroke-width="1" stroke-dasharray="2,2" class="blink"/>""");
            }

            // Now render the text character
            var ch = cell.Character;

            // Skip empty continuation cells (used for wide characters)
            if (string.IsNullOrEmpty(ch))
                continue;

            // Normalize null character to space
            if (ch == "\0")
                ch = " ";

            // Hidden attribute: don't render the character at all
            if ((attrs & CellAttributes.Hidden) != 0)
                continue;

            // Skip spaces unless they have a foreground color or special attributes
            if (ch == " " && !cell.Foreground.HasValue && attrs == CellAttributes.None)
                continue;

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
            var decorations = new List<string>();
            if ((attrs & CellAttributes.Underline) != 0)
                decorations.Add("underline");
            if ((attrs & CellAttributes.Strikethrough) != 0)
                decorations.Add("line-through");
            if ((attrs & CellAttributes.Overline) != 0)
                decorations.Add("overline");
            
            if (decorations.Count > 0)
                styleBuilder.Append($"text-decoration:{string.Join(" ", decorations)};");

            var style = styleBuilder.Length > 0 ? $""" style="{styleBuilder}" """ : "";
            var blinkClass = (attrs & CellAttributes.Blink) != 0 ? " class=\"blink\"" : "";

            var escapedChar = HttpUtility.HtmlEncode(ch);
            sb.AppendLine($"""    <text x="{textX:F1}" y="{textY:F1}" fill="{fgColor}" text-anchor="start"{style}{blinkClass}>{escapedChar}</text>""");
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

        sb.AppendLine("</svg>");

        return sb.ToString();
    }
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
}
