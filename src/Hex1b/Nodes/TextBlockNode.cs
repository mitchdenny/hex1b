using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class TextBlockNode : Hex1bNode
{
    private string _text = "";
    public string Text 
    { 
        get => _text; 
        set 
        {
            if (_text != value)
            {
                _text = value;
                MarkDirty();
            }
        }
    }
    
    private TextOverflow _overflow = TextOverflow.Overflow;
    public TextOverflow Overflow 
    { 
        get => _overflow; 
        set 
        {
            if (_overflow != value)
            {
                _overflow = value;
                MarkDirty();
            }
        }
    }
    
    /// <summary>
    /// Cached wrapped lines, computed during Measure when Overflow is Wrap.
    /// </summary>
    private List<string>? _wrappedLines;
    
    /// <summary>
    /// The width used to compute wrapped lines. If constraints change, we re-wrap.
    /// </summary>
    private int _lastWrapWidth = -1;

    public override Size Measure(Constraints constraints)
    {
        switch (Overflow)
        {
            case TextOverflow.Wrap:
                return MeasureWrapped(constraints);
                
            case TextOverflow.Ellipsis:
                // Ellipsis: single line, but respects max width
                var textWidth = DisplayWidth.GetStringWidth(Text);
                var ellipsisWidth = Math.Min(textWidth, constraints.MaxWidth);
                return constraints.Constrain(new Size(ellipsisWidth, 1));
                
            case TextOverflow.Overflow:
            default:
                // Original behavior: single-line, width is text display width
                return constraints.Constrain(new Size(DisplayWidth.GetStringWidth(Text), 1));
        }
    }

    private Size MeasureWrapped(Constraints constraints)
    {
        var maxWidth = constraints.MaxWidth;
        
        // If unbounded or very large, treat as single line
        if (maxWidth == int.MaxValue || maxWidth <= 0)
        {
            _wrappedLines = [Text];
            _lastWrapWidth = maxWidth;
            return constraints.Constrain(new Size(DisplayWidth.GetStringWidth(Text), 1));
        }
        
        // Only re-wrap if width changed
        if (_wrappedLines == null || _lastWrapWidth != maxWidth)
        {
            _wrappedLines = WrapText(Text, maxWidth);
            _lastWrapWidth = maxWidth;
        }
        
        var width = _wrappedLines.Count > 0 ? _wrappedLines.Max(l => DisplayWidth.GetStringWidth(l)) : 0;
        var height = _wrappedLines.Count;
        
        return constraints.Constrain(new Size(width, height));
    }

    /// <summary>
    /// Wraps text to fit within the specified width (in display columns).
    /// Uses word boundaries when possible, breaks words only when necessary.
    /// </summary>
    private static List<string> WrapText(string text, int maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            return [""];
            
        var lines = new List<string>();
        var words = text.Split(' ');
        var currentLine = "";
        var currentLineWidth = 0;
        
        foreach (var word in words)
        {
            var wordWidth = DisplayWidth.GetStringWidth(word);
            
            if (wordWidth > maxWidth)
            {
                // Word is wider than max width - must break it
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = "";
                    currentLineWidth = 0;
                }
                
                // Break the word by display width
                var remaining = word;
                while (DisplayWidth.GetStringWidth(remaining) > maxWidth)
                {
                    var (chunk, _) = SliceByWidth(remaining, maxWidth);
                    lines.Add(chunk);
                    remaining = remaining[chunk.Length..];
                }
                
                if (remaining.Length > 0)
                {
                    currentLine = remaining;
                    currentLineWidth = DisplayWidth.GetStringWidth(remaining);
                }
            }
            else if (currentLine.Length == 0)
            {
                currentLine = word;
                currentLineWidth = wordWidth;
            }
            else if (currentLineWidth + 1 + wordWidth <= maxWidth)
            {
                currentLine += " " + word;
                currentLineWidth += 1 + wordWidth;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = word;
                currentLineWidth = wordWidth;
            }
        }
        
        if (currentLine.Length > 0)
        {
            lines.Add(currentLine);
        }
        
        return lines.Count > 0 ? lines : [""];
    }

    /// <summary>
    /// Slices a string to fit within the specified display width.
    /// </summary>
    private static (string text, int width) SliceByWidth(string text, int maxWidth)
    {
        var result = DisplayWidth.SliceByDisplayWidth(text, 0, maxWidth);
        return (result.text, result.columns);
    }

    public override void Render(Hex1bRenderContext context)
    {
        var colorCodes = context.GetInheritedColorCodes();
        var resetCodes = !string.IsNullOrEmpty(colorCodes) ? context.GetResetToInheritedCodes() : "";
        
        switch (Overflow)
        {
            case TextOverflow.Wrap:
                RenderWrapped(context, colorCodes, resetCodes);
                break;
                
            case TextOverflow.Ellipsis:
                RenderEllipsis(context, colorCodes, resetCodes);
                break;
                
            case TextOverflow.Overflow:
            default:
                RenderOverflow(context, colorCodes, resetCodes);
                break;
        }
    }

    private void RenderOverflow(Hex1bRenderContext context, string colorCodes, string resetCodes)
    {
        // When a LayoutProvider is active, use clipped rendering
        // Otherwise, use the original simple behavior for backward compatibility
        if (context.CurrentLayoutProvider != null)
        {
            // Use Bounds for position - parent sets cursor but we need absolute coords for clipping
            if (!string.IsNullOrEmpty(colorCodes))
            {
                context.WriteClipped(Bounds.X, Bounds.Y, $"{colorCodes}{Text}{resetCodes}");
            }
            else
            {
                context.WriteClipped(Bounds.X, Bounds.Y, Text);
            }
        }
        else
        {
            // No layout provider - write at current cursor position (original behavior)
            if (!string.IsNullOrEmpty(colorCodes))
            {
                context.Write($"{colorCodes}{Text}{resetCodes}");
            }
            else
            {
                context.Write(Text);
            }
        }
    }

    private void RenderWrapped(Hex1bRenderContext context, string colorCodes, string resetCodes)
    {
        if (_wrappedLines == null || _wrappedLines.Count == 0)
            return;
            
        for (int i = 0; i < _wrappedLines.Count && i < Bounds.Height; i++)
        {
            var line = _wrappedLines[i];
            var y = Bounds.Y + i;
            
            if (!string.IsNullOrEmpty(colorCodes))
            {
                context.WriteClipped(Bounds.X, y, $"{colorCodes}{line}{resetCodes}");
            }
            else
            {
                context.WriteClipped(Bounds.X, y, line);
            }
        }
    }

    private void RenderEllipsis(Hex1bRenderContext context, string colorCodes, string resetCodes)
    {
        var text = Text;
        var textWidth = DisplayWidth.GetStringWidth(Text);
        
        if (textWidth > Bounds.Width && Bounds.Width > 3)
        {
            // Slice to fit with ellipsis
            var (sliced, _) = SliceByWidth(Text, Bounds.Width - 3);
            text = sliced + "...";
        }
        else if (textWidth > Bounds.Width)
        {
            var (sliced, _) = SliceByWidth(Text, Bounds.Width);
            text = sliced;
        }
        
        if (!string.IsNullOrEmpty(colorCodes))
        {
            context.WriteClipped(Bounds.X, Bounds.Y, $"{colorCodes}{text}{resetCodes}");
        }
        else
        {
            context.WriteClipped(Bounds.X, Bounds.Y, text);
        }
    }
}
