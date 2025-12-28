ctx.ThemePanel(
    theme => theme
        .Set(BorderTheme.BorderColor, Hex1bColor.Magenta)
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Magenta),
    ctx.Border(b => [
        b.Text("Content with purple theme"),
        b.Text("Border and text match")
    ], title: "Purple Section")
)
