ctx.ThemingPanel(
    theme => theme
        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Cyan)
        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black),
    ctx.VStack(v => [
        v.Text("Buttons have cyan focus:"),
        v.Button("Styled Button")
    ])
)
