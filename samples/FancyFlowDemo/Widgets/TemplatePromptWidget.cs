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

    // --- Glow effect tuning ---------------------------------------------------
    // The glow paints a soft purple wash anchored to the gutter that fades as it
    // travels right and as it approaches the top/bottom of the prompt block. A
    // slow travelling sine wave gives it an organic, non-uniform pulse so each
    // row breathes slightly out of phase with its neighbours.
    //
    // Because EffectPanel post-processes the child's already-rendered surface,
    // we don't actually know the terminal's true background colour. We model it
    // as near-black and paint *additive* tints on top — i.e. the brighter the
    // glow, the more saturated purple we write into the cell's background.
    private static readonly Hex1bColor GlowColor = Hex1bColor.FromRgb(180, 120, 220);
    private const int GlowWidthMax = 36;     // peak width of the wash (middle row)
    private const int GlowWidthMin = 2;      // width at the very top/bottom rows
    private const float MaxAlpha = 0.55f;    // peak background tint strength
    private const float PulseHz = 0.35f;     // overall slow breathing rate
    private const float SpatialKx = 0.18f;   // horizontal wave density
    private const float SpatialKy = 0.55f;   // vertical phase offset (per row)
    private const float AlphaCutoff = 0.02f; // ignore truly negligible contributions

    protected override Hex1bWidget Build(CompositionContext ctx)
    {
        var grid = BuildGrid(ctx);

        // EffectPanel post-processes the whole frame's surface in place. The
        // context-aware overload also gives us the render context so we can
        // read the global background colour and blend toward it (instead of
        // multiplying GlowColor by alpha, which crushes to black on most
        // terminals). Cells the child explicitly painted are left untouched
        // so deliberate shading (selected list rows, focused textbox, etc.)
        // is preserved.
        return ctx.EffectPanel(grid, ApplyGlow)
            .RedrawAfter(50);
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

    private static void ApplyGlow(Surface surface, Hex1bRenderContext ctx)
    {
        // Resolve the terminal's effective background:
        //  1. If a global theme bg is set, use that (a parent ThemePanel etc).
        //  2. Otherwise use the terminal's reported default bg (probed at
        //     startup via OSC 11 — see ConsolePresentationAdapter).
        //  3. Fall back to neutral dark grey if nothing is known.
        Hex1bColor baseBg;
        var globalBg = ctx.Theme.GetGlobalBackground();
        if (!globalBg.IsDefault)
        {
            baseBg = globalBg;
        }
        else
        {
            var probed = ctx.Capabilities.DefaultBackground;
            baseBg = Hex1bColor.FromRgb(
                (byte)((probed >> 16) & 0xFF),
                (byte)((probed >> 8) & 0xFF),
                (byte)(probed & 0xFF));
        }

        // Wall-clock time in seconds drives the breathing pulse.
        var t = (float)(Environment.TickCount64 / 1000.0);
        var height = surface.Height;
        var width = surface.Width;

        for (int y = 0; y < height; y++)
        {
            // Vertical envelope: 0 at top edge, 1 in the middle, 0 at bottom.
            float vertEnv = height <= 1
                ? 1f
                : MathF.Sin(MathF.PI * (y + 0.5f) / height);
            if (vertEnv <= 0f) continue;

            // The glow forms a lens/leaf shape: the top and bottom rows only
            // wash the first GlowWidthMin cells, while the middle row reaches
            // out to GlowWidthMax. Per-row width is interpolated by vertEnv,
            // and per-cell alpha is further attenuated by vertEnv so the very
            // edges still fade close to the terminal background.
            float rowWidth = GlowWidthMin + (GlowWidthMax - GlowWidthMin) * vertEnv;

            for (int x = 0; x < width; x++)
            {
                // Horizontal falloff inside the row's effective width.
                float horiz = 1f - x / rowWidth;
                if (horiz <= 0f) break;
                horiz *= horiz; // ease-out so the leading edge looks softer

                var cell = surface.GetCell(x, y);
                // Skip cells the child explicitly painted (selected list row,
                // textbox cursor, focused chip, etc.) so we never obliterate
                // intentional shading.
                if (!cell.HasTransparentBackground) continue;

                // Travelling wave with a per-row phase offset gives a gently
                // rolling glow rather than a uniform pulse.
                float wave = 0.5f + 0.5f * MathF.Sin(
                    t * MathF.PI * 2f * PulseHz - x * SpatialKx + y * SpatialKy);

                float alpha = MaxAlpha * vertEnv * horiz * (0.55f + 0.45f * wave);
                if (alpha <= AlphaCutoff) continue;

                // Linear blend in RGB toward the glow colour from the
                // resolved terminal background. At alpha=0 the cell would
                // match the surroundings (and is skipped anyway); at the
                // peak we get GlowColor at MaxAlpha intensity.
                byte br = (byte)(baseBg.R + (GlowColor.R - baseBg.R) * alpha);
                byte bg = (byte)(baseBg.G + (GlowColor.G - baseBg.G) * alpha);
                byte bb = (byte)(baseBg.B + (GlowColor.B - baseBg.B) * alpha);
                surface.TrySetCell(x, y, cell.WithBackground(Hex1bColor.FromRgb(br, bg, bb)));
            }
        }
    }
}

