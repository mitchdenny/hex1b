using Hex1b;

var state = new FormState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.HStack(h => [
                h.Text("First Name: ").FixedWidth(12),
                h.TextBox(state.FirstName)
                    .OnTextChanged(args => state.FirstName = args.NewText)
            ]),
            v.HStack(h => [
                h.Text("Last Name:  ").FixedWidth(12),
                h.TextBox(state.LastName)
                    .OnTextChanged(args => state.LastName = args.NewText)
            ]),
            v.HStack(h => [
                h.Text("Email:      ").FixedWidth(12),
                h.TextBox(state.Email)
                    .OnTextChanged(args => state.Email = args.NewText)
            ]),
            v.Text(""),
            v.Button("Submit").OnClick(_ => state.Submitted = true),
            v.Text(""),
            v.Text("Use Tab to navigate between fields")
        ])
    ]).Title("Registration Form"))
    .Build();

await terminal.RunAsync();

class FormState
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Submitted { get; set; }
}
