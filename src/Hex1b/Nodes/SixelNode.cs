using System.Diagnostics.CodeAnalysis;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that renders Sixel graphics if the terminal supports it,
/// otherwise falls back to rendering a fallback node.
/// 
/// Sixel support detection is done by querying the terminal with DA1 (Primary Device Attributes)
/// escape sequence. If the response includes ";4" (graphics capability), Sixel is supported.
/// The query is sent on first render and times out after 1 second.
/// </summary>
[Experimental("HEX1B_SIXEL", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/sixel.md")]
public sealed class SixelNode : Hex1bNode
{
    /// <summary>
    /// The Sixel-encoded image data to render.
    /// </summary>
    private string _imageData = "";
    public string ImageData 
    { 
        get => _imageData; 
        set
        {
            if (_imageData != value)
            {
                _imageData = value;
                // Mark parent dirty to force full re-render of the container
                // This is a sledgehammer fix for Sixel ghost pixels when switching images
                Parent?.MarkDirty();
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// The fallback node to render if Sixel is not supported.
    /// </summary>
    public Hex1bNode? Fallback { get; set; }

    /// <summary>
    /// Requested width in character cells. If null, uses natural image width.
    /// </summary>
    private int? _requestedWidth;
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
    /// Requested height in character cells. If null, uses natural image height.
    /// </summary>
    private int? _requestedHeight;
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
    /// Start of Sixel data (DCS - Device Control String).
    /// Format: DCS q [sixel data] ST
    /// Using minimal DCS header (parameters default to 0 anyway).
    /// </summary>
    private const string SixelStart = "\x1bPq";

    /// <summary>
    /// End of Sixel data (ST - String Terminator).
    /// </summary>
    private const string SixelEnd = "\x1b\\";

    public override Size Measure(Constraints constraints)
    {
        // During measure, we don't know if Sixel is supported yet
        // Measure both and take the larger to ensure we have enough space
        var fallbackSize = Fallback?.Measure(constraints) ?? Size.Zero;
        
        var sixelWidth = RequestedWidth ?? 40;
        var sixelHeight = RequestedHeight ?? 20;
        var sixelSize = constraints.Constrain(new Size(sixelWidth, sixelHeight));
        
        // Return the larger of the two to accommodate either rendering mode
        return new Size(
            Math.Max(fallbackSize.Width, sixelSize.Width),
            Math.Max(fallbackSize.Height, sixelSize.Height));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        Fallback?.Arrange(bounds);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // SixelNode itself isn't focusable, but fallback might be
        // We return fallback focusables always since we don't know capabilities at this point
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
        // Use capabilities from the render context (flows from presentation → terminal → workload → app)
        var sixelSupported = context.Capabilities.SupportsSixel;

        if (sixelSupported)
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
        if (string.IsNullOrEmpty(ImageData))
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write("[No image data]");
            return;
        }

        // Position cursor at the image location
        context.SetCursorPosition(Bounds.X, Bounds.Y);

        // Check if data already has a DCS header
        // Use explicit character comparison instead of StartsWith to avoid escape char issues
        var startsWithEscP = ImageData.Length >= 2 && ImageData[0] == '\x1b' && ImageData[1] == 'P';
        var startsWithDCS = ImageData.Length >= 1 && ImageData[0] == '\x90';
        var hasHeader = startsWithEscP || startsWithDCS;
        
        if (hasHeader)
        {
            // Already has DCS header
            context.Write(ImageData);
        }
        else
        {
            // Wrap in Sixel sequence - write as a single string so parser can detect it
            context.Write($"{SixelStart}{ImageData}{SixelEnd}");
        }
    }

    private void RenderFallback(Hex1bRenderContext context)
    {
        if (Fallback != null)
        {
            Fallback.Render(context);
        }
        else
        {
            // No fallback provided - show a placeholder
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write("[Sixel not supported]");
        }
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// Always returns fallback as a potential child - actual rendering depends on capabilities.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Fallback != null) yield return Fallback;
    }
}
