using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Slider Widget Documentation: Basic Usage
/// Demonstrates a simple slider with value display.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/slider.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class SliderBasicExample(ILogger<SliderBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<SliderBasicExample> _logger = logger;

    public override string Id => "slider-basic";
    public override string Title => "Slider Widget - Basic Usage";
    public override string Description => "Demonstrates basic slider with value display";

    private double _currentValue = 50.0;

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating slider basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Volume Control"),
                v.Text(""),
                v.Text($"Current: {_currentValue:F0}%"),
                v.Slider(50)
                    .OnValueChanged(e => _currentValue = e.Value),
                v.Text(""),
                v.Text("Use ← → or Home/End to adjust")
            ]);
        };
    }
}
