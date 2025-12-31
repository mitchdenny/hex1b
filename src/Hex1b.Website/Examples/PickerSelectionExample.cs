using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Picker Widget Documentation: Selection Changed
/// Demonstrates handling selection change events.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the selectionCode sample in:
/// src/content/guide/widgets/picker.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class PickerSelectionExample(ILogger<PickerSelectionExample> logger) : Hex1bExample
{
    private readonly ILogger<PickerSelectionExample> _logger = logger;

    public override string Id => "picker-selection";
    public override string Title => "Picker Widget - Selection Changed";
    public override string Description => "Demonstrates handling picker selection change events";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating picker selection example widget builder");

        var selectedFruit = "Apple";

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text($"Selected fruit: {selectedFruit}"),
                    v.Text(""),
                    v.HStack(h => [
                        h.Text("Choose: "),
                        h.Picker(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
                            .OnSelectionChanged(e => selectedFruit = e.SelectedText)
                    ])
                ])
            ], title: "Selection Demo");
        };
    }
}
