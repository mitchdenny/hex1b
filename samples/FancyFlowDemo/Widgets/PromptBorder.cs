using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace FancyFlowDemo.Widgets;

/// <summary>
/// Shared helper that wraps any prompt's interactive content in a rounded
/// yellow border. Used by every step so the four prompts share a consistent
/// "input frame" visual language.
/// </summary>
internal static class PromptBorder
{
    /// <summary>
    /// The accent yellow used for the active marker (◆) in
    /// <see cref="TemplatePromptWidget"/>. Re-used here so the input frame
    /// reads as part of the same accent family.
    /// </summary>
    public static readonly Hex1bColor Accent = Hex1bColor.FromRgb(255, 215, 100);

    /// <summary>
    /// Wraps <paramref name="child"/> in a rounded yellow border constrained
    /// to roughly one third of the available width (left-aligned). The right
    /// two thirds of the row are intentionally empty so each prompt reads as
    /// a compact "card" against the gutter.
    /// </summary>
    public static Hex1bWidget Wrap<TParent>(WidgetContext<TParent> context, Hex1bWidget child)
        where TParent : Hex1bWidget
    {
        var bordered = context.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, Accent)
                  .Set(BorderTheme.TopLeftCorner, "╭")
                  .Set(BorderTheme.TopRightCorner, "╮")
                  .Set(BorderTheme.BottomLeftCorner, "╰")
                  .Set(BorderTheme.BottomRightCorner, "╯"),
            context.Border(child));

        return context.HStack(h =>
        [
            bordered.FillWidth(1),
            h.Text("").FillWidth(2),
        ]);
    }
}
