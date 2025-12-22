using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Nodes;

/// <summary>
/// A node that draws a box border around its child content.
/// Border is not focusable - focus passes through to the child.
/// </summary>
public sealed class BorderNode : Hex1bNode
{
    public Hex1bNode? Child { get; set; }
    
    private string? _title;
    public string? Title 
    { 
        get => _title; 
        set
        {
            if (_title != value)
            {
                _title = value;
                MarkDirty();
            }
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // Border adds 2 to width (left + right border) and 2 to height (top + bottom border)
        var childConstraints = new Constraints(
            Math.Max(0, constraints.MinWidth - 2),
            Math.Max(0, constraints.MaxWidth - 2),
            Math.Max(0, constraints.MinHeight - 2),
            Math.Max(0, constraints.MaxHeight - 2)
        );

        var childSize = Child?.Measure(childConstraints) ?? Size.Zero;
        
        return constraints.Constrain(new Size(childSize.Width + 2, childSize.Height + 2));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        // Child gets the inner area (minus borders)
        if (Child != null)
        {
            var innerBounds = new Rect(
                bounds.X + 1,
                bounds.Y + 1,
                Math.Max(0, bounds.Width - 2),
                Math.Max(0, bounds.Height - 2)
            );
            Child.Arrange(innerBounds);
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var borderColor = theme.Get(BorderTheme.BorderColor);
        var titleColor = theme.Get(BorderTheme.TitleColor);
        var topLeft = theme.Get(BorderTheme.TopLeftCorner);
        var topRight = theme.Get(BorderTheme.TopRightCorner);
        var bottomLeft = theme.Get(BorderTheme.BottomLeftCorner);
        var bottomRight = theme.Get(BorderTheme.BottomRightCorner);
        var horizontal = theme.Get(BorderTheme.HorizontalLine);
        var vertical = theme.Get(BorderTheme.VerticalLine);

        var x = Bounds.X;
        var y = Bounds.Y;
        var width = Bounds.Width;
        var height = Bounds.Height;

        // Apply border color with inherited background
        var inheritedBg = context.InheritedBackground.IsDefault ? "" : context.InheritedBackground.ToBackgroundAnsi();
        var colorCode = $"{inheritedBg}{borderColor.ToForegroundAnsi()}";
        var resetToInherited = context.GetResetToInheritedCodes();
        
        var innerWidth = Math.Max(0, width - 2);

        // Build and render top border with optional title
        string topLine;
        if (!string.IsNullOrEmpty(Title) && innerWidth > 2)
        {
            var titleToShow = Title.Length > innerWidth - 2 ? Title[..(innerWidth - 2)] : Title;
            var leftPadding = (innerWidth - titleToShow.Length) / 2;
            var rightPadding = innerWidth - titleToShow.Length - leftPadding;
            
            topLine = $"{colorCode}{topLeft}" +
                      new string(horizontal[0], leftPadding) +
                      $"{inheritedBg}{titleColor.ToForegroundAnsi()}{titleToShow}{colorCode}" +
                      new string(horizontal[0], rightPadding) +
                      $"{topRight}{resetToInherited}";
        }
        else
        {
            topLine = $"{colorCode}{topLeft}{new string(horizontal[0], innerWidth)}{topRight}{resetToInherited}";
        }
        WriteLineClipped(context, x, y, topLine);

        // Draw left and right borders for each row
        var leftBorder = $"{colorCode}{vertical}{resetToInherited}";
        var rightBorder = $"{colorCode}{vertical}{resetToInherited}";
        for (int row = 1; row < height - 1; row++)
        {
            WriteLineClipped(context, x, y + row, leftBorder);
            WriteLineClipped(context, x + width - 1, y + row, rightBorder);
        }

        // Draw bottom border
        if (height > 1)
        {
            var bottomLine = $"{colorCode}{bottomLeft}{new string(horizontal[0], innerWidth)}{bottomRight}{resetToInherited}";
            WriteLineClipped(context, x, y + height - 1, bottomLine);
        }

        // Render child content
        Child?.Render(context);
    }

    private static void WriteLineClipped(Hex1bRenderContext context, int x, int y, string text)
    {
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(x, y, text);
        }
        else
        {
            context.SetCursorPosition(x, y);
            context.Write(text);
        }
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }
}
