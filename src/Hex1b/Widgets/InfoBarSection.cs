using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Represents a section within an <see cref="InfoBarWidget"/>, containing text and optional custom colors.
/// </summary>
/// <param name="Text">The text content to display in this section.</param>
/// <param name="Foreground">
/// Optional foreground (text) color override for this section. If null, uses the InfoBar's default foreground color.
/// </param>
/// <param name="Background">
/// Optional background color override for this section. If null, uses the InfoBar's default background color.
/// </param>
/// <remarks>
/// <para>
/// InfoBarSection allows you to create visually distinct segments within a status bar. Common patterns include:
/// <list type="bullet">
/// <item><description>Separators: Use " | " or similar text to visually divide sections</description></item>
/// <item><description>Keyboard shortcuts: Alternate between key names and descriptions</description></item>
/// <item><description>Status indicators: Use custom colors to highlight warnings or errors</description></item>
/// </list>
/// </para>
/// <para>
/// When custom colors are provided, they override the InfoBar's theme colors for that specific section only.
/// This is useful for highlighting important information like errors (red) or warnings (yellow).
/// </para>
/// </remarks>
/// <example>
/// <para>Create sections using the fluent API:</para>
/// <code>
/// // Using the string array shorthand
/// ctx.InfoBar([
///     "F1", "Help",
///     "Ctrl+S", "Save"
/// ])
/// 
/// // Using explicit InfoBarSection instances with custom colors
/// ctx.InfoBar([
///     new InfoBarSection("Normal"),
///     new InfoBarSection(" | "),
///     new InfoBarSection("ERROR", Hex1bColor.Red, Hex1bColor.Yellow)
/// ])
/// </code>
/// </example>
/// <seealso cref="InfoBarWidget"/>
/// <seealso cref="InfoBarExtensions.InfoBar{TParent}(WidgetContext{TParent}, IReadOnlyList{InfoBarSection}, bool)"/>
public sealed record InfoBarSection(
    string Text,
    Hex1bColor? Foreground = null,
    Hex1bColor? Background = null);
