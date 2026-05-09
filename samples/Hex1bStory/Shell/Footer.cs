using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Shell;

/// <summary>
/// Single-line footer pinned to the bottom-right of every screen. The exact
/// content depends on whether we're on the menu or in a playlist:
/// <list type="bullet">
///   <item>Menu mode → keyboard hint (`↑↓ choose · Enter start · Esc/Ctrl+C quit`)</item>
///   <item>Presenting mode → `playlist · N / total`</item>
/// </list>
/// </summary>
internal static class Footer
{
    public static Hex1bWidget Build<TParent>(WidgetContext<TParent> ctx, PresenterState state)
        where TParent : Hex1bWidget
    {
        var text = state.Mode switch
        {
            PresenterMode.Presenting when state.Current is { } pl
                => $"{pl.Name} · {state.SlideIndex + 1} / {pl.Slides.Count}",
            PresenterMode.Menu
                => "↑↓ choose · Enter start · Esc/Ctrl+C quit",
            _ => string.Empty,
        };

        // Right-glued, single line. Padding gives a one-cell gutter from the edge.
        return ctx.Padding(1, 0, 0, 0,
            ctx.Align(Alignment.BottomRight, ctx.Text(text)))
            .FillWidth()
            .FixedHeight(1);
    }
}
