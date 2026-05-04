// ScrollPanelDemo
//
// Illustrates the behavioural changes from issue #296:
//   1. Global PageUp/PageDown can scroll a ScrollPanel even when focus is on
//      a sibling widget (e.g. a TextBox). Before the fix, the panel's
//      handlers short-circuited unless the panel itself was focused.
//   2. PageUp/PageDown bubbles up from a focusable descendant of the panel
//      (e.g. a Button rendered inside the scrollable content).
//   3. The new public imperative scroll API on ScrollPanelNode:
//        node.Offset = N            (settable property, mirrors ListNode.SelectedIndex)
//        node.ScrollByPage(±1)      (page jump)
//        node.ScrollBy(±N)          (relative offset, overflow-safe)
//        node.ScrollToTop() / .ScrollToBottom()
//      These methods are state mutations and intentionally do not fire OnScroll —
//      same convention as ListNode.SelectedIndex setter.
//
// The OnScroll handler captures the ScrollPanelNode reference on first fire,
// after which the imperative buttons (Top / Page- / Page+ / Bottom / Offset=25)
// can call into the public API. Until that first scroll, the buttons display a
// hint instead.
//
// Run with: dotnet run --project samples/ScrollPanelDemo

using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

ScrollPanelNode? panelNode = null;
var typedText = "Type here while focused…";
var lastAction = "(none yet)";
var currentOffset = 0;
var maxOffset = 0;
var viewportSize = 0;

var items = Enumerable.Range(1, 60)
    .Select(i => $"Item {i:00} ─ scroll me with PageUp/PageDown, arrows, or the buttons below")
    .ToList();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        var status = panelNode is null
            ? "Status: (scroll the panel once — any arrow or PageUp/Dn — to enable imperative buttons)"
            : $"Status: Offset={currentOffset}/{maxOffset}   Viewport={viewportSize}   " +
              $"Progress={(maxOffset > 0 ? currentOffset * 100 / maxOffset : 0),3}%   " +
              $"AtTop={(currentOffset <= 0).ToString().ToLowerInvariant(),-5}   " +
              $"AtBottom={(currentOffset >= maxOffset).ToString().ToLowerInvariant(),-5}";

        return ctx.VStack(v => [
            v.Text("ScrollPanelDemo — issue #296 fixes (global page actions + imperative API)"),
            v.Separator(),

            v.Text("Scenario A — Global PageUp/PageDown from a focused sibling:"),
            v.Text("  Focus the textbox below and type. Press PageUp/PageDown — the panel scrolls"),
            v.Text("  even though the textbox is focused. Before the fix this was a no-op."),
            v.TextBox(typedText).OnTextChanged(e => typedText = e.NewText),
            v.Text(""),

            v.Text("Scenario B — Bubble-up from a focusable descendant:"),
            v.Text("  Tab into a ★ button inside the panel, then press PageUp/PageDown."),
            v.Text("  The binding bubbles up from the button to the panel (no IsFocused guard)."),

            v.VScrollPanel(p => BuildScrollItems(p, items, label => lastAction = label))
                .WithInputBindings(b =>
                {
                    // Promote PageUp/PageDown to global so they fire from anywhere — issue #296.
                    b.Remove(ScrollPanelWidget.PageUpAction);
                    b.Remove(ScrollPanelWidget.PageDownAction);
                    b.Key(Hex1bKey.PageUp).Global().Triggers(ScrollPanelWidget.PageUpAction);
                    b.Key(Hex1bKey.PageDown).Global().Triggers(ScrollPanelWidget.PageDownAction);
                })
                .OnScroll(e =>
                {
                    panelNode = e.Node;
                    currentOffset = e.Offset;
                    maxOffset = e.MaxOffset;
                    viewportSize = e.ViewportSize;
                    lastAction = $"OnScroll fired: {e.PreviousOffset} → {e.Offset} (binding-driven)";
                })
                .FixedHeight(12),
            v.Text(""),

            v.Text("Scenario C — Imperative scroll API (public ScrollPanelNode methods):"),
            v.Text("  These click handlers call into the new public API directly. They do NOT"),
            v.Text("  fire OnScroll (mirrors ListNode.SelectedIndex setter convention)."),
            v.HStack(h => [
                h.Button("[ Top ]").OnClick(_ =>
                {
                    panelNode?.ScrollToTop();
                    SyncStatus();
                    lastAction = "panelNode.ScrollToTop()";
                }),
                h.Text(" "),
                h.Button("[ Page- ]").OnClick(_ =>
                {
                    panelNode?.ScrollByPage(-1);
                    SyncStatus();
                    lastAction = "panelNode.ScrollByPage(-1)";
                }),
                h.Text(" "),
                h.Button("[ Page+ ]").OnClick(_ =>
                {
                    panelNode?.ScrollByPage(+1);
                    SyncStatus();
                    lastAction = "panelNode.ScrollByPage(+1)";
                }),
                h.Text(" "),
                h.Button("[ Bottom ]").OnClick(_ =>
                {
                    panelNode?.ScrollToBottom();
                    SyncStatus();
                    lastAction = "panelNode.ScrollToBottom()";
                }),
                h.Text(" "),
                h.Button("[ Offset=25 ]").OnClick(_ =>
                {
                    if (panelNode is not null)
                    {
                        panelNode.Offset = 25;
                        SyncStatus();
                        lastAction = "panelNode.Offset = 25 (setter)";
                    }
                }),
                h.Text(" "),
                h.Button("[ Bump +5 ]").OnClick(_ =>
                {
                    panelNode?.ScrollBy(+5);
                    SyncStatus();
                    lastAction = "panelNode.ScrollBy(+5)";
                }),
            ]),

            v.Separator(),
            v.Text(status),
            v.Text($"Last action: {lastAction}"),
            v.Text(""),
            v.Text("Tab / Shift+Tab to move focus.   Ctrl+Q to quit.")
        ])
        .WithInputBindings(b =>
        {
            b.Ctrl().Key(Hex1bKey.Q).Action(c => { c.RequestStop(); return Task.CompletedTask; }, "Quit");
        });

        // Programmatic mutations don't fire OnScroll, so we sync the displayed
        // status manually after each imperative call.
        void SyncStatus()
        {
            if (panelNode is null) return;
            currentOffset = panelNode.Offset;
            maxOffset = panelNode.MaxOffset;
            viewportSize = panelNode.ViewportSize;
        }
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

// Builds the scrollable content: text rows interleaved with focusable ★ buttons
// at item 10/25/40 to demonstrate Scenario B (bubble-up from descendant).
static Hex1bWidget[] BuildScrollItems(
    WidgetContext<VStackWidget> ctx,
    IReadOnlyList<string> items,
    Action<string> setLastAction)
{
    var widgets = new List<Hex1bWidget>(capacity: items.Count + 4);
    for (var i = 0; i < items.Count; i++)
    {
        widgets.Add(ctx.Text(items[i]));
        if (i is 9 or 24 or 39)
        {
            var captured = i + 1;
            widgets.Add(ctx.Button($"  ★ Focusable button after item {captured} — Tab here, then PageUp/PageDown")
                .OnClick(_ => setLastAction($"Clicked ★ button after item {captured}")));
        }
    }
    return widgets.ToArray();
}
