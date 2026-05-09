using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory.Shell;

/// <summary>
/// The launch screen. Shows the available playlists in a focusable List;
/// pressing Enter (or clicking, if mouse were enabled) starts the
/// highlighted playlist.
/// </summary>
internal static class PlaylistMenuView
{
    public static Hex1bWidget Build(
        RootContext ctx,
        PresenterState state,
        IReadOnlyList<Playlist> playlists)
    {
        // Items rendered in the list — keep them aligned so descriptions
        // line up regardless of name length.
        var nameWidth = playlists.Max(p => p.Name.Length);
        var items = playlists
            .Select(p => $"  {p.Name.PadRight(nameWidth)}   {p.Description}")
            .ToArray();

        return ctx.VStack(v =>
        [
            v.Padding(2, 1, v.VStack(body =>
            [
                body.Text("Hex1b Story"),
                body.Text(""),
                body.Text("Pick a playlist to begin."),
                body.Text(""),
                body.List(items)
                    .OnItemActivated(e =>
                    {
                        state.StartPlaylist(playlists[e.ActivatedIndex]);
                        e.Context.Invalidate();
                    }),
            ])).Fill(),
            Footer.Build(v, state),
        ]);
    }
}
