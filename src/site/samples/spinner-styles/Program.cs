using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Built-in Spinner Styles"),
        v.Text(""),
        v.HStack(h => [
            h.Spinner(SpinnerStyle.Dots), h.Text(" Dots  "),
            h.Spinner(SpinnerStyle.Line), h.Text(" Line  "),
            h.Spinner(SpinnerStyle.Arrow), h.Text(" Arrow")
        ]),
        v.HStack(h => [
            h.Spinner(SpinnerStyle.Circle), h.Text(" Circle  "),
            h.Spinner(SpinnerStyle.Square), h.Text(" Square  "),
            h.Spinner(SpinnerStyle.Bounce), h.Text(" Bounce")
        ]),
        v.Text(""),
        v.Text("Multi-Character Styles"),
        v.HStack(h => [
            h.Spinner(SpinnerStyle.BouncingBall), h.Text(" BouncingBall  "),
            h.Spinner(SpinnerStyle.LoadingBar), h.Text(" LoadingBar")
        ])
    ]))
    .Build();

await terminal.RunAsync();
