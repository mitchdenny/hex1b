using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Node that renders a hyperlink using OSC 8 escape sequences.
/// In terminals that support OSC 8, the text becomes clickable.
/// </summary>
public sealed class HyperlinkNode : Hex1bNode
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

    private string _uri = "";
    public string Uri 
    { 
        get => _uri; 
        set 
        {
            if (_uri != value)
            {
                _uri = value;
                MarkDirty();
            }
        }
    }

    private string _parameters = "";
    public string Parameters 
    { 
        get => _parameters; 
        set 
        {
            if (_parameters != value)
            {
                _parameters = value;
                MarkDirty();
            }
        }
    }

    private TextOverflow _overflow = TextOverflow.Truncate;
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
    /// The source widget that was reconciled into this node.
    /// Used to create typed event args.
    /// </summary>
    public HyperlinkWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// The async action to execute when the hyperlink is activated.
    /// This is the wrapped handler that receives InputBindingActionContext.
    /// </summary>
    public Func<InputBindingActionContext, Task>? ClickAction { get; set; }
    
    private bool _isFocused;
    public override bool IsFocused 
    { 
        get => _isFocused; 
        set 
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    public override bool IsHovered 
    { 
        get => _isHovered; 
        set 
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Only register activation bindings if there's an action to perform
        if (ClickAction != null)
        {
            // Enter triggers the link
            bindings.Key(Hex1bKey.Enter).Action(ClickAction, "Open link");
            
            // Left click activates the link
            bindings.Mouse(MouseButton.Left).Action(ClickAction, "Click link");
        }
    }

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
                // Hyperlink renders as just the text (OSC 8 sequences are invisible)
                var width = DisplayWidth.GetStringWidth(Text);
                return constraints.Constrain(new Size(width, 1));
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
        switch (Overflow)
        {
            case TextOverflow.Wrap:
                RenderWrapped(context);
                break;
                
            case TextOverflow.Ellipsis:
                RenderEllipsis(context);
                break;
                
            case TextOverflow.Truncate:
            default:
                RenderTruncate(context);
                break;
        }
    }

    private void RenderTruncate(Hex1bRenderContext context)
    {
        var output = BuildStyledOutput(Text, context);
        
        // Use clipped rendering when a layout provider is active
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }

    private void RenderWrapped(Hex1bRenderContext context)
    {
        if (_wrappedLines == null || _wrappedLines.Count == 0)
            return;
            
        for (int i = 0; i < _wrappedLines.Count && i < Bounds.Height; i++)
        {
            var line = _wrappedLines[i];
            var y = Bounds.Y + i;
            
            var output = BuildStyledOutput(line, context);
            context.WriteClipped(Bounds.X, y, output);
        }
    }

    private void RenderEllipsis(Hex1bRenderContext context)
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
        
        var output = BuildStyledOutput(text, context);
        context.WriteClipped(Bounds.X, Bounds.Y, output);
    }

    /// <summary>
    /// Builds the styled output string with OSC 8 sequences for a given text segment.
    /// </summary>
    private string BuildStyledOutput(string text, Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        // OSC 8 format: ESC ] 8 ; params ; URI ST text ESC ] 8 ; ; ST
        var osc8Start = FormatOsc8Start(Uri, Parameters);
        var osc8End = "\x1b]8;;\x1b\\";
        
        // Apply styling based on focus/hover state
        string styledText;
        if (IsFocused)
        {
            // Focused: underline + bright color
            var fg = theme.Get(HyperlinkTheme.FocusedForegroundColor);
            styledText = $"{fg.ToForegroundAnsi()}\x1b[4m{text}\x1b[24m{resetToGlobal}";
        }
        else if (IsHovered)
        {
            // Hovered: underline
            var fg = theme.Get(HyperlinkTheme.HoveredForegroundColor);
            styledText = $"{fg.ToForegroundAnsi()}\x1b[4m{text}\x1b[24m{resetToGlobal}";
        }
        else
        {
            // Normal: link color (typically underlined by terminal for OSC 8 links)
            var fg = theme.Get(HyperlinkTheme.ForegroundColor);
            if (fg.IsDefault)
            {
                // Use blue as default link color
                styledText = $"\x1b[34m{text}{resetToGlobal}";
            }
            else
            {
                styledText = $"{fg.ToForegroundAnsi()}{text}{resetToGlobal}";
            }
        }
        
        // Wrap with OSC 8 sequences
        return $"{osc8Start}{styledText}{osc8End}";
    }

    /// <summary>
    /// Formats the OSC 8 start sequence.
    /// Format: ESC ] 8 ; params ; URI ST
    /// </summary>
    private static string FormatOsc8Start(string uri, string parameters)
    {
        // ESC ] 8 ; params ; URI ESC \
        return $"\x1b]8;{parameters};{uri}\x1b\\";
    }
}
