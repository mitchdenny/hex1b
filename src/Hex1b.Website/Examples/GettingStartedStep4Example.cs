using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Getting Started Step 4: Todo with Text Input
/// Demonstrates adding new items with text input.
/// </summary>
public class GettingStartedStep4Example(ILogger<GettingStartedStep4Example> logger) : Hex1bExample
{
    private readonly ILogger<GettingStartedStep4Example> _logger = logger;

    public override string Id => "getting-started-step4";
    public override string Title => "Getting Started - Step 4: Todo with Input";
    public override string Description => "Todo list with text input to add new items";

    private class TodoState
    {
        public List<(string Text, bool Done)> Items { get; } = 
        [
            ("Learn Hex1b", true),
            ("Build a TUI", false)
        ];

        public string NewItemText { get; set; } = "";

        public IReadOnlyList<string> FormatItems() =>
            Items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

        public void AddItem()
        {
            if (!string.IsNullOrWhiteSpace(NewItemText))
            {
                Items.Add((NewItemText, false));
                NewItemText = "";
            }
        }

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
        _logger.LogInformation("Creating getting started step 4 widget builder");

        var state = new TodoState();

        return () =>
        {
            var ctx = new RootContext();

            return ctx.Border(b => [
                b.HStack(h => [
                    h.Text("New task: "),
                    h.TextBox(state.NewItemText, e => state.NewItemText = e.NewText),
                    h.Button("Add", _ => state.AddItem())
                ]),
                new SeparatorWidget(),
                b.List(state.FormatItems(), e => state.ToggleItem(e.ActivatedIndex)),
                b.Text(""),
                b.Text("Tab: Focus next  Space: Toggle")
            ], title: "📋 Todo");
        };
    }
}
