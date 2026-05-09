using Hex1bStory.Slides;

namespace Hex1bStory;

/// <summary>
/// The set of playlists shown on the launch screen. Add a new entry here for
/// each version of the talk you want to be able to deliver — the slide
/// objects themselves live one folder over in <c>Slides/</c>.
/// </summary>
internal static class Playlists
{
    // Slides are SHARED between playlists — the same instance fields hold the
    // user's last selection / cursor state, so jumping between playlists feels
    // continuous.
    private static readonly TitleSlide Title = new();
    private static readonly FourItchesSlide FourItches = new();
    private static readonly TheWagerSlide Wager = new();
    private static readonly BootstrapFirstSlide Bootstrap = new();
    private static readonly SamplesGraveyardSlide Graveyard = new();
    private static readonly AgentsTolerateCrapSlide Tolerate = new();
    private static readonly TerminalEmulatorPlotTwistSlide PlotTwist = new();
    private static readonly ConformanceViaAgentsSlide Conformance = new();
    private static readonly VerdictSlide Verdict = new();
    private static readonly ThanksSlide Thanks = new();

    public static IReadOnlyList<Playlist> All { get; } =
    [
        new Playlist(
            Name: "story",
            Description: "How Hex1b came to be (full talk)",
            Slides:
            [
                Title,
                FourItches,
                Wager,
                Bootstrap,
                Graveyard,
                Tolerate,
                PlotTwist,
                Conformance,
                Verdict,
                Thanks,
            ]),

        new Playlist(
            Name: "short",
            Description: "Lightning version — title, samples graveyard, verdict",
            Slides:
            [
                Title,
                Graveyard,
                Verdict,
                Thanks,
            ]),
    ];
}
