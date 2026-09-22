using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Web;

namespace Hex1b.Automation;

/// <summary>
/// Options for SVG rendering of terminal regions.
/// </summary>
public class TerminalSvgOptions
{
    /// <summary>
    /// Gets or sets the maximum aggregate number of bytes used by embedded
    /// Sixel BMP data URIs. Placements beyond the limit render as diagnostic
    /// placeholders. The default is 64 MiB.
    /// </summary>
    public long MaximumEmbeddedSixelBytes { get; set; } = 64L * 1024 * 1024;

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
