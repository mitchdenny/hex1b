using Hex1b.Layout;
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
                _text = value!;
                // Invalidate cached wrapped lines - they need to be recomputed
                _wrappedLines = null;
                _lastWrapWidth = -1;
                _multilineMaxLineWidth = -1;
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
    /// Cached max display width across all lines. Invalidated when Text changes.
    /// This avoids per-frame LINQ Max() + DisplayWidth.GetStringWidth() over every line,
    /// which was a significant allocation source (~0.6KB/node/frame from Split + ToList + LINQ).
    /// </summary>
    private int _multilineMaxLineWidth = -1;

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
    protected override Size MeasureCore(Constraints constraints)
    {
        switch (Overflow)
        {
            case TextOverflow.Wrap:
                return MeasureWrapped(constraints);
                
            case TextOverflow.Ellipsis:
            case TextOverflow.Truncate:
            default:
                // Support multi-line text (split by newlines)
                return MeasureMultiline(constraints);
        }
    }

    private Size MeasureMultiline(Constraints constraints)
    {
        // PERF: Measure is called every frame (even with render caching, layout still runs).
        // Caching the split lines and max width avoids per-frame Split('\n').ToList() and
        // LINQ .Max() allocations. The cache is invalidated when the Text property changes.
        //
        // PITFALL: _lastWrapWidth != -1 detects if a prior MeasureWrapped call populated
        // _wrappedLines with width-wrapped results — we must re-split for unwrapped multiline.
        if (_wrappedLines == null || _lastWrapWidth != -1 || _multilineMaxLineWidth < 0)
        {
            _wrappedLines = Text.IndexOf('\n') < 0
                ? new List<string>(1) { Text }
                : Text.Split('\n').ToList();

            _lastWrapWidth = -1; // Not width-based wrapping
            _multilineMaxLineWidth = 0;
            for (var i = 0; i < _wrappedLines.Count; i++)
            {
                _multilineMaxLineWidth = Math.Max(_multilineMaxLineWidth, DisplayWidth.GetStringWidth(_wrappedLines[i]));
            }
        }

        var maxLineWidth = _multilineMaxLineWidth;
        var width = Overflow == TextOverflow.Ellipsis 
            ? Math.Min(maxLineWidth, constraints.MaxWidth)
            : maxLineWidth;
             
        return constraints.Constrain(new Size(width, _wrappedLines.Count));
    }

    private Size MeasureWrapped(Constraints constraints)
    {
        var maxWidth = constraints.MaxWidth;
        
        // If unbounded or very large, just split by newlines (no word wrapping)
        if (maxWidth == int.MaxValue || maxWidth <= 0)
        {
            if (_wrappedLines == null || _lastWrapWidth != maxWidth)
            {
                _wrappedLines = Text.IndexOf('\n') < 0
                    ? new List<string>(1) { Text }
                    : Text.Split('\n').ToList();
                _lastWrapWidth = maxWidth;
            }

            var maxLineWidth = 0;
            for (var i = 0; i < _wrappedLines.Count; i++)
            {
                maxLineWidth = Math.Max(maxLineWidth, DisplayWidth.GetStringWidth(_wrappedLines[i]));
            }

            return constraints.Constrain(new Size(maxLineWidth, _wrappedLines.Count));
        }
        
        // Only re-wrap if width changed
        if (_wrappedLines == null || _lastWrapWidth != maxWidth)
        {
            _wrappedLines = WrapText(Text, maxWidth);
            _lastWrapWidth = maxWidth;
        }
        
        var width = 0;
        for (var i = 0; i < _wrappedLines.Count; i++)
        {
            width = Math.Max(width, DisplayWidth.GetStringWidth(_wrappedLines[i]));
        }
        var height = _wrappedLines.Count;
        
        return constraints.Constrain(new Size(width, height));
    }

    /// <summary>
    /// Wraps text to fit within the specified width (in display columns).
    /// Uses word boundaries when possible, breaks words only when necessary.
    /// Respects embedded newline characters as explicit line breaks.
    /// </summary>
    private static List<string> WrapText(string text, int maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            return [""];
        
        var lines = new List<string>();
        
        // First, split by explicit newlines
        var paragraphs = text.Split('\n');
        
        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                lines.Add("");
                continue;
            }
            
            // Wrap each paragraph independently
            var words = paragraph.Split(' ');
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
            
            // Add the last line of this paragraph
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
        
        // If a parent container has set an ambient background, include it so that
        // characters written by this node preserve the parent's background color
        // instead of resetting to terminal default. Append after global codes so
        // the ambient bg overrides any global background.
        if (!context.AmbientBackground.IsDefault)
        {
            var ambientBgCode = context.AmbientBackground.ToBackgroundAnsi();
            colorCodes += ambientBgCode;
            resetCodes = (string.IsNullOrEmpty(resetCodes) ? "\x1b[0m" : resetCodes) + ambientBgCode;
        }
        
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
        // If _wrappedLines wasn't computed (Measure wasn't called), fallback to direct rendering
        var lines = _wrappedLines ?? [Text];
        if (lines.Count == 0)
            return;
        
        // Determine how many lines we can render
        var maxLines = Bounds.Height > 0 ? Bounds.Height : lines.Count;
        
        for (int i = 0; i < lines.Count && i < maxLines; i++)
        {
            var line = lines[i];
            var y = Bounds.Y + i;
            
            if (context.CurrentLayoutProvider != null)
            {
                if (!string.IsNullOrEmpty(colorCodes))
                {
                    context.WriteClipped(Bounds.X, y, $"{colorCodes}{line}{resetCodes}");
                }
                else
                {
                    context.WriteClipped(Bounds.X, y, line);
                }
            }
            else
            {
                // No layout provider - write at current cursor position
                if (i == 0)
                {
                    // First line uses current cursor position for backward compatibility
                    if (!string.IsNullOrEmpty(colorCodes))
                    {
                        context.Write($"{colorCodes}{line}{resetCodes}");
                    }
                    else
                    {
                        context.Write(line);
                    }
                }
                else
                {
                    // Subsequent lines need explicit positioning
                    context.SetCursorPosition(Bounds.X, y);
                    if (!string.IsNullOrEmpty(colorCodes))
                    {
                        context.Write($"{colorCodes}{line}{resetCodes}");
                    }
                    else
                    {
                        context.Write(line);
                    }
                }
            }
        }
    }

    private void RenderWrapped(Hex1bRenderContext context, string colorCodes, string resetCodes)
    {
        // If _wrappedLines wasn't computed (Measure wasn't called), fallback to Text
        var lines = _wrappedLines ?? [Text];
        if (lines.Count == 0)
            return;
        
        var maxLines = Bounds.Height > 0 ? Bounds.Height : lines.Count;
            
        for (int i = 0; i < lines.Count && i < maxLines; i++)
        {
            var line = lines[i];
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
        // If _wrappedLines wasn't computed (Measure wasn't called), fallback to Text
        var lines = _wrappedLines ?? [Text];
        if (lines.Count == 0)
            return;
        
        var maxLines = Bounds.Height > 0 ? Bounds.Height : lines.Count;
        
        for (int i = 0; i < lines.Count && i < maxLines; i++)
        {
            var line = lines[i];
            var lineWidth = DisplayWidth.GetStringWidth(line);
            var y = Bounds.Y + i;
            
            // Apply ellipsis truncation per line
            if (lineWidth > Bounds.Width && Bounds.Width > 3)
            {
                var (sliced, _) = SliceByWidth(line, Bounds.Width - 3);
                line = sliced + "...";
            }
            else if (lineWidth > Bounds.Width)
            {
                var (sliced, _) = SliceByWidth(line, Bounds.Width);
                line = sliced;
            }
            
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
}
