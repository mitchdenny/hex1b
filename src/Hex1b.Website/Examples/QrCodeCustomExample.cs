using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// QrCode Widget Documentation: Custom URL Input
/// Demonstrates QR code with URL selection and quiet zone customization.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the customCode sample in:
/// src/content/guide/widgets/qrcode.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class QrCodeCustomExample(ILogger<QrCodeCustomExample> logger) : Hex1bExample
{
    private readonly ILogger<QrCodeCustomExample> _logger = logger;

    public override string Id => "qrcode-custom";
    public override string Title => "QR Code Widget - Custom URL";
    public override string Description => "Interactive QR code with URL selection and quiet zone control";

    private class QrCodeState
    {
        public string CurrentUrl { get; set; } = "https://github.com/mitchdenny/hex1b";
        public int QuietZone { get; set; } = 1;
        public string[] UrlOptions { get; } = [
            "https://github.com/mitchdenny/hex1b",
            "https://hex1b.dev",
            "https://dotnet.microsoft.com"
        ];
        public int SelectedUrlIndex { get; set; } = 0;
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating QR code custom example widget builder");

        var state = new QrCodeState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Interactive QR Code Demo"),
                v.Text(""),
                v.Text($"URL: {state.CurrentUrl}"),
                v.Text(""),
                v.QrCode(state.CurrentUrl).WithQuietZone(state.QuietZone),
                v.Text(""),
                v.Text("Select URL:"),
                v.Picker(state.UrlOptions, state.SelectedUrlIndex)
                    .OnSelectionChanged(e => {
                        state.SelectedUrlIndex = e.SelectedIndex;
                        state.CurrentUrl = state.UrlOptions[e.SelectedIndex];
                    }),
                v.Text(""),
                v.HStack(h => [
                    h.Text("Quiet Zone: "),
                    h.Button("-").OnClick(_ => {
                        if (state.QuietZone > 0) state.QuietZone--;
                    }),
                    h.Text($" {state.QuietZone} "),
                    h.Button("+").OnClick(_ => {
                        if (state.QuietZone < 4) state.QuietZone++;
                    })
                ])
            ]);
        };
    }
}
