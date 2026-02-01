ctx.InfoBar(s => [
    s.Section("Normal"),
    s.Separator(" │ "),
    s.Section("Warning").Theme(t => t
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Yellow)),
    s.Separator(" │ "),
    s.Section("Error").Theme(t => t
        .Set(GlobalTheme.ForegroundColor, Hex1bColor.Red))
])
