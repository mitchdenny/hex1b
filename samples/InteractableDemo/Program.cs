using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

// Demo: InteractableWidget — composable clickable/focusable/hoverable regions
//
// Simulates an Aspire-style dashboard with resource tiles.
// Each tile wraps non-interactive content but the entire area
// is focusable, clickable, and responds to hover.

var resources = new[]
{
    new Resource("apiservice", "Running", "🟢", 3, 142),
    new Resource("frontend", "Running", "🟢", 1, 87),
    new Resource("postgres", "Running", "🟢", 0, 31),
    new Resource("redis", "Starting", "🟡", 0, 5),
    new Resource("worker", "Stopped", "🔴", 12, 0),
    new Resource("migration", "Finished", "⚪", 0, 0),
};

string? selectedResource = null;
string? lastAction = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            // Header
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                v.Text(" ◆ Aspire Dashboard — Interactable Demo")),
            v.Text(" Tab/Shift+Tab to navigate, Enter to select, mouse to click/hover"),
            v.Separator(),

            // Tile grid — each resource is an Interactable
            v.VStack(tiles => [
                ..resources.Select(r => BuildResourceTile(tiles, r))
            ]).Fill(),

            // Footer showing last action
            v.Separator(),
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                v.Text(lastAction != null
                    ? $" Last action: {lastAction}"
                    : " No action yet — click or press Enter on a tile")),
        ]);
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

InteractableWidget BuildResourceTile(
    WidgetContext<VStackWidget> parent,
    Resource resource)
{
    return parent.Interactable(ic =>
    {
        // Colors react to focus and hover state
        var nameFg = ic.IsFocused ? Hex1bColor.White
            : ic.IsHovered ? Hex1bColor.Cyan
            : Hex1bColor.Gray;
        var statusFg = resource.Status switch
        {
            "Running" => Hex1bColor.Green,
            "Starting" => Hex1bColor.Yellow,
            "Stopped" => Hex1bColor.Red,
            "Finished" => Hex1bColor.DarkGray,
            _ => Hex1bColor.White,
        };
        var borderColor = ic.IsFocused ? Hex1bColor.Cyan
            : ic.IsHovered ? Hex1bColor.Blue
            : selectedResource == resource.Name ? Hex1bColor.FromRgb(0, 128, 128)
            : Hex1bColor.DarkGray;

        return ic.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, borderColor),
            ic.Border(
                ic.HStack(h => [
                    // Status indicator + name
                    h.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, statusFg),
                        h.Text($" {resource.Indicator} ")),
                    h.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, nameFg),
                        h.Text(resource.Name)).FixedWidth(20),

                    // Status
                    h.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, statusFg),
                        h.Text(resource.Status)).FixedWidth(12),

                    // Metrics
                    h.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor,
                            resource.Errors > 0 ? Hex1bColor.Red : Hex1bColor.DarkGray),
                        h.Text($"Errors: {resource.Errors}")).FixedWidth(14),

                    h.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                        h.Text($"Req: {resource.Requests}")),
                ])
            )
        );
    })
    .OnClick(args =>
    {
        selectedResource = resource.Name;
        lastAction = $"Clicked {resource.Name} ({resource.Status})";
    })
    .OnFocusChanged(args =>
    {
        if (args.IsFocused)
            lastAction = $"Focused {resource.Name}";
    })
    .OnHoverChanged(args =>
    {
        if (args.IsHovered)
            lastAction = $"Hovering {resource.Name}";
    });
}

record Resource(string Name, string Status, string Indicator, int Errors, int Requests);
