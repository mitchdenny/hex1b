ctx.ThemePanel(
    theme => theme.Clone()
        .Set(BorderTheme.BorderColor, Hex1bColor.Cyan)
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.White),
    ctx.Border(b => [
        b.Text("Content with themed border")
    ], title: "Styled")
)
