using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// ToggleSwitch Widget Documentation: Multi-Option Example
/// Demonstrates toggle switch with more than two options.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the multiOptionCode sample in:
/// src/content/guide/widgets/toggle-switch.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ToggleSwitchMultiOptionExample(ILogger<ToggleSwitchMultiOptionExample> logger) : Hex1bExample
{
    private readonly ILogger<ToggleSwitchMultiOptionExample> _logger = logger;

    public override string Id => "toggle-switch-multi";
    public override string Title => "ToggleSwitch Widget - Multiple Options";
    public override string Description => "Demonstrates toggle switch with multiple options";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating toggle switch multi-option example widget builder");

        string currentSpeed = "Normal";

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text("Speed Settings"),
                    v.Text(""),
                    v.HStack(h => [
                        h.Text("Animation Speed: ").FixedWidth(20),
                        h.ToggleSwitch(["Slow", "Normal", "Fast"], selectedIndex: 1)
                            .OnSelectionChanged(args => currentSpeed = args.SelectedOption)
                    ]),
                    v.Text(""),
                    v.Text($"Current speed: {currentSpeed}"),
                    v.Text(""),
                    v.Text("Use arrow keys to cycle through options")
                ])
            ], title: "Configuration");
        };
    }
}
