using Hex1b;
using Hex1b.Widgets;

namespace Hex1bStory;

/// <summary>
/// Contract every slide implements. Each slide is its own class in
/// <c>Slides/</c> so it can grow as elaborate as it needs to without
/// bloating the shell.
/// </summary>
internal interface ISlide
{
    /// <summary>Short title — currently unused by the chrome but handy for diagnostics
    /// and for future use (e.g. an "outline" view).</summary>
    string Title { get; }

    /// <summary>Builds the slide's widget tree. Called every render frame.</summary>
    Hex1bWidget Build(SlideContext context);

    /// <summary>
    /// Called when the slide is no longer the active slide — i.e. when the
    /// presenter has navigated to another slide or back to the menu. Slides
    /// that own background work (embedded terminals, timers, …) should use
    /// this hook to cancel and dispose those resources so they don't leak
    /// while invisible.
    /// </summary>
    /// <remarks>
    /// Invoked fire-and-forget by the shell — handlers must not throw and
    /// should not assume any particular thread.
    /// </remarks>
    Task OnExit() => Task.CompletedTask;
}

/// <summary>
/// Per-build context handed to a slide. Carries the root widget context, the
/// live presenter state, and the outer terminal's dimensions so slides can
/// size embedded terminals and other dimension-sensitive widgets sensibly.
/// </summary>
/// <param name="Root">The root widget context for constructing widgets.</param>
/// <param name="State">The live presenter state (read-only access expected).</param>
/// <param name="Width">The outer terminal width in cells, including chrome.</param>
/// <param name="Height">The outer terminal height in cells, including chrome.</param>
internal sealed record SlideContext(RootContext Root, PresenterState State, int Width, int Height);
