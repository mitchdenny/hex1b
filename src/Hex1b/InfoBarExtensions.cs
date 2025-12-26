using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="InfoBarWidget"/> instances using the fluent API.
/// </summary>
public static class InfoBarExtensions
{
    /// <summary>
    /// Creates an InfoBar widget with the specified sections.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="sections">The sections to display in the info bar.</param>
    /// <param name="invertColors">
    /// Whether to invert foreground/background colors from the theme (default: true).
    /// When true, creates a visually distinct bar by swapping colors.
    /// </param>
    /// <returns>A new <see cref="InfoBarWidget"/> instance.</returns>
    /// <example>
    /// <code>
    /// ctx.InfoBar([
    ///     new InfoBarSection("Status: Ready"),
    ///     new InfoBarSection(" | "),
    ///     new InfoBarSection("Ln 1, Col 1")
    /// ])
    /// </code>
    /// </example>
    public static InfoBarWidget InfoBar<TParent>(
        this WidgetContext<TParent> ctx,
        IReadOnlyList<InfoBarSection> sections,
        bool invertColors = true)
        where TParent : Hex1bWidget
    {
        return new InfoBarWidget(sections, invertColors);
    }

    /// <summary>
    /// Creates an InfoBar widget with text sections. Each string becomes a separate section
    /// with default colors. This overload is ideal for displaying keyboard shortcuts or
    /// alternating labels and values.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="texts">The text strings to display as sections.</param>
    /// <returns>A new <see cref="InfoBarWidget"/> instance with default inverted colors.</returns>
    /// <example>
    /// <para>Create a status bar with keyboard shortcuts:</para>
    /// <code>
    /// ctx.InfoBar([
    ///     "F1", "Help",
    ///     "Ctrl+S", "Save",
    ///     "Ctrl+Q", "Quit"
    /// ])
    /// </code>
    /// </example>
    public static InfoBarWidget InfoBar<TParent>(
        this WidgetContext<TParent> ctx,
        params string[] texts)
        where TParent : Hex1bWidget
    {
        var sections = texts.Select(t => new InfoBarSection(t)).ToList();
        return new InfoBarWidget(sections);
    }

    /// <summary>
    /// Creates an InfoBar widget with a single text section. This is the simplest way to create
    /// a status bar with a single message.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="text">The text to display.</param>
    /// <param name="invertColors">
    /// Whether to invert foreground/background colors from the theme (default: true).
    /// </param>
    /// <returns>A new <see cref="InfoBarWidget"/> instance.</returns>
    /// <example>
    /// <para>Create a simple status message:</para>
    /// <code>
    /// ctx.InfoBar("Ready")
    /// </code>
    /// </example>
    public static InfoBarWidget InfoBar<TParent>(
        this WidgetContext<TParent> ctx,
        string text,
        bool invertColors = true)
        where TParent : Hex1bWidget
    {
        return new InfoBarWidget([new InfoBarSection(text)], invertColors);
    }

    /// <summary>
    /// Creates an <see cref="InfoBarSection"/> with custom styling. Use this helper method
    /// when you need to specify custom foreground or background colors for a section.
    /// </summary>
    /// <param name="text">The text content of the section.</param>
    /// <param name="foreground">Optional foreground (text) color.</param>
    /// <param name="background">Optional background color.</param>
    /// <returns>A new <see cref="InfoBarSection"/> instance.</returns>
    /// <example>
    /// <para>Create an error indicator with custom colors:</para>
    /// <code>
    /// using static Hex1b.InfoBarExtensions;
    /// 
    /// ctx.InfoBar([
    ///     Section("Status: OK"),
    ///     Section(" | "),
    ///     Section("ERROR", Hex1bColor.Red, Hex1bColor.Yellow)
    /// ])
    /// </code>
    /// </example>
    public static InfoBarSection Section(
        string text,
        Hex1bColor? foreground = null,
        Hex1bColor? background = null)
    {
        return new InfoBarSection(text, foreground, background);
    }
}
