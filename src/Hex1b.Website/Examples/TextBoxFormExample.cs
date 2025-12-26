using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

public class TextBoxFormExample(ILogger<TextBoxFormExample> logger) : Hex1bExample
{
    private readonly ILogger<TextBoxFormExample> _logger = logger;

    public override string Id => "textbox-form";
    public override string Title => "TextBox - Form Example";
    public override string Description => "Multiple TextBox widgets in a form layout.";

    private class FormState
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool Submitted { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating textbox form example");
        var state = new FormState();

        return () =>
        {
            var ctx = new RootContext();
            
            if (state.Submitted)
            {
                return ctx.Border(b => [
                    b.VStack(v => [
                        v.Text("✓ Form Submitted!"),
                        v.Text(""),
                        v.Text($"Name: {state.FirstName} {state.LastName}"),
                        v.Text($"Email: {state.Email}"),
                        v.Text(""),
                        v.Button("Reset").OnClick(_ => {
                            state.FirstName = "";
                            state.LastName = "";
                            state.Email = "";
                            state.Submitted = false;
                        })
                    ])
                ], title: "Confirmation");
            }

            return ctx.Border(b => [
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
            ], title: "Registration Form");
        };
    }
}
