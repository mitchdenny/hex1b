using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A horizontal info bar widget, typically placed at the bottom of the screen.
/// Supports sections, dividers, and spacers with flexible layout options.
/// </summary>
/// <param name="Children">The info bar children (sections, dividers, spacers).</param>
/// <param name="InvertColors">Whether to invert foreground/background colors (default: true).</param>
/// <example>
/// <code>
/// // Basic info bar with an automatic divider between consecutive sections
/// ctx.InfoBar(s => [
///     s.Section("NORMAL"),
///     s.Section("file.cs"),
///     s.Section("Ln 42")
/// ]).Divider(" | ")
/// 
/// // Info bar with a spacer to push content right
/// ctx.InfoBar(s => [
///     s.Section("Mode"),
///     s.Spacer(),
///     s.Section("Ready")
/// ])
/// </code>
/// </example>
public sealed record InfoBarWidget(
    IReadOnlyList<IInfoBarChild> Children,
    bool InvertColors = true) : Hex1bWidget
{
    /// <summary>
    /// The default divider to insert between consecutive sections.
    /// When null, no automatic dividers are inserted.
    /// </summary>
    public InfoBarDividerWidget? DefaultDivider { get; init; }

    /// <summary>
    /// Sets the default divider that is automatically inserted between consecutive sections.
    /// </summary>
    /// <param name="character">The divider character(s). Defaults to " | ".</param>
    /// <param name="foreground">Optional foreground color.</param>
    /// <param name="background">Optional background color.</param>
    /// <returns>A new <see cref="InfoBarWidget"/> with the default divider configured.</returns>
    public InfoBarWidget Divider(
        string character = " | ",
        Hex1bColor? foreground = null,
        Hex1bColor? background = null)
    {
        return this with { DefaultDivider = new InfoBarDividerWidget(character, foreground, background) };
    }

    /// <summary>
    /// Gets the effective children with default dividers inserted between consecutive sections.
    /// </summary>
    internal IReadOnlyList<IInfoBarChild> GetEffectiveChildren()
    {
        if (DefaultDivider is null || Children.Count == 0)
        {
            return Children;
        }

        var result = new List<IInfoBarChild>();
        IInfoBarChild? previousChild = null;

        foreach (var child in Children)
        {
            // Insert default divider between consecutive sections
            // (but not before spacers, after spacers, or when an explicit divider is present)
            if (previousChild is InfoBarSectionWidget && child is InfoBarSectionWidget)
            {
                result.Add(DefaultDivider);
            }

            result.Add(child);
            previousChild = child;
        }

        return result;
    }

    /// <summary>
    /// Builds the composed widget tree for this info bar.
    /// InfoBar is syntactic sugar that produces: ThemePanel(HStack([children...])).
    /// </summary>
    internal Hex1bWidget BuildWidgetTree()
    {
        var effectiveChildren = GetEffectiveChildren();
        
        // Build each child into a widget
        var childWidgets = new List<Hex1bWidget>();
        foreach (var child in effectiveChildren)
        {
            var widget = child switch
            {
                InfoBarSectionWidget section => section.Build(),
                InfoBarDividerWidget divider => divider.Build(),
                InfoBarSpacerWidget spacer => spacer.Build(),
                _ => throw new InvalidOperationException($"Unknown info bar child type: {child.GetType().Name}")
            };
            childWidgets.Add(widget);
        }

        // Wrap in HStack (takes full width, content height for 1 row)
        Hex1bWidget content = new HStackWidget(childWidgets) 
        { 
            WidthHint = Layout.SizeHint.Fill,
            HeightHint = Layout.SizeHint.Content 
        };

        // Always wrap in ThemePanel to apply InfoBar colors
        content = new ThemePanelWidget(
            theme =>
            {
                var fg = theme.Get(InfoBarTheme.ForegroundColor);
                var bg = theme.Get(InfoBarTheme.BackgroundColor);
                
                // Fall back to global colors if InfoBar-specific not set
                if (fg.IsDefault)
                    fg = theme.Get(GlobalTheme.ForegroundColor);
                if (bg.IsDefault)
                    bg = theme.Get(GlobalTheme.BackgroundColor);
                
                if (InvertColors)
                {
                    // Invert: make foreground the background and vice versa
                    var invertedFg = bg.IsDefault ? Hex1bColor.Black : bg;
                    var invertedBg = fg.IsDefault ? Hex1bColor.White : fg;
                    fg = invertedFg;
                    bg = invertedBg;
                }
                
                return theme
                    .Set(GlobalTheme.ForegroundColor, fg)
                    .Set(GlobalTheme.BackgroundColor, bg);
            },
            content);

        return content;
    }

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as InfoBarNode ?? new InfoBarNode();
        
        // Build the composed widget tree and reconcile it as the child
        var widgetTree = BuildWidgetTree();
        node.Child = await context.ReconcileChildAsync(node.Child, widgetTree, node);
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(InfoBarNode);
}
