using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that draws a horizontal or vertical separator line.
/// The orientation is either explicitly set or inferred from the parent layout axis.
/// Customize appearance using <see cref="Theming.SeparatorTheme"/> via ThemePanel.
/// </summary>
public sealed class SeparatorNode : Hex1bNode
{
    private LayoutAxis? _explicitAxis;
    /// <summary>
    /// Optional explicit axis. If set, overrides the inferred axis.
    /// </summary>
    public LayoutAxis? ExplicitAxis 
    { 
        get => _explicitAxis; 
        set 
        {
            if (_explicitAxis != value)
            {
                _explicitAxis = value;
                MarkDirty();
            }
        }
    }
    
    private LayoutAxis? _inferredAxis;
    /// <summary>
    /// The axis inferred from the parent container during reconciliation.
    /// </summary>
    public LayoutAxis? InferredAxis 
    { 
        get => _inferredAxis; 
        set 
        {
            if (_inferredAxis != value)
            {
                _inferredAxis = value;
                MarkDirty();
            }
        }
    }
    
    /// <summary>
    /// The effective axis (explicit or inferred, defaulting to Vertical which produces horizontal lines).
    /// </summary>
    private LayoutAxis EffectiveAxis => ExplicitAxis ?? InferredAxis ?? LayoutAxis.Vertical;
    
    /// <summary>
    /// Whether this separator draws horizontally (when in VStack) or vertically (when in HStack).
    /// </summary>
    private bool IsHorizontal => EffectiveAxis == LayoutAxis.Vertical;

    protected override Size MeasureCore(Constraints constraints)
    {
        if (IsHorizontal)
        {
            // Horizontal line: take full width, 1 character tall
            var width = constraints.MaxWidth < int.MaxValue ? constraints.MaxWidth : 1;
            return new Size(width, 1);
        }
        else
        {
            // Vertical line: 1 character wide, take full height
            var height = constraints.MaxHeight < int.MaxValue ? constraints.MaxHeight : 1;
            return new Size(1, height);
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        // Get color: use separator theme color, fall back to global foreground
        var separatorColor = theme.Get(SeparatorTheme.Color);
        var fg = separatorColor.IsDefault ? theme.GetGlobalForeground() : separatorColor;
        var fgAnsi = fg.IsDefault ? "" : fg.ToForegroundAnsi();
        var reset = fg.IsDefault ? "" : theme.GetResetToGlobalCodes();
        
        // Get characters from theme
        var horizontalChar = theme.Get(SeparatorTheme.HorizontalChar)[0];
        var verticalChar = theme.Get(SeparatorTheme.VerticalChar)[0];
        
        if (IsHorizontal)
        {
            // Draw horizontal line
            var line = new string(horizontalChar, Bounds.Width);
            context.WriteClipped(Bounds.X, Bounds.Y, $"{fgAnsi}{line}{reset}");
        }
        else
        {
            // Draw vertical line
            for (int y = 0; y < Bounds.Height; y++)
            {
                context.WriteClipped(Bounds.X, Bounds.Y + y, $"{fgAnsi}{verticalChar}{reset}");
            }
        }
    }
}
