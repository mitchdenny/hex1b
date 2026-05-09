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
}

/// <summary>
/// Per-build context handed to a slide. Carries the root widget context plus
/// the live presenter state so slides can react to where they are in the deck
/// if they want to. Most slides won't need <see cref="State"/>.
/// </summary>
/// <param name="Root">The root widget context for constructing widgets.</param>
/// <param name="State">The live presenter state (read-only access expected).</param>
internal sealed record SlideContext(RootContext Root, PresenterState State);
