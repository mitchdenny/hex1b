using Hex1b;

// Application state
var currentUrl = "https://github.com/mitchdenny/hex1b";
var urlOptions = new[]
{
    "https://github.com/mitchdenny/hex1b",
    "https://hex1b.dev",
    "https://dotnet.microsoft.com",
    "https://www.nuget.org/packages/Hex1b",
    "https://learn.microsoft.com/dotnet/aspire"
};
var selectedUrlIndex = 0;
var customUrl = "";
var showCustomUrl = false;
var quietZone = 1;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    ctx.VStack(main => [
        // Title
        main.Border(
            main.VStack(title => [
                title.Text(""),
                title.Text("  QR Code Widget Demo").FillWidth(),
                title.Text("  ═══════════════════════════════════════").FillWidth(),
                title.Text(""),
                title.Text("  Scan the QR code with your phone to visit the URL").FillWidth(),
                title.Text("")
            ]),
            title: "QR Code Demo"
        ).FixedHeight(8),

        // Main content
        main.Border(
            main.HStack(content => [
                // Left side - QR Code
                content.VStack(left => [
                    left.Text(""),
                    left.Text("  Current URL:"),
                    left.Text($"  {currentUrl}").Ellipsis(),
                    left.Text(""),
                    left.HStack(qr => [
                        qr.Text("    "), // Indent
                        qr.QrCode(currentUrl).WithQuietZone(quietZone)
                    ]).Fill(),
                    left.Text(""),
                    left.Text($"  Quiet Zone: {quietZone}"),
                    left.Text("")
                ]).FillWidth(),

                // Right side - Controls
                content.VStack(right => [
                    right.Text(""),
                    right.Text("  Select Preset URL:"),
                    right.Text(""),
                    right.Picker(urlOptions, selectedUrlIndex)
                        .OnSelectionChanged(e => {
                            selectedUrlIndex = e.SelectedIndex;
                            currentUrl = urlOptions[e.SelectedIndex];
                            showCustomUrl = false;
                        }),
                    right.Text(""),
                    right.Text("  Or enter custom URL:"),
                    right.Text(""),
                    right.HStack(row => [
                        row.Text("  "),
                        row.TextBox(customUrl)
                            .FixedWidth(40)
                            .OnTextChanged(e => {
                                customUrl = e.NewText;
                                if (!string.IsNullOrWhiteSpace(customUrl))
                                {
                                    showCustomUrl = true;
                                    currentUrl = customUrl;
                                }
                            })
                    ]).FixedHeight(1),
                    right.Text(""),
                    right.Text("  Quiet Zone (border):"),
                    right.Text(""),
                    right.HStack(row => [
                        row.Text("  "),
                        row.Button("-").OnClick(_ => {
                            if (quietZone > 0) quietZone--;
                        }),
                        row.Text($" {quietZone} "),
                        row.Button("+").OnClick(_ => {
                            if (quietZone < 4) quietZone++;
                        })
                    ]).FixedHeight(1),
                    right.Text(""),
                    right.HStack(buttons => [
                        buttons.Text("  "),
                        buttons.Button("Reset").OnClick(_ => {
                            selectedUrlIndex = 0;
                            currentUrl = urlOptions[0];
                            customUrl = "";
                            showCustomUrl = false;
                            quietZone = 1;
                        }),
                        buttons.Text(" "),
                        buttons.Button("Exit").OnClick(e => e.Context.RequestStop())
                    ]).FixedHeight(1),
                    right.Text("")
                ]).FillWidth()
            ]),
            title: "QR Code & Controls"
        ).Fill(),

        // Status bar
        main.InfoBar([
            "Tab", "Navigate",
            "↑/↓", "Select URL",
            "Ctrl+C", "Exit"
        ])
    ]))
    .WithMouse()
    .WithRenderOptimization()
    .Build();

await terminal.RunAsync();
