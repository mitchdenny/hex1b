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
    // Outcome markers — filled circles read as "punctuation" against the
    // prompt diamonds, giving the flow a clear start/end shape.
    internal const string HeaderMarker = "●";
    internal const string SuccessMarker = "●";
    internal const string CancelMarker = "●";
    internal const string ErrorMarker = "●";

    internal static readonly Hex1bColor HeaderColor = Hex1bColor.FromRgb(120, 180, 255);   // soft cyan-blue
    internal static readonly Hex1bColor SuccessColor = Hex1bColor.FromRgb(120, 210, 130);  // friendly green
    internal static readonly Hex1bColor CancelColor = Hex1bColor.FromRgb(245, 165, 65);    // warm orange
    internal static readonly Hex1bColor ErrorColor = Hex1bColor.FromRgb(230, 95, 95);      // alert red

    /// <summary>
    /// Builds a "bar only" row — used to seed the gutter above the very first
    /// prompt and as the trailing spacer beneath each completed step.
    /// </summary>
    public static Hex1bWidget BuildBarRow<TParent>(WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => ctx.HStack(h =>
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
            BuildMarkerRow(v, TemplatePromptWidget.SubmittedMarker, TemplatePromptWidget.BarColor, label, TemplatePromptWidget.BodyColor),
            v.HStack(h =>
            [
                h.Text(" "),
                h.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, TemplatePromptWidget.BarColor),
                    h.Text(TemplatePromptWidget.BarChar)),
            ]),
        ]);

    /// <summary>
    /// Header row anchoring the start of the flow. Renders the supplied label
    /// next to a filled circle in the header colour, with a trailing bar-only
    /// spacer so the gutter flows naturally into the first prompt.
    /// </summary>
    public static Hex1bWidget BuildHeader(RootContext ctx, string label) =>
        ctx.VStack(v =>
        [
            BuildMarkerRow(v, HeaderMarker, HeaderColor, label, HeaderColor, bold: true),
            BuildBarRow(v),
        ]);

    /// <summary>
    /// Terminal outcome tombstone (success / cancel / error). The marker, the
    /// primary label, and any continuation detail lines are all painted in
    /// <paramref name="color"/>. The gutter stops here — no trailing bar row.
    /// </summary>
    public static Hex1bWidget BuildOutcome(
        RootContext ctx,
        string marker,
        Hex1bColor color,
        string primaryLabel,
        params string[] detailLines)
    {
        return ctx.VStack(v =>
        {
            var rows = new List<Hex1bWidget>
            {
                BuildMarkerRow(v, marker, color, primaryLabel, color, bold: true),
            };

            foreach (var detail in detailLines)
            {
                rows.Add(v.HStack(h =>
                [
                    h.Text(" "),
                    // Use a faint gutter-coloured bar so detail rows feel like
                    // a continuation of the flow rather than free-floating text.
                    h.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, TemplatePromptWidget.BarColor),
                        h.Text(TemplatePromptWidget.BarChar)),
                    h.Text("   "),
                    h.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, TemplatePromptWidget.BodyColor),
                        h.Text(detail)),
                ]));
            }

            return rows.ToArray();
        });
    }

    private static Hex1bWidget BuildMarkerRow<TParent>(
        WidgetContext<TParent> ctx,
        string marker,
        Hex1bColor markerColor,
        string label,
        Hex1bColor labelColor,
        bool bold = false)
        where TParent : Hex1bWidget
    {
        return ctx.HStack(h =>
        [
            h.Text(" "),
            h.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, markerColor),
                h.Text(marker)),
            h.Text("  "),
            h.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, labelColor),
                h.Text(label)),
        ]);
    }
}

