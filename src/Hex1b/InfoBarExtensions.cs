using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building InfoBar widgets.
/// </summary>
public static class InfoBarExtensions
{
    /// <summary>
    /// Creates an InfoBar widget with the specified sections.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="sections">The sections to display.</param>
    /// <param name="invertColors">Whether to invert foreground/background colors (default: true).</param>
    public static InfoBarWidget InfoBar<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        IReadOnlyList<InfoBarSection> sections,
        bool invertColors = true)
        where TParent : Hex1bWidget
    {
        return new InfoBarWidget(sections, invertColors);
    }

    /// <summary>
    /// Creates an InfoBar widget with text sections.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="texts">The text strings to display as sections.</param>
    public static InfoBarWidget InfoBar<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        params string[] texts)
        where TParent : Hex1bWidget
    {
        var sections = texts.Select(t => new InfoBarSection(t)).ToList();
        return new InfoBarWidget(sections);
    }

    /// <summary>
    /// Creates an InfoBar widget with a single text section.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="text">The text to display.</param>
    /// <param name="invertColors">Whether to invert foreground/background colors (default: true).</param>
    public static InfoBarWidget InfoBar<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        string text,
        bool invertColors = true)
        where TParent : Hex1bWidget
    {
        return new InfoBarWidget([new InfoBarSection(text)], invertColors);
    }

    /// <summary>
    /// Creates an InfoBarSection with styling.
    /// </summary>
    /// <param name="text">The text content.</param>
    /// <param name="foreground">Optional foreground color.</param>
    /// <param name="background">Optional background color.</param>
    public static InfoBarSection Section(
        string text,
        Hex1bColor? foreground = null,
        Hex1bColor? background = null)
    {
        return new InfoBarSection(text, foreground, background);
    }
}
