using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Checkbox Widget Documentation: States
/// Demonstrates the three checkbox states: unchecked, checked, indeterminate.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the statesCode sample in:
/// src/content/guide/widgets/checkbox.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class CheckboxStatesExample(ILogger<CheckboxStatesExample> logger) : Hex1bExample
{
    private readonly ILogger<CheckboxStatesExample> _logger = logger;

    public override string Id => "checkbox-states";
    public override string Title => "Checkbox Widget - States";
    public override string Description => "Demonstrates the three checkbox states: unchecked, checked, indeterminate";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating checkbox states example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Checkbox States:"),
                v.Text(""),
                v.Checkbox().Unchecked().Label("Unchecked [ ]"),
                v.Checkbox().Checked().Label("Checked [x]"),
                v.Checkbox().Indeterminate().Label("Indeterminate [-]")
            ]);
        };
    }
}
