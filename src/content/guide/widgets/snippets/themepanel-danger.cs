ctx.ThemePanel(
    theme => theme
        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(139, 0, 0))
        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red)
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Red),
    ctx.VStack(v => [
        v.Text("⚠ Danger Zone"),
        v.Button("Delete Everything")
    ])
)
