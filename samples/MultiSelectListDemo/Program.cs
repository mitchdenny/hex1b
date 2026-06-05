using Hex1b;
using Hex1b.Events;
using Hex1b.Widgets;

// ── ListWidget<T>.MultiSelect() demo ──────────────────────────────
// A "pick what to deploy" panel: Space toggles individual rows, Shift+Up/Down
// extend a contiguous range, Ctrl+A flips select-all. The footer shows live
// counts; the row template paints checked rows with an accent stripe and a
// custom badge instead of relying solely on the built-in checkbox glyph.

var services = new[]
{
    new Service("auth-api",       "Authentication API",      "v3.4.1",  Tier.Critical),
    new Service("billing-api",    "Billing & invoicing",     "v2.0.0",  Tier.Critical),
    new Service("catalog-api",    "Product catalog",         "v1.18.0", Tier.Standard),
    new Service("search-indexer", "Search indexer worker",   "v0.9.7",  Tier.Standard),
    new Service("notifier",       "Notification dispatcher", "v1.2.3",  Tier.Standard),
    new Service("metrics-agent",  "Metrics agent",           "v4.0.0",  Tier.Optional),
    new Service("dashboard-web",  "Status dashboard",        "v2.1.4",  Tier.Optional),
    new Service("docs-site",      "Public docs site",        "v6.2.0",  Tier.Optional),
};

// Controlled multi-select: the selected indices live in our own state,
// and we replace the list inside the OnSelectionChanged callback.
IReadOnlyList<int> selected = Array.Empty<int>();
Hex1b.Hex1bApp? appHandle = null;

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
        v.Text(""),
        v.HStack(header => [
            header.Text("  📦 Deploy planner "),
            header.Text("").Fill(),
            header.Text($"Selected: {selected.Count}/{services.Length} "),
        ]).FixedHeight(1),
        v.Text(""),

        v.List(services)
            .ItemHeight(1)
            .ItemKey(s => s.Id)
            .MultiSelect()
            .SelectedIndices(selected)
            .OnSelectionChanged(args =>
            {
                selected = args.SelectedIndices;
                appHandle?.Invalidate();
            })
            .ItemTemplate(context =>
            {
                var s = context.Item;
                var stripe = context.IsSelected ? "▍" : " ";
                var arrow = context.IsFocused ? "▸" : " ";
                var badge = s.Tier switch
                {
                    Tier.Critical => "[CRIT]",
                    Tier.Standard => "[STD ]",
                    Tier.Optional => "[OPT ]",
                    _ => "      ",
                };
                var marker = context.IsSelected ? "▣" : "▢";
                return context.Text(
                    $" {arrow} {stripe} {marker} {badge} {s.DisplayName,-26} {s.Version}");
            })
            .Fill(),

        v.Text(""),
        v.HStack(footer => [
            footer.Text("  "),
            footer.Text(selected.Count == 0
                ? "Nothing queued — press Space or Ctrl+A to start."
                : $"Queued: {string.Join(", ", selected.Select(i => services[i].Id))}"),
        ]).FixedHeight(1),
        v.Text(""),
        v.InfoBar([
            "↑↓",        "Move",
            "Space",     "Toggle row",
            "Shift+↑↓",  "Extend range",
            "Ctrl+A",    "Select all",
            "Esc/^C",    "Exit",
        ]),
    ])
]).Title(" ListWidget<T>.MultiSelect() demo ");

internal enum Tier { Critical, Standard, Optional }

internal sealed record Service(string Id, string DisplayName, string Version, Tier Tier);
