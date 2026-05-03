namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating and configuring <see cref="FigletTextWidget"/> instances using
/// the fluent widget-builder API.
/// </summary>
/// <remarks>
/// <para>
/// These methods follow the noun/verb naming convention used elsewhere in Hex1b
/// (compare <c>TextExtensions.Truncate()</c>, <c>.Wrap()</c>, <c>.Ellipsis()</c>) — there is no
/// <c>With*</c> prefix.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ctx.FigletText("Hello, World")
///    .Font(FigletFonts.Slant)
///    .Layout(FigletLayoutMode.Smushed)
///    .HorizontalOverflow(FigletHorizontalOverflow.Wrap);
/// </code>
/// </example>
public static class FigletTextExtensions
{
    /// <summary>
    /// Creates a <see cref="FigletTextWidget"/> with the specified text content.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="text">The text to render.</param>
    /// <returns>A new <see cref="FigletTextWidget"/> using <see cref="FigletFonts.Standard"/>.</returns>
    public static FigletTextWidget FigletText<TParent>(
        this WidgetContext<TParent> ctx,
        string text)
        where TParent : Hex1bWidget
        => new(text);

    /// <summary>Sets the font used to render the FIGlet text.</summary>
    /// <param name="widget">The widget to configure.</param>
    /// <param name="font">The font.</param>
    /// <returns>A new widget instance.</returns>
    public static FigletTextWidget Font(this FigletTextWidget widget, FigletFont font)
        => widget with { Font = font };

    /// <summary>Sets the horizontal layout mode.</summary>
    public static FigletTextWidget Horizontal(this FigletTextWidget widget, FigletLayoutMode mode)
        => widget with { HorizontalLayout = mode };

    /// <summary>Sets the vertical layout mode.</summary>
    public static FigletTextWidget Vertical(this FigletTextWidget widget, FigletLayoutMode mode)
        => widget with { VerticalLayout = mode };

    /// <summary>
    /// Sets BOTH the horizontal and vertical layout modes to <paramref name="mode"/>. Convenience
    /// for the common case where the same mode applies on both axes.
    /// </summary>
    public static FigletTextWidget Layout(this FigletTextWidget widget, FigletLayoutMode mode)
        => widget with { HorizontalLayout = mode, VerticalLayout = mode };

    /// <summary>Sets how horizontal overflow is handled.</summary>
    public static FigletTextWidget HorizontalOverflow(this FigletTextWidget widget, FigletHorizontalOverflow overflow)
        => widget with { HorizontalOverflow = overflow };

    /// <summary>Sets how vertical overflow is handled.</summary>
    public static FigletTextWidget VerticalOverflow(this FigletTextWidget widget, FigletVerticalOverflow overflow)
        => widget with { VerticalOverflow = overflow };
}
