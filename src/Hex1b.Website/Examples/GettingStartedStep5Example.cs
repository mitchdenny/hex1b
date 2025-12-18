using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Getting Started Step 5: Complete Todo Application
/// Full-featured todo app with add, toggle, and delete functionality.
/// </summary>
public class GettingStartedStep5Example(ILogger<GettingStartedStep5Example> logger) : Hex1bExample
{
    private readonly ILogger<GettingStartedStep5Example> _logger = logger;

    public override string Id => "getting-started-step5";
    public override string Title => "Getting Started - Step 5: Complete Todo";
    public override string Description => "Full-featured todo application";

    private class TodoState
    {
        public List<(string Text, bool Done)> Items { get; } = 
        [
            ("Learn Hex1b", true),
            ("Build a TUI", false),
            ("Deploy to production", false)
        ];

        public string NewItemText { get; set; } = "";
        public int SelectedIndex { get; set; }

        public int RemainingCount => Items.Count(i => !i.Done);

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

        public void DeleteItem(int index)
        {
            if (index >= 0 && index < Items.Count)
            {
                Items.RemoveAt(index);
                if (SelectedIndex >= Items.Count && Items.Count > 0)
                {
                    SelectedIndex = Items.Count - 1;
                }
            }
        }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating getting started step 5 widget builder");

        var state = new TodoState();

        return () =>
        {
            var ctx = new RootContext();

            return ctx.Border(b => [
                b.Text($"📋 Todo List ({state.RemainingCount} remaining)"),
                b.Text(""),
                b.HStack(h => [
                    h.Text("New: "),
                    h.TextBox(state.NewItemText, e => state.NewItemText = e.NewText),
                    h.Button("Add", _ => state.AddItem())
                ]),
                new SeparatorWidget(),
                b.List(
                    state.FormatItems(),
                    e => state.SelectedIndex = e.SelectedIndex,
                    e => state.ToggleItem(e.ActivatedIndex)
                ),
                b.Text(""),
                b.Button("Delete Selected", _ => state.DeleteItem(state.SelectedIndex)),
                b.Text(""),
                b.Text("↑↓: Navigate  Space: Toggle  Tab: Focus  Del: Delete")
            ], title: "My Todos");
        };
    }
}
