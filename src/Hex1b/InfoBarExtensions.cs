using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building InfoBar widgets.
/// </summary>
public static class InfoBarExtensions
{
    /// <summary>
    /// Creates an InfoBar widget using a builder pattern.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="builder">A function that builds the info bar children using an InfoBarContext.</param>
    /// <param name="invertColors">Whether to invert foreground/background colors (default: true).</param>
    /// <returns>An InfoBarWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.InfoBar(s => [
    ///     s.Section("NORMAL"),
    ///     s.Section("file.cs"),
    ///     s.Section("Ln 42, Col 15")
    /// ]).WithDefaultSeparator(" | ")
    /// </code>
    /// </example>
    public static InfoBarWidget InfoBar<TParent>(
        this WidgetContext<TParent> ctx,
        Func<InfoBarContext, IEnumerable<IInfoBarChild>> builder,
        bool invertColors = true)
        where TParent : Hex1bWidget
    {
        var infoBarContext = new InfoBarContext();
        var children = builder(infoBarContext).ToList();
        return new InfoBarWidget(children, invertColors);
    }

    /// <summary>
    /// Creates an InfoBar widget with legacy InfoBarSection objects.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="sections">The sections to display.</param>
    /// <param name="invertColors">Whether to invert foreground/background colors (default: true).</param>
    public static InfoBarWidget InfoBar<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<InfoBarSection> sections,
        bool invertColors = true)
        where TParent : Hex1bWidget
    {
        // Convert legacy InfoBarSection to new InfoBarSectionWidget
        var children = sections.Select(s => 
            new InfoBarSectionWidget(new TextBlockWidget(s.Text), s.Foreground, s.Background) as IInfoBarChild
        ).ToList();
        return new InfoBarWidget(children, invertColors);
    }

    /// <summary>
    /// Creates a simple InfoBar widget with text sections.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="texts">The text strings to display as sections.</param>
    public static InfoBarWidget InfoBar<TParent>(
        this WidgetContext<TParent> ctx,
        params string[] texts)
        where TParent : Hex1bWidget
    {
        var infoBarContext = new InfoBarContext();
        var children = texts.Select(t => infoBarContext.Section(t)).ToList<IInfoBarChild>();
        return new InfoBarWidget(children);
    }

    /// <summary>
    /// Creates an InfoBar widget with a single text section.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="text">The text to display.</param>
    /// <param name="invertColors">Whether to invert foreground/background colors (default: true).</param>
    public static InfoBarWidget InfoBar<TParent>(
        this WidgetContext<TParent> ctx,
        string text,
        bool invertColors = true)
        where TParent : Hex1bWidget
    {
        var infoBarContext = new InfoBarContext();
        return new InfoBarWidget([infoBarContext.Section(text)], invertColors);
    }

    /// <summary>
    /// Creates an InfoBar widget with pre-built children.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="children">The info bar children.</param>
    /// <param name="invertColors">Whether to invert foreground/background colors (default: true).</param>
    /// <returns>An InfoBarWidget.</returns>
    public static InfoBarWidget InfoBar<TParent>(
        this WidgetContext<TParent> ctx,
        IEnumerable<IInfoBarChild> children,
        bool invertColors = true)
        where TParent : Hex1bWidget
    {
        return new InfoBarWidget(children.ToList(), invertColors);
    }

    /// <summary>
    /// Creates an InfoBarSection with styling.
    /// </summary>
    /// <param name="text">The text content.</param>
    /// <param name="foreground">Optional foreground color.</param>
    /// <param name="background">Optional background color.</param>
    [Obsolete("Use the builder pattern: ctx.InfoBar(s => [s.Section(...)]) instead.")]
    public static InfoBarSection Section(
        string text,
        Hex1bColor? foreground = null,
        Hex1bColor? background = null)
    {
        return new InfoBarSection(text, foreground, background);
    }
}
