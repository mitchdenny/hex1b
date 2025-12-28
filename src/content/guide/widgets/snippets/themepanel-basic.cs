ctx.ThemePanel(
    theme => theme.Clone()
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow)
        .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(0, 0, 139)),
    ctx.VStack(v => [
        v.Text("Themed content"),
        v.Text("Yellow on dark blue")
    ])
)
