using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Picker Widget Documentation: Basic Usage
/// Demonstrates simple picker creation with string items.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/picker.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class PickerBasicExample(ILogger<PickerBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<PickerBasicExample> _logger = logger;

    public override string Id => "picker-basic";
    public override string Title => "Picker Widget - Basic Usage";
    public override string Description => "Demonstrates basic picker creation with string items";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating picker basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text("Select a fruit:"),
                    v.Text(""),
                    v.HStack(h => [
                        h.Text("Fruit: "),
                        h.Picker(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
                    ])
                ])
            ], title: "Fruit Picker");
        };
    }
}
