using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that renders the output of an embedded terminal via a presentation adapter.
/// </summary>
/// <remarks>
/// This node displays the screen buffer of an embedded terminal,
/// supporting resizing and clipping like any other widget.
/// </remarks>
public sealed class TerminalNode : Hex1bNode
{
    /// <summary>
    /// The presentation adapter for the embedded terminal.
    /// </summary>
    private Hex1bAppPresentationAdapter? _presentationAdapter;
    public Hex1bAppPresentationAdapter? PresentationAdapter 
    { 
        get => _presentationAdapter; 
        set
        {
            if (_presentationAdapter != value)
            {
                // Unsubscribe from old adapter
                if (_presentationAdapter != null)
                {
                    _presentationAdapter.OutputReceived -= OnOutputReceived;
                }
                
                _presentationAdapter = value;
                
                // Subscribe to new adapter
                if (_presentationAdapter != null)
                {
                    _presentationAdapter.OutputReceived += OnOutputReceived;
                }
                
                MarkDirty();
            }
        }
    }
    
    /// <summary>
    /// Handler for terminal output - marks this node as dirty.
    /// </summary>
    private void OnOutputReceived()
    {
        MarkDirty();
    }

    /// <summary>
    /// Cached terminal output lines for rendering.
    /// </summary>
    private string[]? _cachedLines;
    
    /// <summary>
    /// Last measured size to detect if we need to resize the terminal.
    /// </summary>
    private Size _lastMeasuredSize = Size.Zero;

    public override Size Measure(Constraints constraints)
    {
        if (PresentationAdapter == null)
        {
            return constraints.Constrain(Size.Zero);
        }

        // The terminal has a fixed size based on its width/height
        // We'll constrain it to fit within the available space
        var termWidth = PresentationAdapter.Width;
        var termHeight = PresentationAdapter.Height;
        
        var size = new Size(termWidth, termHeight);
        _lastMeasuredSize = constraints.Constrain(size);
        
        return _lastMeasuredSize;
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        // Resize the terminal if needed to fit the allocated bounds
        if (PresentationAdapter != null && (bounds.Width != PresentationAdapter.Width || bounds.Height != PresentationAdapter.Height))
        {
            PresentationAdapter.Resize(bounds.Width, bounds.Height);
            MarkDirty();
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (PresentationAdapter == null)
        {
            return;
        }

        // Get the terminal's rendered output via the presentation adapter
        var lines = PresentationAdapter.GetRenderedLines();
        _cachedLines = lines;

        // Render each line within bounds, respecting clipping
        var y = Bounds.Y;
        for (int i = 0; i < lines.Length && i < Bounds.Height; i++)
        {
            var line = lines[i];
            
            // Clip the line to the bounds width using display-width-aware slicing
            var displayLine = line;
            var lineWidth = DisplayWidth.GetStringWidth(line);
            if (lineWidth > Bounds.Width)
            {
                var result = DisplayWidth.SliceByDisplayWidth(line, 0, Bounds.Width);
                displayLine = result.text;
            }
            
            // Use WriteClipped to respect layout provider clipping
            context.WriteClipped(Bounds.X, y + i, displayLine);
        }
    }
}
