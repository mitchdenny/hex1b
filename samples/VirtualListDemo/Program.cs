using System.Collections.Specialized;
using Hex1b;
using Hex1b.Composition;
using Hex1b.Data;
using Hex1b.Input;
using Hex1b.Widgets;

// ── Virtualized ListWidget<T> demo ──────────────────────────────────────────
// Binds a ListWidget<T> to a synthetic 100_000-row data source that simulates
// a slow remote API (50ms per page). The list materialises only the window of
// rows around the visible viewport on each frame — measure, render, and
// template reconciliation all stay O(viewport) regardless of the total count.
// Scroll fast with Down/Up to see the placeholder "loading…" appear for
// in-flight rows; they fill in as fetches complete.

const int TotalCount = 100_000;

var dataSource = new SyntheticRowSource(TotalCount, simulatedLatency: TimeSpan.FromMilliseconds(50));
Row? lastActivated = null;
Hex1bApp? appHandle = null;

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

await terminal.RunAsync();

Hex1bWidget BuildUi(RootContext ctx) => ctx.Border(b => [
    b.VStack(v => [
        v.Text($"  ☁  Virtualized list — {TotalCount:N0} rows, ~50ms simulated load latency"),
        v.Text("     Up/Down moves selection. Selecting a row past the cached window awaits a fetch.").FixedHeight(1),
        v.Text(""),

        v.List(dataSource)
            .ItemHeight(2)
            .ItemKey(r => r.Id)
            .OnItemActivated(args =>
            {
                lastActivated = args.ActivatedItem;
                appHandle?.Invalidate();
            })
            .ItemTemplate(RenderRow)
            .Fill(),

        v.Text(""),
        v.Text(lastActivated is null
            ? "  Press Enter on a row to activate it."
            : $"  ★ Activated: #{lastActivated.Id:N0} — {lastActivated.Title}").FixedHeight(1),

        v.InfoBar([
            "↑↓", "Browse",
            "Enter", "Activate",
            "Esc / Ctrl+C", "Exit",
        ]),
    ])
]).Title($" Virtualized ListWidget<T> · {TotalCount:N0} rows ");

static Hex1bWidget RenderRow(ListItemContext<Row> context)
{
    // In-flight rows have IsLoaded=false; keep the same height so scrolling
    // doesn't shimmer.
    if (!context.IsLoaded)
    {
        return context.VStack(v => [
            v.Text($"  · loading row {context.Index:N0}…"),
            v.Text(""),
        ]);
    }

    var item = context.Item;
    var marker = context.IsSelected ? "▸" : " ";
    var hoverHint = context.IsHovered && !context.IsSelected ? "·" : " ";

    return context.VStack(v => [
        v.Text($" {marker}{hoverHint} #{item.Id,-7:N0}  {item.Title}"),
        v.Text($"        {item.Subtitle}"),
    ]);
}

internal sealed record Row(int Id, string Title, string Subtitle);

/// <summary>
/// Synthetic data source — pretends to be a paged remote API. Generates rows
/// deterministically from the index so we never have to materialise the full
/// collection. Latency simulates the cost of a real network fetch so the
/// "loading…" placeholder is visible on fast scroll.
/// </summary>
internal sealed class SyntheticRowSource(int totalCount, TimeSpan simulatedLatency) : IListDataSource<Row>
{
    private static readonly string[] Adjectives =
    [
        "Cobalt", "Velvet", "Hexagonal", "Quiet", "Lo-Fi", "Glowing", "Cinnamon",
        "Polar", "Phosphor", "Ember", "Lunar", "Stratosphere", "Indigo", "Crystal",
        "Neon", "Drifting", "Soft", "Translucent", "Geodesic", "Marbled",
    ];

    private static readonly string[] Nouns =
    [
        "Otter", "Cassette", "Antenna", "Lighthouse", "Comet", "Loom", "Reef",
        "Atlas", "Echo", "Beacon", "Pavilion", "Mirage", "Caravan", "Aperture",
        "Switchback", "Filament", "Constellation", "Glacier", "Foothill", "Garden",
    ];

#pragma warning disable CS0067 // never raised — the source is immutable
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
#pragma warning restore CS0067

    public ValueTask<int> GetItemCountAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(totalCount);

    public async ValueTask<IReadOnlyList<Row>> GetItemsAsync(
        int startIndex,
        int count,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(simulatedLatency, cancellationToken).ConfigureAwait(false);

        var actual = Math.Min(count, Math.Max(0, totalCount - startIndex));
        var window = new Row[actual];
        for (int i = 0; i < actual; i++)
        {
            var idx = startIndex + i;
            var adj = Adjectives[idx % Adjectives.Length];
            var noun = Nouns[(idx / Adjectives.Length) % Nouns.Length];
            window[i] = new Row(
                Id: idx,
                Title: $"{adj} {noun}",
                Subtitle: $"row {idx:N0} of {totalCount:N0}");
        }
        return window;
    }

    public ValueTask<int?> GetIndexForKeyAsync(object? key, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<int?>(key is int i && i >= 0 && i < totalCount ? i : null);
}
