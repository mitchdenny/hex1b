using System.Diagnostics.CodeAnalysis;
using Hex1b.Layout;
using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace Hex1b.Nodes;

/// <summary>
/// Renders Sixel graphics when the effective presentation supports them,
/// otherwise renders a fallback node.
/// </summary>
[Experimental("HEX1B_SIXEL", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/sixel.md")]
public sealed class SixelNode : Hex1bNode
{
    private string? _imageData;
    private SixelPixelBuffer? _pixels;
    private SixelExtent _pixelExtent;
    private int? _requestedWidth;
    private int? _requestedHeight;

    /// <summary>
    /// Gets or sets validated pre-encoded Sixel data.
    /// </summary>
    public string ImageData
    {
        get => _imageData ?? string.Empty;
        set => SetEncodedImage(value);
    }

    /// <summary>
    /// Gets or sets the fallback node rendered when Sixel is unavailable.
    /// </summary>
    public Hex1bNode? Fallback { get; set; }

    /// <summary>
    /// Gets or sets the requested width in terminal cells.
    /// </summary>
    public int? RequestedWidth
    {
        get => _requestedWidth;
        set
        {
            if (_requestedWidth != value)
            {
                _requestedWidth = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Gets or sets the requested height in terminal cells.
    /// </summary>
    public int? RequestedHeight
    {
        get => _requestedHeight;
        set
        {
            if (_requestedHeight != value)
            {
                _requestedHeight = value;
                MarkDirty();
            }
        }
    }

    internal void SetPixels(SixelPixelBuffer pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (ReferenceEquals(_pixels, pixels) && _imageData is null)
        {
            return;
        }

        _pixels = pixels;
        _imageData = null;
        _pixelExtent = new SixelExtent(pixels.Width, pixels.Height);
        MarkDirty();
    }

    internal void SetEncodedImage(string imageData)
    {
        var normalized = SixelPayload.NormalizeAndValidate(imageData, nameof(imageData));
        if (_imageData == normalized && _pixels is null)
        {
            return;
        }

        var parsed = SixelParser.ParsePayload(normalized);
        _pixels = null;
        _imageData = normalized;
        _pixelExtent = parsed.LogicalCanvasExtent;
        MarkDirty();
    }

    /// <inheritdoc />
    protected override Size MeasureCore(Constraints constraints)
    {
        if (!IsSixelSupported(TerminalCapabilities))
        {
            return Fallback?.Measure(constraints) ?? Size.Zero;
        }

        var metrics = TerminalCapabilities.SixelCellMetrics
            ?? SixelCellMetrics.FromCapabilities(TerminalCapabilities);
        var width = RequestedWidth ?? metrics.ColumnsFor(_pixelExtent.Width);
        var height = RequestedHeight ?? metrics.RowsFor(_pixelExtent.Height);
        return constraints.Constrain(new Size(width, height));
    }

    /// <inheritdoc />
    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        if (!IsSixelSupported(TerminalCapabilities))
        {
            Fallback?.Arrange(bounds);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (!IsSixelSupported(TerminalCapabilities) && Fallback is not null)
        {
            foreach (var focusable in Fallback.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    /// <inheritdoc />
    public override void Render(Hex1bRenderContext context)
    {
        if (IsSixelSupported(context.Capabilities))
        {
            RenderSixel(context);
        }
        else
        {
            RenderFallback(context);
        }
    }

    private void RenderSixel(Hex1bRenderContext context)
    {
        if (_pixels is null && _imageData is null)
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write("[No image data]");
            return;
        }

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        context.SetCursorPosition(Bounds.X, Bounds.Y);
        if (_pixels is not null)
        {
            context.WriteSixel(_pixels, Bounds.Width, Bounds.Height);
        }
        else
        {
            context.WriteSixel(_imageData!, Bounds.Width, Bounds.Height);
        }
    }

    private void RenderFallback(Hex1bRenderContext context)
    {
        if (Fallback is not null)
        {
            context.RenderChild(Fallback);
            return;
        }

        context.SetCursorPosition(Bounds.X, Bounds.Y);
        context.Write("[Sixel not supported]");
    }

    /// <inheritdoc />
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Fallback is not null)
        {
            yield return Fallback;
        }
    }

    internal override IEnumerable<Hex1bNode> GetInputChildren()
        => IsSixelSupported(TerminalCapabilities) || Fallback is null ? [] : [Fallback];

    /// <inheritdoc />
    protected override void OnTerminalCapabilitiesChanged() => MarkDirty();

    internal static bool IsSixelSupported(TerminalCapabilities capabilities)
        => capabilities.SixelSupport is SixelPresentationSupport.Native or SixelPresentationSupport.Headless
            || capabilities.SixelSupport == SixelPresentationSupport.Unknown && capabilities.SupportsSixel;
}
