ctx.ThemingPanel(
    theme => theme
        .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Cyan)
        .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
        .Set(ButtonTheme.BackgroundColor, Hex1bColor.DarkGray)
        .Set(ButtonTheme.LeftBracket, "< ")
        .Set(ButtonTheme.RightBracket, " >"),
    ctx.Button("Custom Button Style")
)
