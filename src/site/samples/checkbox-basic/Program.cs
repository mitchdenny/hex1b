using Hex1b;
using Hex1b.Widgets;

var state = new TermsState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Terms and Conditions"),
        v.Text(""),
        v.Checkbox(state.AcceptTerms ? CheckboxValue.Checked : CheckboxValue.Unchecked)
            .Label("I accept the terms")
            .OnToggled(e => state.AcceptTerms = !state.AcceptTerms),
        v.Text(""),
        v.Button("Continue")
            .OnClick(_ => { if (state.AcceptTerms) Console.WriteLine("Accepted!"); })
    ]))
    .Build();

await terminal.RunAsync();

class TermsState
{
    public bool AcceptTerms { get; set; }
}
