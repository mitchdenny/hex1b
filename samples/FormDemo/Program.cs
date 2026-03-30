using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

// ──────────────────────────────────────────────────────
// FormDemo — demonstrates the Hex1b Form widget
// ──────────────────────────────────────────────────────

var firstName = "";
var lastName = "";
var email = "";
var company = "";
var title = "";
var lastAction = "None";
var labelPlacementIndex = 0; // 0 = Above, 1 = Inline

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        var labelPlacement = labelPlacementIndex == 0
            ? LabelPlacement.Above
            : LabelPlacement.Inline;

        return ctx.VStack(v => [
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                v.Text("  ◆ Form Widget Demo")),
            v.Text("  Tab/Shift+Tab to navigate fields. Type to edit. Ctrl+C to exit."),
            v.Separator(),

            // Label placement toggle (outside the form)
            v.HStack(h => [
                h.Text("  Label Position: "),
                h.ToggleSwitch(["Above", "Inline"], labelPlacementIndex)
                    .OnSelectionChanged(e => labelPlacementIndex = e.SelectedIndex),
            ]).ContentHeight(),

            v.Separator(),
            v.Text(""),

            // ── Contact Form ──
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.White),
                v.Text("  Contact Information")),
            v.Text(""),
            v.Form(form =>
            {
                var firstNameField = form.TextField("First Name")
                    .WithMinWidth(30)
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("First name is required")
                        : ValidationResult.Valid)
                    .OnTextChanged(e => firstName = e.NewText);

                var lastNameField = form.TextField("Last Name")
                    .WithMinWidth(30)
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("Last name is required")
                        : ValidationResult.Valid)
                    .OnTextChanged(e => lastName = e.NewText);

                var emailField = form.TextField("Email")
                    .WithMinWidth(40)
                    .Validate(value =>
                    {
                        if (string.IsNullOrWhiteSpace(value))
                            return ValidationResult.Error("Email is required");
                        if (!value.Contains('@') || !value.Contains('.'))
                            return ValidationResult.Error("Enter a valid email address");
                        return ValidationResult.Valid;
                    })
                    .OnTextChanged(e => email = e.NewText);

                var companyField = form.TextField("Company")
                    .WithMinWidth(30)
                    .OnTextChanged(e => company = e.NewText);

                var titleField = form.TextField("Job Title")
                    .WithMinWidth(30)
                    .OnTextChanged(e => title = e.NewText);

                return [
                    firstNameField,
                    lastNameField,
                    emailField,
                    form.ValidationMessageFor(firstNameField, lastNameField, emailField),
                    companyField,
                    titleField,
                    form.SubmitButton("Submit", _ => lastAction = "Submitted!"),
                    form.CancelButton(_ => lastAction = "Cancelled"),
                ];
            }).WithLabelPlacement(labelPlacement)
              .WithLabelWidth(15),

            v.Text(""),
            v.Separator(),

            // Live preview of entered values
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                v.VStack(preview => [
                    preview.Text($"  Name:    {firstName} {lastName}"),
                    preview.Text($"  Email:   {email}"),
                    preview.Text($"  Company: {company}"),
                    preview.Text($"  Title:   {title}"),
                ])),
            v.Text($"  Last action: {lastAction}"),
        ]);
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();
