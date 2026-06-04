using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// ── A cute playlist demo for ListWidget<T> + ItemTemplate ─────────
// Pick a track with Enter / Space / click — the row's template morphs into
// a "Now Playing" banner with a dancing equalizer. Pick another track and
// it takes over; the old one collapses back to its compact view.

var tracks = new[]
{
    new Track("01", "Strawberry Static",   "The Velvet Cassette", "Analog Daydreams",    new TimeSpan(0, 3, 42), "🍓"),
    new Track("02", "Neon Garden",         "Polaroid Comet",      "City Light Atlas",     new TimeSpan(0, 4, 18), "🌃"),
    new Track("03", "Lo-Fi Cartography",   "Otter Mountain",      "Where The Maps End",   new TimeSpan(0, 5,  7), "🗺️"),
    new Track("04", "Hexagons in the Rain","Mitchell & The Keys", "Side B",               new TimeSpan(0, 3, 11), "💎"),
    new Track("05", "Sunset Buffer",       "Background Service",  "warmboot.zip",         new TimeSpan(0, 2, 56), "🌅"),
    new Track("06", "Cassette Future",     "Future Tape",         "Side A",               new TimeSpan(0, 4,  2), "📼"),
    new Track("07", "Tiny Robot Lullaby",  "Quiet Machines",      "Sleep Mode",           new TimeSpan(0, 3, 28), "🤖"),
};

Track? nowPlaying = null;
int equalizerPhase = 0;
Hex1b.Hex1bApp? appHandle = null;

// Bump the equalizer phase ~5 times per second so the playing row visibly dances.
using var animationCts = new CancellationTokenSource();
_ = Task.Run(async () =>
{
    try
    {
        while (!animationCts.IsCancellationRequested)
        {
            await Task.Delay(200, animationCts.Token).ConfigureAwait(false);
            if (nowPlaying is not null)
            {
                equalizerPhase++;
                appHandle?.Invalidate();
            }
        }
    }
    catch (OperationCanceledException) { }
});

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp(
        _ => { },
        a =>
        {
            appHandle = a;
            return BuildUi;
        })
    .Build();

try
{
    await terminal.RunAsync();
}
finally
{
    animationCts.Cancel();
}

Hex1bWidget BuildUi(RootContext ctx) => ctx.Border(b => [
    b.VStack(v => [
        v.Text(""),
        v.HStack(header => [
            header.Text("  ♪ Hex1b FM "),
            header.Text("").Fill(),
            header.Text(nowPlaying is null
                ? "Pick a track and press Enter "
                : $"Now playing: {nowPlaying.Title} "),
        ]).FixedHeight(1),
        v.Text(""),

        // The star of the show: ListWidget<Track> with a template that
        // branches on whether the row is the currently-playing track.
        v.List(tracks)
            .ItemHeight(3)
            .ItemKey(t => t.Number)
            .OnItemActivated(args =>
            {
                // Toggle: same track again -> stop; different track -> switch.
                nowPlaying = ReferenceEquals(nowPlaying, args.ActivatedItem)
                    ? null
                    : args.ActivatedItem;
                equalizerPhase = 0;
                appHandle?.Invalidate();
            })
            .ItemTemplate(context =>
            {
                var isPlaying = ReferenceEquals(nowPlaying, context.Item);
                return isPlaying
                    ? RenderPlaying(context, equalizerPhase)
                    : RenderCompact(context);
            })
            .Fill(),

        v.Text(""),
        v.InfoBar([
            "↑↓", "Browse",
            "Enter", "Play / Stop",
            "Esc / Ctrl+C", "Exit",
        ])
    ])
]).Title(" ListWidget<T> demo: pick a track ");

static Hex1bWidget RenderCompact(ListItemContext<Track> context)
{
    var track = context.Item;
    var marker = context.IsSelected ? "▸" : " ";
    var hoverHint = context.IsHovered && !context.IsSelected ? "·" : " ";
    var length = $"{track.Length.Minutes}:{track.Length.Seconds:00}";

    return context.VStack(v => [
        v.Text($" {marker}{hoverHint} {track.Emoji}  {track.Number}  {track.Title}"),
        v.Text($"        {track.Artist} — {track.Album}  ({length})"),
        v.Text(""),
    ]);
}

static Hex1bWidget RenderPlaying(ListItemContext<Track> context, int phase)
{
    var track = context.Item;
    var length = $"{track.Length.Minutes}:{track.Length.Seconds:00}";
    var bars = RenderEqualizer(phase);

    return context.VStack(v => [
        v.Text($" ♫♫ NOW PLAYING ──────────────────── {bars} "),
        v.Text($"    {track.Emoji}  {track.Title}  ({length})"),
        v.Text($"        {track.Artist} · {track.Album}"),
    ]);
}

static string RenderEqualizer(int phase)
{
    // Pseudo-random but deterministic per-phase bar pattern. 8 bars, each
    // cycles through a 4-step ramp offset by its column index.
    const string steps = "▁▂▄▆█▆▄▂";
    Span<char> buf = stackalloc char[8];
    for (int i = 0; i < 8; i++)
    {
        var step = (phase + i * 3) % steps.Length;
        buf[i] = steps[step];
    }
    return new string(buf);
}

internal sealed record Track(
    string Number,
    string Title,
    string Artist,
    string Album,
    TimeSpan Length,
    string Emoji);
