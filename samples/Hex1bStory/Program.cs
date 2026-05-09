using Hex1b;
using Hex1b.Input;
using Hex1bStory;
using Hex1bStory.Shell;

// Hex1bStory — a presentation tool built with Hex1b. The shell flips between
// a playlist picker and a slideshow; each individual slide is its own class
// in `Slides/` so they can grow as elaborate as needed without bloating
// this file.

var state = new PresenterState();
var playlists = Playlists.All;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        var screen = state.Mode == PresenterMode.Presenting
            ? PresentationView.Build(ctx, state)
            : PlaylistMenuView.Build(ctx, state, playlists);

        return screen.InputBindings(b => RegisterGlobalBindings(b, state));
    })
    .Build();

await terminal.RunAsync();

static void RegisterGlobalBindings(InputBindingsBuilder bindings, PresenterState state)
{
    // Esc backs out of a playlist; from the menu it exits. Always wired —
    // List doesn't use Escape, so it's safe to register globally regardless
    // of mode.
    bindings.Key(Hex1bKey.Escape).Global().Action(c =>
    {
        if (state.Mode == PresenterMode.Presenting)
        {
            state.ReturnToMenu();
            c.Invalidate();
        }
        else
        {
            c.RequestStop();
        }
    }, "Back / quit");

    // Slide navigation is only registered while presenting. The menu's List
    // owns Spacebar (activate), Home/End and PageUp/PageDown (paging), and
    // Enter — registering global handlers for those keys in menu mode would
    // swallow input the List needs.
    if (state.Mode != PresenterMode.Presenting)
    {
        return;
    }

    bindings.Key(Hex1bKey.PageDown).Global().Action(c => Advance(state, c), "Next slide");
    bindings.Key(Hex1bKey.RightArrow).Global().Action(c => Advance(state, c), "Next slide");
    bindings.Key(Hex1bKey.Spacebar).Global().Action(c => Advance(state, c), "Next slide");
    bindings.Key(Hex1bKey.PageUp).Global().Action(c => Retreat(state, c), "Previous slide");
    bindings.Key(Hex1bKey.LeftArrow).Global().Action(c => Retreat(state, c), "Previous slide");

    bindings.Key(Hex1bKey.Home).Global().Action(c =>
    {
        state.FirstSlide();
        c.Invalidate();
    }, "First slide");

    bindings.Key(Hex1bKey.End).Global().Action(c =>
    {
        state.LastSlide();
        c.Invalidate();
    }, "Last slide");
}

static void Advance(PresenterState state, InputBindingActionContext ctx)
{
    state.NextSlide();
    ctx.Invalidate();
}

static void Retreat(PresenterState state, InputBindingActionContext ctx)
{
    state.PreviousSlide();
    ctx.Invalidate();
}
