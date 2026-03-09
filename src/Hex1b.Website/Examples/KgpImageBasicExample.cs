using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// KGP Image Documentation: Basic Usage
/// Demonstrates KGP image creation with a generated gradient and fallback text.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/kgpimage.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class KgpImageBasicExample(ILogger<KgpImageBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<KgpImageBasicExample> _logger = logger;

    public override string Id => "kgpimage-basic";
    public override string Title => "KGP Image - Basic Usage";
    public override string Description => "Demonstrates KGP image creation with a gradient and fallback";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating KGP image basic example widget builder");

        var width = 64;
        var height = 32;
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                pixels[i]     = (byte)(x * 255 / width);
                pixels[i + 1] = (byte)(y * 255 / height);
                pixels[i + 2] = 128;
                pixels[i + 3] = 255;
            }
        }

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("KGP Image Demo"),
                v.Text(""),
                v.KgpImage(pixels, width, height, "Terminal does not support graphics")
            ]);
        };
    }
}
