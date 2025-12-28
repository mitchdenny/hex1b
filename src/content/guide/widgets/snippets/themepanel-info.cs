ctx.ThemePanel(
    theme => theme.Clone()
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan)
        .Set(BorderTheme.BorderColor, Hex1bColor.Cyan),
    ctx.Border(b => [
        b.Text("ℹ Info: This is helpful information.")
    ], title: "Info")
)
