ctx.ThemePanel(
    theme => theme
        .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(0, 100, 0))
        .Set(ButtonTheme.ForegroundColor, Hex1bColor.White)
        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Green),
    ctx.Button("Themed Button", () => { })
)
