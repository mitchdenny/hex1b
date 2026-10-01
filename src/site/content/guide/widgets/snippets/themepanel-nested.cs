ctx.ThemePanel(
    outer => outer
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
    ctx.VStack(v => [
        v.Text("Outer theme - Cyan"),
        v.ThemePanel(
            inner => inner
                .Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow),
            v.VStack(innerV => [
                innerV.Text("Inner theme - Yellow")
            ])
        ),
        v.Text("Back to outer - Cyan")
    ])
)
