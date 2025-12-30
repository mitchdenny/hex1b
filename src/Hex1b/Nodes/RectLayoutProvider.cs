using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A simple layout provider that clips to a given rectangle.
/// Used by containers like SplitterNode that need to clip children to specific regions.
/// </summary>
internal sealed class RectLayoutProvider : ILayoutProvider
{
    private readonly Rect _clipRect;

    public RectLayoutProvider(Rect clipRect)
    {
        _clipRect = clipRect;
    }

    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    /// <inheritdoc />
    public Rect ClipRect => _clipRect;

    /// <inheritdoc />
    public ClipMode ClipMode => ClipMode.Clip;

    /// <inheritdoc />
    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    /// <inheritdoc />
    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);
}
