using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ToggleSwitch Widget Documentation: Basic Usage
/// Demonstrates simple toggle switch creation and navigation.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/toggle-switch.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ToggleSwitchBasicExample(ILogger<ToggleSwitchBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ToggleSwitchBasicExample> _logger = logger;

    public override string Id => "toggle-switch-basic";
    public override string Title => "ToggleSwitch Widget - Basic Usage";
    public override string Description => "Demonstrates basic toggle switch creation with multiple options";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating toggle switch basic example widget builder");

        string currentSelection = "Off";

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("ToggleSwitch Examples"),
                v.Text(""),
                v.Text($"Power: {currentSelection}"),
                v.Text(""),
                v.HStack(h => [
                    h.Text("Status: "),
                    h.ToggleSwitch(["Off", "On"])
                        .OnSelectionChanged(args => currentSelection = args.SelectedOption)
                ]),
                v.Text(""),
                v.Text("Use Left/Right arrows or click to toggle")
            ]);
        };
    }
}
