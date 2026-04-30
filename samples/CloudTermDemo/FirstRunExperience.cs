using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace CloudTermDemo;

/// <summary>
/// First-run / device login experience. Shows a QR code on the left for device
/// authentication and instructions on the right. Simulates the Azure CLI device
/// code flow pattern.
/// </summary>
public sealed class FirstRunExperience
{
    private const string DeviceLoginUrl = "https://aka.ms/devicelogin";
    private const string DeviceCode = "A1FB-HJ43";

    private readonly AppState _appState;

    public FirstRunExperience(AppState appState)
    {
        _appState = appState;
    }

    public Hex1bWidget Build<TParent>(WidgetContext<TParent> ctx, Hex1bApp app)
        where TParent : Hex1bWidget
    {
        // Light blue ANSI for the device code
        var codeText = $"\x1b[38;2;100;180;255m{DeviceCode}\x1b[0m";

        return ctx.Center(
            ctx.HStack(h => [

                // QR code with device code underneath
                h.Padding(0, 3, 0, 0,
                    h.VStack(qr => [
                        qr.QrCode(DeviceLoginUrl).WithQuietZone(0),
                        qr.Text(codeText),
                    ])
                ),

                // Instructions — vertically centered alongside the QR code
                h.Align(Alignment.VCenter,
                    h.VStack(text => [
                        text.Text("Welcome to Cloud Term"),
                        text.Text(""),
                        text.Text("It looks like this is your first time"),
                        text.Text("running Cloud Term. To get started you"),
                        text.Text("need to sign in to the cloud."),
                        text.Text(""),
                        text.Text("Scan the QR code with your phone, or"),
                        text.Text($"visit {DeviceLoginUrl}"),
                        text.Text("and enter the code shown below the"),
                        text.Text("QR code to get started."),
                        text.Text(""),
                        text.HStack(s => [
                            s.Spinner(SpinnerStyle.Dots),
                            s.Text(" Waiting for authentication..."),
                        ]),
                    ])
                ),

            ])
        ).Fill();
    }
}
