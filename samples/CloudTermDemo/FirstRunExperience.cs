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
        return ctx.HStack(h => [

            // Left panel — QR code centered
            h.VStack(left => [
                left.Text(""),
                left.Center(
                    left.QrCode(DeviceLoginUrl).WithQuietZone(1)
                ).Fill(),
            ]).Fill(),

            h.VSeparator(),

            // Right panel — instructions
            h.VStack(right => [
                right.Text(""),
                right.Text("  Welcome to Cloud Term"),
                right.Text(""),
                right.Text("  It looks like this is your first time"),
                right.Text("  running Cloud Term. To get started you"),
                right.Text("  need to sign in to the cloud."),
                right.Text(""),
                right.Text("  Scan the QR code with your phone, or"),
                right.Text($"  visit {DeviceLoginUrl}"),
                right.Text("  and enter the code:"),
                right.Text(""),
                right.Text($"  {DeviceCode}").Wrap(),
                right.Text(""),
                right.Text("  Waiting for authentication..."),
            ]).Fill(),

        ]);
    }
}
