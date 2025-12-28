using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for displaying text content. Created by reconciling a <see cref="TextBlockWidget"/>.
/// </summary>
/// <remarks>
/// <para>
/// TextBlockNode handles measuring, arranging, and rendering text with support for
/// different overflow behaviors: overflow (clipped by parent), wrapping, and ellipsis truncation.
/// </para>
/// <para>
/// This node is not focusable and does not handle input. For editable text, see <see cref="TextBoxNode"/>.
/// </para>
/// </remarks>
/// <seealso cref="TextBlockWidget"/>
/// <seealso cref="TextOverflow"/>
public sealed class TextBlockNode : Hex1bNode
{
    private string _text = "";
    
    /// <summary>
    /// Gets or sets the text content to display.
    /// </summary>
    /// <remarks>
    /// When this property changes, the node is marked dirty to trigger re-layout and re-render.
    /// The text can contain Unicode characters including wide characters (CJK) and emoji,
    /// which are correctly measured using display width calculations.
    /// </remarks>
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
    
    private TextOverflow _overflow = TextOverflow.Truncate;
    
    /// <summary>
    /// Gets or sets how text handles horizontal overflow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When this property changes, the node is marked dirty to trigger re-layout and re-render.
    /// </para>
    /// <para>
    /// The behavior depends on the <see cref="TextOverflow"/> value:
    /// <list type="bullet">
    /// <item><description><see cref="TextOverflow.Truncate"/>: Text is clipped by parent; no visual indicator shown.</description></item>
    /// <item><description><see cref="TextOverflow.Wrap"/>: Text wraps at word boundaries, increasing measured height.</description></item>
    /// <item><description><see cref="TextOverflow.Ellipsis"/>: Text is truncated with "..." to fit available width.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Measures the size required to display the text within the given constraints.
    /// </summary>
    /// <param name="constraints">The size constraints for layout.</param>
    /// <returns>
    /// The measured size. For <see cref="TextOverflow.Wrap"/> mode, height may be greater than 1
    /// if text wraps to multiple lines.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The measurement behavior depends on the <see cref="Overflow"/> setting:
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="TextOverflow.Truncate"/>: Returns the full text width (constrained to max width) and height of 1.</description></item>
    /// <item><description><see cref="TextOverflow.Wrap"/>: Calculates wrapped lines and returns the width of the widest line and total line count as height.</description></item>
    /// <item><description><see cref="TextOverflow.Ellipsis"/>: Returns the minimum of text width and max width, with height of 1.</description></item>
    /// </list>
    /// </remarks>
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
                
            case TextOverflow.Truncate:
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

    /// <summary>
    /// Renders the text to the terminal using the current render context.
    /// </summary>
    /// <param name="context">The render context providing terminal access and inherited styling.</param>
    /// <remarks>
    /// The rendering behavior depends on the <see cref="Overflow"/> setting and whether
    /// a parent <see cref="Nodes.LayoutNode"/> provides clipping. Inherited colors from
    /// parent nodes are applied automatically.
    /// </remarks>
    public override void Render(Hex1bRenderContext context)
    {
        var colorCodes = context.Theme.GetGlobalColorCodes();
        var resetCodes = !string.IsNullOrEmpty(colorCodes) ? context.Theme.GetResetToGlobalCodes() : "";
        
        switch (Overflow)
        {
            case TextOverflow.Wrap:
                RenderWrapped(context, colorCodes, resetCodes);
                break;
                
            case TextOverflow.Ellipsis:
                RenderEllipsis(context, colorCodes, resetCodes);
                break;
                
            case TextOverflow.Truncate:
            default:
                RenderTruncate(context, colorCodes, resetCodes);
                break;
        }
    }

    private void RenderTruncate(Hex1bRenderContext context, string colorCodes, string resetCodes)
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
