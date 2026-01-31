using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Provides a fluent API context for building info bar structures.
/// This context exposes only info bar-related methods (Section, Separator, Spacer)
/// to guide developers toward the correct API usage.
/// </summary>
public readonly struct InfoBarContext
{
    /// <summary>
    /// Creates a text section with optional styling.
    /// </summary>
    /// <param name="text">The text content to display.</param>
    /// <param name="foreground">Optional foreground color override.</param>
    /// <param name="background">Optional background color override.</param>
    /// <returns>An InfoBarSectionWidget configured with the specified text and colors.</returns>
    public InfoBarSectionWidget Section(
        string text,
        Hex1bColor? foreground = null,
        Hex1bColor? background = null)
    {
        return new InfoBarSectionWidget(new TextBlockWidget(text), foreground, background);
    }

    /// <summary>
    /// Creates a widget section that can contain any widget(s).
    /// </summary>
    /// <param name="builder">A function that builds the section content.</param>
    /// <param name="foreground">Optional foreground color override.</param>
    /// <param name="background">Optional background color override.</param>
    /// <returns>An InfoBarSectionWidget configured with the built widget.</returns>
    public InfoBarSectionWidget Section(
        Func<WidgetContext<HStackWidget>, Hex1bWidget> builder,
        Hex1bColor? foreground = null,
        Hex1bColor? background = null)
    {
        var ctx = new WidgetContext<HStackWidget>();
        var content = builder(ctx);
        return new InfoBarSectionWidget(content, foreground, background);
    }

    /// <summary>
    /// Creates an explicit separator between sections.
    /// When placed between sections, this overrides any default separator.
    /// </summary>
    /// <param name="character">The separator character(s). Defaults to "|" if not specified.</param>
    /// <param name="foreground">Optional foreground color override.</param>
    /// <param name="background">Optional background color override.</param>
    /// <returns>An InfoBarSeparatorWidget.</returns>
    public InfoBarSeparatorWidget Separator(
        string? character = null,
        Hex1bColor? foreground = null,
        Hex1bColor? background = null)
    {
        return new InfoBarSeparatorWidget(character ?? "|", foreground, background);
    }

    /// <summary>
    /// Creates a flexible spacer that pushes adjacent sections apart.
    /// The spacer expands to fill available horizontal space.
    /// </summary>
    /// <returns>An InfoBarSpacerWidget.</returns>
    public InfoBarSpacerWidget Spacer()
    {
        return new InfoBarSpacerWidget();
    }
}
