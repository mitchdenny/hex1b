using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace FancyFlowDemo.Widgets;

/// <summary>
/// Builds the frozen "tombstone" widget that each prompt emits via
/// <c>FlowStep.Complete(...)</c>. Wraps the supplied label in the same
/// 1-cell gutter + purple bar layout as the live prompt frame so the bar
/// runs continuously down the screen as steps complete.
/// </summary>
internal static class TombstoneFactory
{
    /// <summary>
    /// Builds a "bar only" row — used to seed the gutter above the very first
    /// prompt and as the trailing spacer beneath each completed step.
    /// </summary>
    public static Hex1bWidget BuildBarRow(RootContext ctx) =>
        ctx.HStack(h =>
        [
            h.Text(" "),
            h.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, TemplatePromptWidget.BarColor),
                h.Text(TemplatePromptWidget.BarChar)),
        ]);

    /// <summary>
    /// Builds the frozen tombstone for a completed prompt: a hollow-diamond
    /// marker on the gutter line, the supplied label, and a trailing bar-only
    /// row to give consecutive tombstones some breathing room.
    /// </summary>
    public static Hex1bWidget Build(RootContext ctx, string label) =>
        ctx.VStack(v =>
        [
            v.HStack(h =>
            [
                h.Text(" "),
                h.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, TemplatePromptWidget.BarColor),
                    h.Text(TemplatePromptWidget.SubmittedMarker)),
                h.Text("  "),
                h.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, TemplatePromptWidget.BodyColor),
                    h.Text(label)),
            ]),
            v.HStack(h =>
            [
                h.Text(" "),
                h.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, TemplatePromptWidget.BarColor),
                    h.Text(TemplatePromptWidget.BarChar)),
            ]),
        ]);
}
