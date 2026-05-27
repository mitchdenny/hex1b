using Hex1b;
using Hex1b.Composition;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace FancyFlowDemo.Widgets;

/// <summary>
/// Shared visual frame for every prompt in the FancyFlowDemo flow. Renders the
/// purple gutter bar one cell in from the left edge, with vertical breathing
/// room above the title and a special active-row marker at the row where the
/// prompt's interactive content starts.
/// </summary>
/// <param name="StepNumber">1-based step number shown in the header.</param>
/// <param name="Title">Short prompt title (e.g. "Pick a language").</param>
/// <param name="Description">One-line elaboration shown beneath the title.</param>
/// <param name="Content">The actual interactive prompt body.</param>
internal sealed record TemplatePromptWidget(
    int StepNumber,
    string Title,
    string Description,
    Hex1bWidget Content) : Hex1bWidget
{
    internal static readonly Hex1bColor BarColor = Hex1bColor.FromRgb(155, 89, 182);
    internal static readonly Hex1bColor ActiveColor = Hex1bColor.FromRgb(255, 215, 100);
    internal static readonly Hex1bColor TitleColor = Hex1bColor.FromRgb(187, 134, 252);
    internal static readonly Hex1bColor BodyColor = Hex1bColor.FromRgb(220, 200, 245);
    internal static readonly Hex1bColor MutedColor = Hex1bColor.FromRgb(140, 130, 160);
    internal const string BarChar = "┃";
    // Filled diamond marks the active prompt; the hollow diamond (◇) is used by
    // TombstoneFactory for completed steps, mirroring Clack's vertical timeline.
    internal const string ActiveMarker = "◆";
    internal const string SubmittedMarker = "◇";

    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        return BuildGrid(ctx);
    }

    private Hex1bWidget BuildGrid(CompositionContext ctx)
    {
        return ctx.Grid(g =>
        {
            // gutter | bar | gap | body
            g.Columns.Add(SizeHint.Fixed(1));
            g.Columns.Add(SizeHint.Fixed(1));
            g.Columns.Add(SizeHint.Fixed(2));
            g.Columns.Add(SizeHint.Fill);

            // 0 top spacer | 1 title | 2 desc | 3 mid spacer | 4 content | 5 bottom spacer
            g.Rows.Add(SizeHint.Fixed(1));
            g.Rows.Add(SizeHint.Content);
            g.Rows.Add(SizeHint.Content);
            g.Rows.Add(SizeHint.Fixed(1));
            g.Rows.Add(SizeHint.Content);
            g.Rows.Add(SizeHint.Fixed(1));

            return
            [
                // Top spacer bar (row 0).
                g.Cell(c => c.ThemePanel(
                        t => t.Set(SeparatorTheme.Color, BarColor)
                              .Set(SeparatorTheme.VerticalChar, BarChar),
                        c.VSeparator()))
                    .Row(0).Column(1),

                // Active marker on the title row — that's the prompt itself.
                g.Cell(c => c.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, ActiveColor),
                        c.Text(ActiveMarker)))
                    .Row(1).Column(1),

                // Bar segment below the marker (rows 2..5).
                g.Cell(c => c.ThemePanel(
                        t => t.Set(SeparatorTheme.Color, BarColor)
                              .Set(SeparatorTheme.VerticalChar, BarChar),
                        c.VSeparator()))
                    .RowSpan(2, 4).Column(1),

                // Body cells.
                g.Cell(c => c.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, TitleColor),
                        c.Text($"Step {StepNumber} — {Title}")))
                    .Row(1).Column(3),

                g.Cell(c => c.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, MutedColor),
                        c.Text(Description)))
                    .Row(2).Column(3),

                g.Cell(c => c.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, BodyColor)
                              .Set(ListTheme.SelectedBackgroundColor, ActiveColor)
                              .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black),
                        Content))
                    .Row(4).Column(3),
            ];
        });
    }
}

