using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Picker Widget Documentation: Initial Selection
/// Demonstrates setting initial selected index.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the initialCode sample in:
/// src/content/guide/widgets/picker.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class PickerInitialExample(ILogger<PickerInitialExample> logger) : Hex1bExample
{
    private readonly ILogger<PickerInitialExample> _logger = logger;

    public override string Id => "picker-initial";
    public override string Title => "Picker Widget - Initial Selection";
    public override string Description => "Demonstrates setting initial selected index for pickers";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating picker initial selection example widget builder");

        var size = "Medium";
        var priority = "High";

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.HStack(h => [
                        h.Text("Size:     "),
                        h.Picker(["Small", "Medium", "Large", "X-Large"], initialSelectedIndex: 1)
                            .OnSelectionChanged(e => size = e.SelectedText)
                    ]),
                    v.HStack(h => [
                        h.Text("Priority: "),
                        h.Picker(["Low", "Medium", "High", "Critical"], initialSelectedIndex: 2)
                            .OnSelectionChanged(e => priority = e.SelectedText)
                    ]),
                    v.Text(""),
                    v.Text($"Order: {size} priority {priority}")
                ])
            ], title: "Order Form");
        };
    }
}
