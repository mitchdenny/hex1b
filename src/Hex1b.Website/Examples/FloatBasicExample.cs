using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Float Widget Documentation: Basic Usage
/// Demonstrates placing widgets at absolute (x, y) coordinates.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/float.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class FloatBasicExample(ILogger<FloatBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<FloatBasicExample> _logger = logger;

    public override string Id => "float-basic";
    public override string Title => "Float - Basic Usage";
    public override string Description => "Demonstrates placing widgets at absolute coordinates";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating float basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Float(v.Text("📍 Marker at (2, 1)")).Absolute(2, 1),
                v.Float(v.Text("📍 Marker at (30, 5)")).Absolute(30, 5),
                v.Float(v.Text("📍 Marker at (10, 9)")).Absolute(10, 9),
                v.Float(v.Border(b => [
                    b.Text("Boxed content")
                ]).Title("Info")).Absolute(45, 3),
            ]);
        };
    }
}
