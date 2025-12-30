using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that renders a one-line info bar with sections.
/// </summary>
public sealed class InfoBarNode : Hex1bNode
{
    private IReadOnlyList<InfoBarSection> _sections = [];
    /// <summary>
    /// The sections to display in the info bar.
    /// </summary>
    public IReadOnlyList<InfoBarSection> Sections 
    { 
        get => _sections; 
        set 
        {
            if (!SectionsEqual(_sections, value))
            {
                _sections = value;
                MarkDirty();
            }
        }
    }
    
    /// <summary>
    /// Compares two section lists for content equality.
    /// This prevents unnecessary dirty marking when the same sections are passed as a new list.
    /// </summary>
    private static bool SectionsEqual(IReadOnlyList<InfoBarSection> a, IReadOnlyList<InfoBarSection> b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private bool _invertColors = true;
    /// <summary>
    /// Whether to invert foreground/background colors from the theme.
    /// </summary>
    public bool InvertColors 
    { 
        get => _invertColors; 
        set 
        {
            if (_invertColors != value)
            {
                _invertColors = value;
                MarkDirty();
            }
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // InfoBar is always 1 line tall
        // Width is the sum of all section text lengths, or fills available width
        var contentWidth = Sections.Sum(s => s.Text.Length);
        var width = Math.Max(contentWidth, constraints.MaxWidth == int.MaxValue ? contentWidth : constraints.MaxWidth);
        return constraints.Constrain(new Size(width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        // Get base colors from theme
        var baseForeground = theme.Get(InfoBarTheme.ForegroundColor);
        var baseBackground = theme.Get(InfoBarTheme.BackgroundColor);

        // If colors are default, fall back to global theme colors for inversion
        if (baseForeground.IsDefault)
            baseForeground = theme.Get(GlobalTheme.ForegroundColor);
        if (baseBackground.IsDefault)
            baseBackground = theme.Get(GlobalTheme.BackgroundColor);

        // Apply inversion if enabled
        Hex1bColor foreground, background;
        if (InvertColors)
        {
            // Invert: swap foreground and background
            // If both are still default, use white on black as a sensible default
            foreground = baseBackground.IsDefault ? Hex1bColor.Black : baseBackground;
            background = baseForeground.IsDefault ? Hex1bColor.White : baseForeground;
        }
        else
        {
            foreground = baseForeground;
            background = baseBackground;
        }

        // Fill the entire bar with background color
        var fillLine = new string(' ', Bounds.Width);
        var bgCode = background.IsDefault ? "" : background.ToBackgroundAnsi();
        var fgCode = foreground.IsDefault ? "" : foreground.ToForegroundAnsi();
        var resetCode = "\x1b[0m";

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, $"{bgCode}{fgCode}{fillLine}{resetCode}");
        }
        else
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write($"{bgCode}{fgCode}{fillLine}{resetCode}");
        }

        // Render each section
        var currentX = Bounds.X;
        foreach (var section in Sections)
        {
            if (currentX >= Bounds.X + Bounds.Width)
                break;

            // Use section-specific colors if provided, otherwise use the bar's colors
            var sectionFg = section.Foreground ?? foreground;
            var sectionBg = section.Background ?? background;

            var sectionFgCode = sectionFg.IsDefault ? "" : sectionFg.ToForegroundAnsi();
            var sectionBgCode = sectionBg.IsDefault ? "" : sectionBg.ToBackgroundAnsi();

            // Truncate text if it would exceed bounds
            var availableWidth = Bounds.X + Bounds.Width - currentX;
            var text = section.Text.Length > availableWidth
                ? section.Text.Substring(0, availableWidth)
                : section.Text;

            var output = $"{sectionBgCode}{sectionFgCode}{text}{resetCode}";

            if (context.CurrentLayoutProvider != null)
            {
                context.WriteClipped(currentX, Bounds.Y, output);
            }
            else
            {
                context.SetCursorPosition(currentX, Bounds.Y);
                context.Write(output);
            }

            currentX += section.Text.Length;
        }
    }
}
