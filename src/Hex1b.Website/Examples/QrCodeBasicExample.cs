using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// QrCode Widget Documentation: Basic Usage
/// Demonstrates simple QR code creation for encoding URLs.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/qrcode.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class QrCodeBasicExample(ILogger<QrCodeBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<QrCodeBasicExample> _logger = logger;

    public override string Id => "qrcode-basic";
    public override string Title => "QR Code Widget - Basic Usage";
    public override string Description => "Demonstrates basic QR code creation for URLs";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating QR code basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("QR Code Example"),
                v.Text(""),
                v.Text("Scan with your phone:"),
                v.QrCode("https://hex1b.dev"),
                v.Text(""),
                v.Text("The QR code encodes: https://hex1b.dev")
            ]);
        };
    }
}
