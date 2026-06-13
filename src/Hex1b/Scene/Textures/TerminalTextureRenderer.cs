namespace Hex1b.Scene.Textures;

using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

/// <summary>
/// Renders any widget to a SceneTexture2D by rendering it to a terminal buffer
/// and converting terminal cell colors to RGB pixels.
/// 
/// This enables any Hex1b widget to be used as a texture on 3D mesh surfaces.
/// The rendered terminal output is sampled and converted to RGBA32 pixel data.
/// </summary>
public class TerminalTextureRenderer
{
    /// <summary>
    /// Render a widget to a texture by converting terminal rendering output to RGB pixels.
    /// </summary>
    /// <remarks>
    /// Any widget can be rendered:
    /// - Terminal widgets (Text, Grid, etc) → rendered normally
    /// - SceneWidget → renders the 3D scene to terminal cells → converted to texture
    /// - Complex widgets → flattened to terminal output → RGB conversion
    /// 
    /// The terminal foreground color of each cell is sampled and converted to RGB.
    /// </remarks>
    /// <param name="widget">The widget to render (any Hex1bWidget)</param>
    /// <param name="width">Texture width in terminal cells</param>
    /// <param name="height">Texture height in terminal cells</param>
    /// <param name="theme">Optional theme for rendering (uses default if null)</param>
    /// <returns>SceneTexture2D with RGBA32 pixel data from terminal output</returns>
    public static SceneTexture2D RenderToTexture(
        Hex1bWidget widget, 
        int width, 
        int height,
        Hex1bTheme? theme = null)
    {
        if (widget == null) throw new ArgumentNullException(nameof(widget));
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Texture dimensions must be positive");

        theme ??= Hex1bThemes.Default;

        // Create texture buffer
        var texture = new SceneTexture2D(width, height);

        // Render widget using off-screen buffer
        var buffer = new OffScreenRenderBuffer(width, height);

        // Get the expected node type for this widget
        var nodeType = widget.GetExpectedNodeType();
#pragma warning disable IL2072
        var node = (Hex1bNode)Activator.CreateInstance(nodeType)!;
#pragma warning restore IL2072

        // Measure widget to fit constraints
        var measureConstraints = new Constraints(width, height, width, height);
        var measuredSize = node.Measure(measureConstraints);
        var rect = new Rect(0, 0, System.Math.Min(measuredSize.Width, width), System.Math.Min(measuredSize.Height, height));
        node.Arrange(rect);

        // Render to buffer using a mock context
        var renderContext = new OffScreenRenderContext(buffer, theme);
        node.Render(renderContext);

        // Convert buffer to RGBA32 texture
        ConvertBufferToTexture(buffer, texture);

        return texture;
    }

    private static void ConvertBufferToTexture(
        OffScreenRenderBuffer buffer, 
        SceneTexture2D texture)
    {
        var cells = buffer.GetCells();

        for (int y = 0; y < buffer.Height && y < texture.Height; y++)
        {
            for (int x = 0; x < buffer.Width && x < texture.Width; x++)
            {
                var cell = cells[y * buffer.Width + x];
                
                // Extract color from cell
                var color = cell.ForegroundColor;
                
                // Set texture pixel (color → RGB32)
                texture.SetPixel(x, y, color.R, color.G, color.B, 255);
            }
        }
    }
}

/// <summary>
/// Buffer for off-screen rendering.
/// </summary>
internal class OffScreenRenderBuffer
{
    private readonly OffScreenCell[] _cells;
    public int Width { get; }
    public int Height { get; }

    public OffScreenRenderBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new OffScreenCell[width * height];
        
        // Initialize with cells containing white foreground
        for (int i = 0; i < _cells.Length; i++)
            _cells[i] = new OffScreenCell { ForegroundColor = Hex1bColor.White };
    }

    public void SetCell(int x, int y, OffScreenCell cell)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;
        _cells[y * Width + x] = cell;
    }

    public OffScreenCell[] GetCells() => _cells;
}

/// <summary>
/// Cell data for off-screen rendering.
/// </summary>
internal struct OffScreenCell
{
    public string Content { get; set; }
    public Hex1bColor ForegroundColor { get; set; }

    public OffScreenCell()
    {
        Content = " ";
        ForegroundColor = Hex1bColor.White;
    }
}

/// <summary>
/// Render context for off-screen rendering.
/// </summary>
internal class OffScreenRenderContext : Hex1bRenderContext
{
    private readonly OffScreenRenderBuffer _buffer;

    public OffScreenRenderContext(OffScreenRenderBuffer buffer, Hex1bTheme theme) 
        : base(theme)
    {
        _buffer = buffer;
    }

    public override void WriteClipped(int x, int y, string text)
    {
        if (x < 0 || x >= _buffer.Width || y < 0 || y >= _buffer.Height)
            return;
        
        _buffer.SetCell(x, y, new OffScreenCell { Content = text, ForegroundColor = Hex1bColor.White });
    }

    public override void Clear()
    {
        for (int i = 0; i < _buffer.GetCells().Length; i++)
            _buffer.GetCells()[i] = new OffScreenCell();
    }
}
