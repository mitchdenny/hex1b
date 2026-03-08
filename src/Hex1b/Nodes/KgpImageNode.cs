using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that renders KGP (Kitty Graphics Protocol) images if the terminal supports it,
/// otherwise falls back to rendering a fallback node.
/// </summary>
public sealed class KgpImageNode : Hex1bNode
{
    private byte[] _imageData = [];
    private int _pixelWidth;
    private int _pixelHeight;
    private int? _requestedWidth;
    private int? _requestedHeight;
    private KgpZOrder _zOrder = KgpZOrder.BelowText;
    private KgpImageStretch _stretch = KgpImageStretch.Stretch;

    // Approximate terminal cell dimensions for aspect ratio calculations.
    private const double CellPixelWidth = 10.0;
    private const double CellPixelHeight = 20.0;

    /// <summary>
    /// The raw RGBA32 pixel data for the image.
    /// </summary>
    public byte[] ImageData
    {
        get => _imageData;
        set
        {
            if (!ReferenceEquals(_imageData, value))
            {
                _imageData = value;
                Parent?.MarkDirty();
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Width of the source image in pixels.
    /// </summary>
    public int PixelWidth
    {
        get => _pixelWidth;
        set
        {
            if (_pixelWidth != value)
            {
                _pixelWidth = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Height of the source image in pixels.
    /// </summary>
    public int PixelHeight
    {
        get => _pixelHeight;
        set
        {
            if (_pixelHeight != value)
            {
                _pixelHeight = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Requested width in character cells. If null, computed from pixel dimensions.
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
    /// Requested height in character cells. If null, computed from pixel dimensions.
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

    /// <summary>
    /// Z-ordering relative to text content.
    /// </summary>
    public KgpZOrder ZOrder
    {
        get => _zOrder;
        set
        {
            if (_zOrder != value)
            {
                _zOrder = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// How the image is scaled within its allocated cell area.
    /// </summary>
    public KgpImageStretch Stretch
    {
        get => _stretch;
        set
        {
            if (_stretch != value)
            {
                _stretch = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// The fallback node to render if KGP is not supported.
    /// </summary>
    public Hex1bNode? Fallback { get; set; }

    /// <summary>
    /// Computes the natural cell dimensions from pixel dimensions.
    /// </summary>
    internal static (int Width, int Height) NaturalCellSize(int pixelWidth, int pixelHeight)
        => (Math.Max(1, (pixelWidth + 9) / 10), Math.Max(1, (pixelHeight + 19) / 20));

    protected override Size MeasureCore(Constraints constraints)
    {
        var fallbackSize = Fallback?.Measure(constraints) ?? Size.Zero;

        var (naturalW, naturalH) = NaturalCellSize(PixelWidth, PixelHeight);

        // All modes claim the same layout space — Stretch mode only affects rendering.
        // When SizeHint.Fill is set, expand to fill constraints (guard int.MaxValue).
        var cellWidth = RequestedWidth ?? (WidthHint == SizeHint.Fill && constraints.MaxWidth < int.MaxValue
            ? constraints.MaxWidth
            : naturalW);
        var cellHeight = RequestedHeight ?? (HeightHint == SizeHint.Fill && constraints.MaxHeight < int.MaxValue
            ? constraints.MaxHeight
            : naturalH);

        var kgpSize = constraints.Constrain(new Size(cellWidth, cellHeight));

        return new Size(
            Math.Max(fallbackSize.Width, kgpSize.Width),
            Math.Max(fallbackSize.Height, kgpSize.Height));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        Fallback?.Arrange(bounds);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Fallback != null)
        {
            foreach (var focusable in Fallback.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (context.Capabilities.SupportsKgp)
        {
            RenderKgp(context);
        }
        else
        {
            RenderFallback(context);
        }
    }

    private void RenderKgp(Hex1bRenderContext context)
    {
        if (ImageData.Length == 0)
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write("[No image data]");
            return;
        }

        context.SetCursorPosition(Bounds.X, Bounds.Y);

        var (naturalW, naturalH) = NaturalCellSize(PixelWidth, PixelHeight);
        int cellWidth, cellHeight;

        switch (Stretch)
        {
            case KgpImageStretch.None:
                // Render at natural pixel-to-cell dimensions regardless of Bounds
                cellWidth = RequestedWidth ?? naturalW;
                cellHeight = RequestedHeight ?? naturalH;
                break;

            case KgpImageStretch.Fit:
            {
                // Fit within Bounds maintaining aspect ratio
                var boundsW = RequestedWidth ?? (Bounds.Width > 0 ? Bounds.Width : naturalW);
                var boundsH = RequestedHeight ?? (Bounds.Height > 0 ? Bounds.Height : naturalH);
                var scaleW = (double)boundsW / naturalW;
                var scaleH = (double)boundsH / naturalH;
                var scale = Math.Min(scaleW, scaleH);
                cellWidth = Math.Max(1, (int)Math.Round(naturalW * scale));
                cellHeight = Math.Max(1, (int)Math.Round(naturalH * scale));
                break;
            }

            case KgpImageStretch.Fill:
            {
                // Fill Bounds with source-rect crop to maintain aspect ratio
                cellWidth = RequestedWidth
                    ?? (Bounds.Width > 0 ? Bounds.Width : naturalW);
                cellHeight = RequestedHeight
                    ?? (Bounds.Height > 0 ? Bounds.Height : naturalH);
                var (clipX, clipY, clipW, clipH) = ComputeFillClip(
                    PixelWidth, PixelHeight, cellWidth, cellHeight);
                context.WriteKgp(ImageData, PixelWidth, PixelHeight, cellWidth, cellHeight, ZOrder,
                    clipX, clipY, clipW, clipH);
                return;
            }

            case KgpImageStretch.Stretch:
            default:
                // Stretch to fill Bounds (may distort)
                cellWidth = RequestedWidth
                    ?? (Bounds.Width > 0 ? Bounds.Width : naturalW);
                cellHeight = RequestedHeight
                    ?? (Bounds.Height > 0 ? Bounds.Height : naturalH);
                break;
        }

        context.WriteKgp(ImageData, PixelWidth, PixelHeight, cellWidth, cellHeight, ZOrder);
    }

    /// <summary>
    /// Computes the source-rectangle crop for Fill scaling.
    /// Centers the crop so that the image fills the display area while
    /// preserving the original aspect ratio.
    /// </summary>
    internal static (int ClipX, int ClipY, int ClipW, int ClipH) ComputeFillClip(
        int pixelWidth, int pixelHeight, int cellWidth, int cellHeight)
    {
        // Display area in equivalent pixel dimensions
        var displayPixelW = cellWidth * CellPixelWidth;
        var displayPixelH = cellHeight * CellPixelHeight;

        var displayRatio = displayPixelW / displayPixelH;
        var sourceRatio = (double)pixelWidth / pixelHeight;

        int clipX, clipY, clipW, clipH;

        if (sourceRatio > displayRatio)
        {
            // Source is wider → crop width, show full height
            clipH = pixelHeight;
            clipW = Math.Max(1, (int)Math.Round(pixelHeight * displayRatio));
            clipX = (pixelWidth - clipW) / 2;
            clipY = 0;
        }
        else if (sourceRatio < displayRatio)
        {
            // Source is taller → crop height, show full width
            clipW = pixelWidth;
            clipH = Math.Max(1, (int)Math.Round(pixelWidth / displayRatio));
            clipX = 0;
            clipY = (pixelHeight - clipH) / 2;
        }
        else
        {
            // Aspect ratios match — no crop needed
            clipX = 0;
            clipY = 0;
            clipW = 0;
            clipH = 0;
        }

        return (clipX, clipY, clipW, clipH);
    }

    private void RenderFallback(Hex1bRenderContext context)
    {
        if (Fallback != null)
        {
            context.RenderChild(Fallback);
        }
        else
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write("[KGP not supported]");
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Fallback != null) yield return Fallback;
    }
}
