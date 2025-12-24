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
    /// Sixel support status: null = not yet determined, true = supported, false = not supported.
    /// </summary>
    private bool? _sixelSupported;

    /// <summary>
    /// Timeout for Sixel support detection (1 second).
    /// </summary>
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Escape sequence to query terminal capabilities (DA1 - Primary Device Attributes).
    /// Response format: ESC [ ? {params} c
    /// If response contains ";4" it indicates Sixel graphics support.
    /// </summary>
    private const string DA1Query = "\x1b[c";

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

    /// <summary>
    /// Global Sixel support status shared across all SixelNodes.
    /// Once determined for one node, all nodes use the same value.
    /// </summary>
    private static bool? _globalSixelSupported;

    /// <summary>
    /// Global query state - only one probe is needed per application.
    /// </summary>
    private static bool _globalQuerySent;
    private static DateTime _globalQueryTime;

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
        // Only return fallback focusables if Sixel is NOT supported (fallback is being shown)
        if (_sixelSupported != true && Fallback != null)
        {
            foreach (var focusable in Fallback.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Use global state if already determined
        if (_globalSixelSupported.HasValue)
        {
            _sixelSupported = _globalSixelSupported;
        }

        // Check for query timeout
        if (_globalQuerySent && !_globalSixelSupported.HasValue)
        {
            var elapsed = DateTime.UtcNow - _globalQueryTime;
            if (elapsed > QueryTimeout)
            {
                // Timed out waiting for response - assume Sixel is not supported
                _globalSixelSupported = false;
                _sixelSupported = false;
            }
        }

        // If support status is still unknown and we haven't queried yet, send the probe
        if (!_sixelSupported.HasValue && !_globalQuerySent)
        {
            SendSixelProbe(context);
            // Show a brief loading state
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write("Checking Sixel support...");
            return;
        }

        // If still waiting for response, show loading
        if (!_sixelSupported.HasValue)
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write("Checking Sixel support...");
            return;
        }

        // Now we know the support status - render appropriately
        if (_sixelSupported == true)
        {
            RenderSixel(context);
        }
        else
        {
            RenderFallback(context);
        }
    }

    private void SendSixelProbe(Hex1bRenderContext context)
    {
        _globalQuerySent = true;
        _globalQueryTime = DateTime.UtcNow;

        // Send the DA1 query to the terminal
        // The response will come back through the input channel
        context.Write(DA1Query);
    }

    /// <summary>
    /// Called when a DA1 response is received from the terminal.
    /// This should be called from input handling when a DA1 response is detected.
    /// </summary>
    /// <param name="response">The DA1 response string from the terminal.</param>
    public static void HandleDA1Response(string response)
    {
        if (_globalSixelSupported.HasValue)
        {
            Console.Error.WriteLine($"[Sixel] HandleDA1Response called but already determined: {_globalSixelSupported}");
            return; // Already determined
        }

        // DA1 response format: ESC [ ? {params} c
        // Parameter 4 indicates Sixel graphics support
        // Examples:
        //   \x1b[?62;4;6;22c - has ;4; so supports Sixel
        //   \x1b[?62;6;22c - no ;4; so no Sixel support
        var hasSixel = response.Contains(";4") || 
                       response.Contains("?4;") || 
                       response.Contains("?4c");
        
        Console.Error.WriteLine($"[Sixel] HandleDA1Response: hasSixel={hasSixel}, response={response.Replace("\x1b", "ESC")}");
        _globalSixelSupported = hasSixel;
    }

    /// <summary>
    /// Force Sixel support to a known value (useful for testing or manual override).
    /// </summary>
    public void SetSixelSupport(bool supported)
    {
        _sixelSupported = supported;
        _globalSixelSupported = supported;
    }

    /// <summary>
    /// Reset Sixel support detection globally to re-probe on next render.
    /// </summary>
    public static void ResetGlobalSixelDetection()
    {
        _globalSixelSupported = null;
        _globalQuerySent = false;
    }

    /// <summary>
    /// Reset this node's Sixel support detection.
    /// Also resets global state to allow re-probing.
    /// </summary>
    public void ResetSixelDetection()
    {
        _sixelSupported = null;
        ResetGlobalSixelDetection();
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
    /// Only returns fallback children when Sixel is not supported (fallback is shown).
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        // Only return fallback as a child when Sixel is NOT supported (fallback is being shown)
        if (_sixelSupported != true && Fallback != null) yield return Fallback;
    }
}
