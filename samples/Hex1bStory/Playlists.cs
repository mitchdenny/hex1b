using Hex1bStory.Slides;

namespace Hex1bStory;

/// <summary>
/// The set of playlists shown on the launch screen. Add a new entry here for
/// each version of the talk you want to be able to deliver — the slide
/// objects themselves live one folder over in <c>Slides/</c>.
/// </summary>
internal static class Playlists
{
    public static IReadOnlyList<Playlist> All { get; } =
    [
        new Playlist(
            Name: "short",
            Description: "Lightning version (~5 min)",
            Slides:
            [
                new TitleSlide(),
                new WhatIsHex1bSlide(),
                new ThanksSlide(),
            ]),

        new Playlist(
            Name: "full",
            Description: "Full talk with codebase tour",
            Slides:
            [
                new TitleSlide(),
                new WhatIsHex1bSlide(),
                new BuiltWithAgentsSlide(),
                new CodebaseTourSlide(),
                new ThanksSlide(),
            ]),
    ];
}
