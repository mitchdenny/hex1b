namespace Hex1b.Scene.Textures;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Theming;

/// <summary>
/// A live texture whose pixels are sampled from a terminal screen buffer. Wrap a
/// <see cref="TerminalTextureSource"/> (which references a <see cref="TerminalWidgetHandle"/>)
/// and call <see cref="Update"/> once per frame to project the terminal's current content
/// onto mesh geometry.
/// </summary>
/// <remarks>
/// <para>
/// The produced <see cref="SceneTexture2D"/> is reused between frames and only reallocated
/// when the source terminal changes size, so per-frame updates avoid churning allocations.
/// Assign <see cref="Texture"/> (or the return value of <see cref="Update"/>) to a
/// <see cref="Materials.SceneTextureMaterial.Texture"/>.
/// </para>
/// <code>
/// var termTex = new TerminalTexture(new TerminalTextureSource(handle));
/// // each frame:
/// material.Texture = termTex.Update();
/// </code>
/// </remarks>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public sealed class TerminalTexture
{
    private readonly TerminalTextureSource _source;
    private SceneTexture2D? _texture;
    private int _bufferWidth;
    private int _bufferHeight;

    /// <summary>Pixels each terminal cell expands to horizontally.</summary>
    public int CellPixelWidth { get; }

    /// <summary>Pixels each terminal cell expands to vertically.</summary>
    public int CellPixelHeight { get; }

    /// <summary>Colour used when a cell's foreground is the terminal default.</summary>
    public Hex1bColor DefaultForeground { get; set; }

    /// <summary>Colour used when a cell's background is the terminal default.</summary>
    public Hex1bColor DefaultBackground { get; set; }

    /// <summary>
    /// Creates a terminal-backed texture.
    /// </summary>
    /// <param name="source">The terminal buffer source to sample each frame.</param>
    /// <param name="cellPixelWidth">
    /// Pixels per cell horizontally. Larger values give finer reconstruction of quadrant and
    /// braille glyphs at the cost of texture size.
    /// </param>
    /// <param name="cellPixelHeight">
    /// Pixels per cell vertically. At least 2 is recommended so half-block glyphs reconstruct.
    /// Use 4 for faithful braille.
    /// </param>
    public TerminalTexture(
        TerminalTextureSource source,
        int cellPixelWidth = TerminalCellTextureSampler.DefaultCellPixelWidth,
        int cellPixelHeight = TerminalCellTextureSampler.DefaultCellPixelHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (cellPixelWidth <= 0 || cellPixelHeight <= 0)
            throw new ArgumentException("Cell pixel dimensions must be positive.");

        _source = source;
        CellPixelWidth = cellPixelWidth;
        CellPixelHeight = cellPixelHeight;
        DefaultForeground = Hex1bColor.White;
        DefaultBackground = Hex1bColor.Black;
    }

    /// <summary>
    /// Gets the current texture. Returns <see langword="null"/> until <see cref="Update"/>
    /// has been called at least once.
    /// </summary>
    public SceneTexture2D? Texture => _texture;

    /// <summary>
    /// Samples the source terminal's current buffer into the texture and returns it.
    /// Reallocates the underlying texture only when the terminal size has changed.
    /// </summary>
    public SceneTexture2D Update()
    {
        var (buffer, width, height) = _source.GetScreenBufferSnapshot();

        if (width <= 0 || height <= 0)
        {
            // Degenerate terminal size: hand back a 1x1 texture so callers always get something.
            _texture ??= new SceneTexture2D(1, 1);
            return _texture;
        }

        if (_texture is null || width != _bufferWidth || height != _bufferHeight)
        {
            _texture = new SceneTexture2D(width * CellPixelWidth, height * CellPixelHeight);
            _bufferWidth = width;
            _bufferHeight = height;
        }

        TerminalCellTextureSampler.SampleInto(
            _texture,
            buffer,
            width,
            height,
            CellPixelWidth,
            CellPixelHeight,
            DefaultForeground,
            DefaultBackground);

        return _texture;
    }
}
