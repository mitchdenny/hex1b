using Hex1b;
using Hex1b.Animation;
using Hex1b.Layout;
using Hex1b.Theming;
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
        return ctx.Center(
            ctx.HStack(h => [

                // QR code with URL and device code underneath, centered
                h.Padding(0, 3, 0, 0,
                    h.VStack(qr => [
                        qr.QrCode(DeviceLoginUrl).WithQuietZone(1),
                        qr.Padding(0, 0, 1, 0,
                            qr.Align(Alignment.HCenter,
                                qr.VStack(info => [
                                    info.Hyperlink(DeviceLoginUrl, DeviceLoginUrl),
                                    info.Text(""),
                                    // StatePanel for copy-flash animation
                                    info.StatePanel(this, sp =>
                                    {
                                        var anims = sp.GetAnimations();
                                        var flash = anims.Get<NumericAnimator<double>>("copyFlash", a =>
                                        {
                                            a.From = 0.0;
                                            a.To = 0.0;
                                            a.Duration = TimeSpan.FromMilliseconds(500);
                                            a.EasingFunction = Easing.EaseOutCubic;
                                        }, autoStart: false);

                                        var flashValue = flash.Value;

                                        // Blend from light blue to inverted (white bg)
                                        var bgR = (byte)(flashValue * 255);
                                        var bgG = (byte)(flashValue * 255);
                                        var bgB = (byte)(flashValue * 255);
                                        var fgR = (byte)(100 + (1 - flashValue) * 0);
                                        var fgG = (byte)(180 - flashValue * 150);
                                        var fgB = (byte)(255 - flashValue * 200);

                                        return sp.ThemePanel(
                                            t => t
                                                .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(fgR, fgG, fgB))
                                                .Set(GlobalTheme.BackgroundColor, flashValue > 0.01
                                                    ? Hex1bColor.FromRgb(bgR, bgG, bgB)
                                                    : Hex1bColor.Default),
                                            sp.Align(Alignment.HCenter,
                                                sp.HStack(code => [
                                                    code.Text($" {DeviceCode} "),
                                                    code.Icon("📋").OnClick(_ =>
                                                    {
                                                        app.CopyToClipboard(DeviceCode);
                                                        flash.From = 1.0;
                                                        flash.To = 0.0;
                                                        flash.Start();
                                                    }),
                                                ])
                                            )
                                        );
                                    }),
                                ])
                            )
                        ),
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
