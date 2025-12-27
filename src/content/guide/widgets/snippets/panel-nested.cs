ctx.ThemingPanel(
    theme => theme.Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Blue),
    ctx.VStack(v => [
        v.Button("Blue focus"),
        v.ThemingPanel(
            theme => theme.Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Red),
            v.Button("Red focus (nested override)")
        ),
        v.Button("Blue focus again")
    ])
)
