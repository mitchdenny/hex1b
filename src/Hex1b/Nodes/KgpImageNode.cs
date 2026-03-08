using System.Security.Cryptography;
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
    /// The fallback node to render if KGP is not supported.
    /// </summary>
    public Hex1bNode? Fallback { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        var fallbackSize = Fallback?.Measure(constraints) ?? Size.Zero;

        // Compute cell dimensions from pixel dimensions using CellMetrics
        var cellWidth = RequestedWidth ?? Math.Max(1, (PixelWidth + 9) / 10);  // Default 10px per cell
        var cellHeight = RequestedHeight ?? Math.Max(1, (PixelHeight + 19) / 20); // Default 20px per cell
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

        // Build transmit + placement command
        var base64 = Convert.ToBase64String(ImageData);
        var contentHash = SHA256.HashData(ImageData);
        var imageId = (uint)(contentHash[0] << 24 | contentHash[1] << 16 | contentHash[2] << 8 | contentHash[3]);
        var zIndex = ZOrder == KgpZOrder.AboveText ? 1 : -1;

        var cellWidth = RequestedWidth ?? Math.Max(1, (PixelWidth + 9) / 10);
        var cellHeight = RequestedHeight ?? Math.Max(1, (PixelHeight + 19) / 20);

        // Transmit image data
        var transmit = $"\x1b_Ga=t,f=32,s={PixelWidth},v={PixelHeight},i={imageId},t=d,q=2;{base64}\x1b\\";
        context.Write(transmit);

        // Place the image
        var placement = $"\x1b_Ga=p,i={imageId},c={cellWidth},r={cellHeight},C=1,q=2,z={zIndex}\x1b\\";
        context.Write(placement);
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
