ctx.ThemePanel(
    theme => theme
        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(139, 0, 0))
        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red),
    ctx.VStack(v => [
        v.Text("Theme mutations applied"),
        v.Button("Danger Button")
    ])
)
