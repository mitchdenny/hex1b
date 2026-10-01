using Hex1b;

var state = new QrCodeState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Interactive QR Code Demo"),
        v.Text(""),
        v.Text($"URL: {state.CurrentUrl}"),
        v.Text(""),
        v.QrCode(state.CurrentUrl).QuietZone(state.QuietZone),
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
    ]))
    .Build();

await terminal.RunAsync();

class QrCodeState
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
