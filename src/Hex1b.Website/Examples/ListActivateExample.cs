using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// List Widget Documentation: Item Activation
/// Demonstrates handling item activation with OnItemActivated for a todo list.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the activateCode sample in:
/// src/content/guide/widgets/list.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ListActivateExample(ILogger<ListActivateExample> logger) : Hex1bExample
{
    private readonly ILogger<ListActivateExample> _logger = logger;

    public override string Id => "list-activate";
    public override string Title => "List Widget - Item Activation";
    public override string Description => "Demonstrates handling item activation with OnItemActivated";

    private class TodoState
    {
        private readonly List<(string Text, bool Done)> _items =
        [
            ("Learn Hex1b", true),
            ("Build a TUI", false),
            ("Deploy to production", false)
        ];

        public IReadOnlyList<string> GetFormattedItems() =>
            _items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

        public void ToggleItem(int index)
        {
            if (index >= 0 && index < _items.Count)
            {
                var item = _items[index];
                _items[index] = (item.Text, !item.Done);
            }
        }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating list activate example widget builder");

        var state = new TodoState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text("Press Enter or Space to toggle items:"),
                    v.Text(""),
                    v.List(state.GetFormattedItems())
                        .OnItemActivated(e => state.ToggleItem(e.ActivatedIndex))
                ])
            ], title: "Todo List");
        };
    }
}
