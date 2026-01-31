using Hex1b.Layout;

namespace Hex1b.Widgets;

/// <summary>
/// A flexible spacer that expands to fill available horizontal space.
/// Use spacers to push sections apart within an info bar.
/// </summary>
/// <example>
/// <code>
/// // Push "Ln 42" to the right edge
/// ctx.InfoBar(s => [
///     s.Section("NORMAL"),
///     s.Section("file.cs"),
///     s.Spacer(),  // Expands to fill space
///     s.Section("Ln 42")
/// ])
/// </code>
/// </example>
public sealed record InfoBarSpacerWidget() : IInfoBarChild
{
    /// <summary>
    /// Builds the widget tree for this spacer.
    /// </summary>
    internal Hex1bWidget Build()
    {
        // A spacer is just an empty text block with FillWidth
        return new TextBlockWidget("") { WidthHint = SizeHint.Fill };
    }
}
