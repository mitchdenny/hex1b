using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Getting Started Step 3: Simple Todo List
/// Demonstrates list display and item toggling.
/// </summary>
public class GettingStartedStep3Example(ILogger<GettingStartedStep3Example> logger) : Hex1bExample
{
    private readonly ILogger<GettingStartedStep3Example> _logger = logger;

    public override string Id => "getting-started-step3";
    public override string Title => "Getting Started - Step 3: Simple Todo";
    public override string Description => "Todo list with item toggling";

    private class TodoState
    {
        public List<(string Text, bool Done)> Items { get; } = 
        [
            ("Learn Hex1b", true),
            ("Build a TUI", false),
            ("Deploy to production", false)
        ];

        public IReadOnlyList<string> FormatItems() =>
            Items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

        public void ToggleItem(int index)
        {
            if (index >= 0 && index < Items.Count)
            {
                var item = Items[index];
                Items[index] = (item.Text, !item.Done);
            }
        }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating getting started step 3 widget builder");

        var state = new TodoState();

        return () =>
        {
            var ctx = new RootContext();

            return ctx.VStack(v => [
                v.Border(b => [
                    b.Text("📋 My Todos"),
                    b.Text(""),
                    b.List(state.FormatItems()).OnItemActivated(e => state.ToggleItem(e.ActivatedIndex))
                ]).Title("Todo List").Fill(),
                v.InfoBar("↑↓ Navigate  Space: Toggle")
            ]);
        };
    }
}
